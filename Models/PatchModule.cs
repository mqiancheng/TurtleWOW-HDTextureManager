using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace HDTextureManager.Models
{
    public class PatchModule
    {
        public string Id { get; set; } = string.Empty;  // PATCH-A
        public string Variant { get; set; } = string.Empty;  // "Less Thicc", "Performance", ""
        public string Name { get; set; } = string.Empty;  // 显示名称
        public string Version { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string Category { get; set; } = "Optional";  // Core, Ultra, Optional
        public string Description { get; set; } = string.Empty;
        public List<string> Dependencies { get; set; } = new List<string>();

        [JsonIgnore]
        public string DisplayName => string.IsNullOrEmpty(Variant) ? Name : $"{Name} ({Variant})";

        [JsonIgnore]
        public string UniqueId => string.IsNullOrEmpty(Variant) ? Id : $"{Id}-{Variant.Replace(" ", "")}";

        /// <summary>
        /// 启用状态的文件名（统一格式：patch-X.mpq，不带变体后缀，注意大写）
        /// </summary>
        [JsonIgnore]
        public string ActiveFilename => $"patch-{Id.Replace("PATCH-", "").ToUpperInvariant()}.mpq";

        /// <summary>
        /// 禁用状态的文件名（带变体后缀以便识别：_patch-X-变体名称.mpq）
        /// </summary>
        [JsonIgnore]
        public string DisabledFilename => $"_patch-{Id.Replace("PATCH-", "").ToLowerInvariant()}-{Variant.ToLowerInvariant().Replace(" ", "-")}.mpq";

        /// <summary>
        /// 下载/安装时的目标文件名（带变体后缀）
        /// </summary>
        [JsonIgnore]
        public string DownloadFilename => $"patch-{Id.Replace("PATCH-", "").ToLowerInvariant()}-{Variant.ToLowerInvariant().Replace(" ", "-")}.mpq";

        [JsonIgnore]
        public string ActualFilename { get; set; }

        /// <summary>
        /// 当前实际的禁用文件名（带变体后缀）
        /// </summary>
        [JsonIgnore]
        public string ActualDisabledFilename => 
            string.IsNullOrEmpty(Variant) || Variant == "Standard"
                ? $"_{ActiveFilename}"  // _patch-x.mpq
                : DisabledFilename;      // _patch-x-variant.mpq

        [JsonIgnore]
        public string LocalVersion { get; set; }

        [JsonIgnore]
        public bool IsInstalled => !string.IsNullOrEmpty(LocalVersion) && !string.IsNullOrEmpty(ActualFilename);

        [JsonIgnore]
        public bool IsDisabled { get; set; }

        [JsonIgnore]
        public bool NeedsUpdate => IsInstalled  && (LocalVersion == "未知" || LocalVersion != Version);

        [JsonIgnore]
        public double FileSizeMB { get; set; }

        [JsonIgnore]
        public DateTime? LastModified { get; set; }

        [JsonIgnore]
        public double DownloadProgress { get; set; }

        [JsonIgnore]
        public bool IsDownloading { get; set; }
    }
}