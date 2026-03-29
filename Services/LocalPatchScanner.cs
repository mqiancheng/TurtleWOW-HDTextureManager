using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace HDTextureManager.Services
{
    public class LocalPatchInfo
    {
        public string PatchId { get; set; }
        public string FileName { get; set; }
        public string FilePath { get; set; }
        public long FileSize { get; set; }
        public DateTime LastModified { get; set; }
        public string Version { get; set; } = "未知";
        public string Variant { get; set; }  // 变体名称: "Standard", "Less Thick", "Performance" 等
        public bool IsDisabled { get; set; }  // 新增：是否禁用
    }

    public class LocalPatchScanner
    {
        public static List<LocalPatchInfo> ScanPatches(string gamePath)
        {
            var results = new List<LocalPatchInfo>();

            if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
                return results;

            var dataPath = Path.Combine(gamePath, "Data");
            if (!Directory.Exists(dataPath))
                return results;

            // 匹配 patch-a.mpq 和 _patch-a.mpq（禁用状态）
            var pattern = @"^_?patch-([a-z])(?:-.*)?\.mpq$";
            var files = Directory.GetFiles(dataPath, "*.mpq", SearchOption.TopDirectoryOnly)
                .Where(f => Regex.IsMatch(Path.GetFileName(f).ToLowerInvariant(), pattern));

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file).ToLowerInvariant();
                var match = Regex.Match(fileName, pattern);

                if (match.Success)
                {
                    var letter = match.Groups[1].Value.ToUpperInvariant();
                    var patchId = $"PATCH-{letter}";
                    var isDisabled = Path.GetFileName(file).StartsWith("_");

                    var info = new FileInfo(file);
                    
                    // 首先尝试从文件名提取版本
                    var version = TryExtractVersionFromFilename(fileName);
                    
                    // 如果文件名没有版本信息，尝试从 MPQ 文件中的 Patch.toc 读取
                    if (version == "未知")
                    {
                        version = MpqVersionReader.ReadVersionFromMpq(file);
                    }

                    // 尝试从文件名提取变体信息
                    var variant = TryExtractVariantFromFilename(fileName, patchId);
                    
                    // 如果文件名没有变体信息（启用状态的 patch-X.mpq），从 MPQ 读取
                    if (string.IsNullOrEmpty(variant) && !isDisabled)
                    {
                        variant = MpqVersionReader.ReadVariantFromMpq(file);
                    }
                    
                    // 如果仍然无法确定，默认为 Standard
                    if (string.IsNullOrEmpty(variant))
                    {
                        variant = "Standard";
                    }

                    results.Add(new LocalPatchInfo
                    {
                        PatchId = patchId,
                        FileName = Path.GetFileName(file),
                        FilePath = file,
                        FileSize = info.Length,
                        LastModified = info.LastWriteTime,
                        Version = version ?? "未知",
                        Variant = variant,
                        IsDisabled = isDisabled
                    });
                }
            }

            return results;
        }

        public static string TryExtractVersionFromFilename(string filename)
        {
            var match = Regex.Match(filename, @"v(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (match.Success)
                return "v" + match.Groups[1].Value;

            return "未知";
        }

        /// <summary>
        /// 尝试从文件名提取变体信息
        /// 例如: patch-l-less-thick.mpq → "Less Thick"
        ///       patch-l.mpq → null (需要从 MPQ 读取)
        /// </summary>
        public static string TryExtractVariantFromFilename(string filename, string patchId)
        {
            // 移除禁用前缀和下划线
            var cleanName = filename.TrimStart('_').ToLowerInvariant();
            
            // 提取 patch ID 后面的部分
            // 例如 patch-l-less-thick.mpq，提取 "less-thick"
            var patchPrefix = $"patch-{patchId.Replace("PATCH-", "").ToLowerInvariant()}-";
            
            if (cleanName.StartsWith(patchPrefix) && cleanName.EndsWith(".mpq"))
            {
                var middlePart = cleanName.Substring(patchPrefix.Length, cleanName.Length - patchPrefix.Length - 4);
                
                if (!string.IsNullOrEmpty(middlePart))
                {
                    // 标准化变体名称
                    var variantNormalized = middlePart.Replace("-", " ").Replace("_", " ");
                    
                    if (variantNormalized.IndexOf("Thicc", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        variantNormalized.IndexOf("Thick", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return "Less Thick";
                    }
                    else if (variantNormalized.IndexOf("Performance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             variantNormalized.Equals("Perf", StringComparison.OrdinalIgnoreCase))
                    {
                        return "Performance";
                    }
                    else if (variantNormalized.IndexOf("Regular", StringComparison.OrdinalIgnoreCase) >= 0 || 
                             variantNormalized.IndexOf("Standard", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return "Standard";
                    }
                    else
                    {
                        // 首字母大写的格式化
                        var words = variantNormalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        return string.Join(" ", words.Select(w => 
                            char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant()));
                    }
                }
            }

            // 文件名没有变体信息（如 patch-l.mpq），返回 null 表示需要从 MPQ 读取
            return null;
        }
    }
}