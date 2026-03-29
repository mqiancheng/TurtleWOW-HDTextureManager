using HDTextureManager.Models;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HDTextureManager.Services
{
    /// <summary>
    /// 暂停令牌源
    /// </summary>
    public class PauseTokenSource
    {
        private bool _isPaused = false;
        private readonly object _lock = new object();

        public bool IsPaused
        {
            get { lock (_lock) return _isPaused; }
            set { lock (_lock) _isPaused = value; }
        }

        public async Task WaitWhilePausedAsync()
        {
            while (IsPaused)
            {
                await Task.Delay(100);
            }
        }
    }

    public class DownloadService
    {
        private readonly HttpClient _httpClient;

        public DownloadService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// 下载文件，支持断点续传和暂停
        /// </summary>
        public async Task DownloadAsync(
            PatchModule module,
            string gamePath,
            IProgress<double> progress,
            CancellationToken ct = default,
            PauseTokenSource pauseToken = null)
        {
            var tempPath = Path.Combine(gamePath, "Data", $"{module.DownloadFilename}.downloading");
            var targetPath = Path.Combine(gamePath, "Data", module.DownloadFilename);

            Directory.CreateDirectory(Path.GetDirectoryName(tempPath));

            var realUrl = await ResolveRealUrl(module.DownloadUrl, module.Id);

            // 检查是否存在已下载的部分（断点续传）
            long existingSize = 0;
            if (File.Exists(tempPath))
            {
                existingSize = new FileInfo(tempPath).Length;
            }

            // 创建 HTTP 请求，支持断点续传
            using (var request = new HttpRequestMessage(HttpMethod.Get, realUrl))
            {
                // 如果有已下载的部分，添加 Range 请求头
                if (existingSize > 0)
                {
                    request.Headers.Add("Range", $"bytes={existingSize}-");
                }

                using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct))
                {
                    // 检查服务器是否支持断点续传
                    bool isResumed = response.StatusCode == HttpStatusCode.PartialContent;
                    bool isNewDownload = response.StatusCode == HttpStatusCode.OK;

                    if (!isResumed && !isNewDownload)
                    {
                        response.EnsureSuccessStatusCode();
                    }

                    // 获取文件总大小
                    long totalBytes = -1;
                    long downloadStart = 0;

                    if (isResumed && response.Content.Headers.ContentRange != null)
                    {
                        // 断点续传：从 Content-Range 获取总大小
                        totalBytes = response.Content.Headers.ContentRange.Length ?? -1;
                        downloadStart = existingSize;
                    }
                    else if (response.Content.Headers.ContentLength.HasValue)
                    {
                        // 新下载或服务器不支持断点续传
                        if (isNewDownload && existingSize > 0)
                        {
                            // 服务器不支持断点续传，但有临时文件，需要重新下载
                            existingSize = 0;
                        }
                        totalBytes = response.Content.Headers.ContentLength.Value + existingSize;
                        downloadStart = existingSize;
                    }

                    var downloadedBytes = existingSize;

                    // 确定文件模式：新下载用 Create，断点续传用 Append
                    var fileMode = (isResumed && existingSize > 0) ? FileMode.Append : FileMode.Create;

                    using (var contentStream = await response.Content.ReadAsStreamAsync())
                    using (var fileStream = new FileStream(tempPath, fileMode, FileAccess.Write, FileShare.None, 8192, true))
                    {
                        var buffer = new byte[8192];
                        int read;

                        while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
                        {
                            // 检查是否暂停
                            if (pauseToken != null && pauseToken.IsPaused)
                            {
                                await pauseToken.WaitWhilePausedAsync();
                            }

                            await fileStream.WriteAsync(buffer, 0, read, ct);
                            downloadedBytes += read;

                            if (totalBytes > 0)
                            {
                                progress?.Report((double)downloadedBytes / totalBytes * 100);
                            }
                        }
                    }

                    // 验证下载完整性
                    if (totalBytes > 0 && downloadedBytes != totalBytes)
                    {
                        throw new IOException($"下载不完整: 期望 {totalBytes} bytes, 实际 {downloadedBytes} bytes");
                    }
                }
            }

            // 下载完成，移动到目标位置
            if (File.Exists(targetPath))
                File.Delete(targetPath);

            File.Move(tempPath, targetPath);
        }

        /// <summary>
        /// 检查是否存在未完成的下载
        /// </summary>
        public static bool HasIncompleteDownload(PatchModule module, string gamePath)
        {
            var tempPath = Path.Combine(gamePath, "Data", $"{module.DownloadFilename}.downloading");
            return File.Exists(tempPath) && new FileInfo(tempPath).Length > 0;
        }

        /// <summary>
        /// 获取未完成的下载进度（已下载字节数）
        /// </summary>
        public static long GetIncompleteDownloadSize(PatchModule module, string gamePath)
        {
            var tempPath = Path.Combine(gamePath, "Data", $"{module.DownloadFilename}.downloading");
            if (File.Exists(tempPath))
            {
                return new FileInfo(tempPath).Length;
            }
            return 0;
        }

        /// <summary>
        /// 清理未完成的下载文件
        /// </summary>
        public static void ClearIncompleteDownload(PatchModule module, string gamePath)
        {
            var tempPath = Path.Combine(gamePath, "Data", $"{module.DownloadFilename}.downloading");
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        public void DeleteModule(PatchModule module, string gamePath)
        {
            // 删除该模块的所有可能文件（启用和禁用状态）
            var activePath = Path.Combine(gamePath, "Data", module.ActiveFilename);
            var disabledPath = Path.Combine(gamePath, "Data", module.DisabledFilename);
            var tempPath = Path.Combine(gamePath, "Data", $"{module.DownloadFilename}.downloading");

            if (File.Exists(activePath))
                File.Delete(activePath);
            if (File.Exists(disabledPath))
                File.Delete(disabledPath);
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }

        private async Task<string> ResolveRealUrl(string pageUrl, string moduleId)
        {
            if (pageUrl.EndsWith(".mpq") || pageUrl.EndsWith(".zip"))
                return pageUrl;

            if (pageUrl.Contains("github.com") && pageUrl.Contains("releases"))
            {
                try
                {
                    var parts = pageUrl.Split('/');
                    if (parts.Length >= 5)
                    {
                        var owner = parts[3];
                        var repo = parts[4];
                        var apiUrl = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";

                        _httpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("HDManager/1.0");
                        var json = await _httpClient.GetStringAsync(apiUrl);

                        var match = Regex.Match(json,
                            $"\"browser_download_url\":\\s*\"([^\"]*{moduleId}[^\"]*\\.mpq)\"",
                            RegexOptions.IgnoreCase);

                        if (match.Success)
                            return match.Groups[1].Value;
                    }
                }
                catch { }
            }

            return pageUrl;
        }
    }
}
