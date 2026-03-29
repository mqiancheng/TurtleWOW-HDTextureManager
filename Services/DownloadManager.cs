using HDTextureManager.Models;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace HDTextureManager.Services
{
    /// <summary>
    /// 下载任务信息
    /// </summary>
    public class DownloadTask
    {
        public PatchModule Module { get; set; }
        public CancellationTokenSource CancellationTokenSource { get; set; }
        public PauseTokenSource PauseTokenSource { get; set; }
        public Task Task { get; set; }
        public double Progress { get; set; }
        public bool IsPaused => PauseTokenSource?.IsPaused ?? false;
        public DateTime StartTime { get; set; }
        public long DownloadedBytes { get; set; }
        public long TotalBytes { get; set; }
    }

    /// <summary>
    /// 下载管理器 - 管理多个并行下载任务
    /// </summary>
    public class DownloadManager
    {
        private readonly ConcurrentDictionary<string, DownloadTask> _tasks = new();
        private readonly DownloadService _downloadService;
        private readonly Dispatcher _dispatcher;
        private readonly Action<string> _statusCallback;
        private readonly Action _updateAllCardsCallback;
        private readonly Action<PatchModule> _updateModuleCallback;

        public int ActiveDownloadCount => _tasks.Count;
        public bool HasActiveDownloads => !_tasks.IsEmpty;

        public DownloadManager(HttpClient httpClient, Dispatcher dispatcher, Action<string> statusCallback, Action updateAllCardsCallback, Action<PatchModule> updateModuleCallback = null)
        {
            _downloadService = new DownloadService(httpClient);
            _dispatcher = dispatcher;
            _statusCallback = statusCallback;
            _updateAllCardsCallback = updateAllCardsCallback;
            _updateModuleCallback = updateModuleCallback;
        }

        /// <summary>
        /// 开始下载
        /// </summary>
        public async Task StartDownloadAsync(PatchModule module, string gamePath)
        {
            if (_tasks.ContainsKey(module.UniqueId))
                return;

            var cts = new CancellationTokenSource();
            var pts = new PauseTokenSource();
            var task = new DownloadTask
            {
                Module = module,
                CancellationTokenSource = cts,
                PauseTokenSource = pts,
                StartTime = DateTime.Now
            };

            _tasks[module.UniqueId] = task;
            UpdateStatusText();

            try
            {
                module.IsDownloading = true;
                module.DownloadProgress = 0;

                var progress = new Progress<double>(p =>
                {
                    task.Progress = p;
                    module.DownloadProgress = p;
                    UpdateStatusText();
                    
                    // 每 5% 更新一次卡片 UI，避免过于频繁刷新
                    if ((int)p % 5 == 0 || p >= 99)
                    {
                        _dispatcher.Invoke(() => _updateAllCardsCallback?.Invoke());
                    }
                });

                await _downloadService.DownloadAsync(module, gamePath, progress, cts.Token, pts);

                // 下载完成
                module.IsDownloading = false;
                _tasks.TryRemove(module.UniqueId, out _);
                UpdateStatusText();

                // 自动禁用
                var dataPath = Path.Combine(gamePath, "Data");
                var downloadedFilePath = Path.Combine(dataPath, module.DownloadFilename);
                var version = MpqVersionReader.ReadVersionFromMpq(downloadedFilePath);
                
                if (version != null)
                {
                    module.LocalVersion = version;
                }
                else
                {
                    module.LocalVersion = module.Version;
                }

                var disabledFilePath = Path.Combine(dataPath, module.DisabledFilename);
                if (File.Exists(downloadedFilePath))
                {
                    if (File.Exists(disabledFilePath))
                    {
                        File.Delete(disabledFilePath);
                    }
                    File.Move(downloadedFilePath, disabledFilePath);
                    module.ActualFilename = module.DisabledFilename.TrimStart('_');
                    module.IsDisabled = true;
                }

                _dispatcher.Invoke(() =>
                {
                    // 下载完成，刷新该模块的 UI（进度条隐藏）
                    _updateModuleCallback?.Invoke(module);
                    // 同时刷新所有卡片
                    _updateAllCardsCallback?.Invoke();
                });
            }
            catch (OperationCanceledException)
            {
                module.IsDownloading = false;
                _tasks.TryRemove(module.UniqueId, out _);
                UpdateStatusText();
                throw;
            }
            catch (Exception)
            {
                module.IsDownloading = false;
                _tasks.TryRemove(module.UniqueId, out _);
                UpdateStatusText();
                throw;
            }
        }

        /// <summary>
        /// 暂停下载
        /// </summary>
        public void PauseDownload(string uniqueId)
        {
            if (_tasks.TryGetValue(uniqueId, out var task))
            {
                task.PauseTokenSource.IsPaused = true;
                UpdateStatusText();
                _dispatcher.Invoke(() => _updateAllCardsCallback?.Invoke());
            }
        }

        /// <summary>
        /// 继续下载
        /// </summary>
        public void ResumeDownload(string uniqueId)
        {
            if (_tasks.TryGetValue(uniqueId, out var task))
            {
                task.PauseTokenSource.IsPaused = false;
                UpdateStatusText();
                _dispatcher.Invoke(() => _updateAllCardsCallback?.Invoke());
            }
        }

        /// <summary>
        /// 取消下载
        /// </summary>
        public void CancelDownload(string uniqueId)
        {
            if (_tasks.TryGetValue(uniqueId, out var task))
            {
                task.CancellationTokenSource?.Cancel();
            }
        }

        /// <summary>
        /// 检查是否正在下载
        /// </summary>
        public bool IsDownloading(string uniqueId)
        {
            return _tasks.ContainsKey(uniqueId);
        }

        /// <summary>
        /// 检查是否已暂停
        /// </summary>
        public bool IsPaused(string uniqueId)
        {
            if (_tasks.TryGetValue(uniqueId, out var task))
            {
                return task.IsPaused;
            }
            return false;
        }

        /// <summary>
        /// 获取下载进度
        /// </summary>
        public double GetProgress(string uniqueId)
        {
            if (_tasks.TryGetValue(uniqueId, out var task))
            {
                return task.Progress;
            }
            return 0;
        }

        /// <summary>
        /// 更新状态栏文本
        /// </summary>
        private void UpdateStatusText()
        {
            var count = _tasks.Count;
            if (count == 0)
            {
                _dispatcher.Invoke(() => _statusCallback?.Invoke("就绪"));
            }
            else if (count == 1)
            {
                var task = _tasks.Values.First();
                var status = task.IsPaused ? "已暂停" : $"{task.Progress:F0}%";
                _dispatcher.Invoke(() => _statusCallback?.Invoke(
                    $"正在下载 {task.Module.Id}: {status}"));
            }
            else
            {
                var activeCount = _tasks.Values.Count(t => !t.IsPaused);
                var pausedCount = _tasks.Values.Count(t => t.IsPaused);
                var activeTasks = _tasks.Values.Where(t => !t.IsPaused).ToList();
                var avgProgress = activeTasks.Any() ? activeTasks.Average(t => t.Progress) : 0;
                
                string statusText;
                if (activeCount == 0)
                {
                    // 全部暂停
                    statusText = $"已暂停 {pausedCount} 个下载任务";
                }
                else if (pausedCount > 0)
                {
                    statusText = $"正在下载 {count} 个文件 ({activeCount} 进行中, {pausedCount} 已暂停), 平均进度: {avgProgress:F0}%";
                }
                else
                {
                    statusText = $"正在下载 {count} 个文件，平均进度: {avgProgress:F0}%";
                }
                _dispatcher.Invoke(() => _statusCallback?.Invoke(statusText));
            }
        }

        /// <summary>
        /// 取消所有下载
        /// </summary>
        public void CancelAll()
        {
            foreach (var task in _tasks.Values)
            {
                task.CancellationTokenSource?.Cancel();
            }
        }
    }
}
