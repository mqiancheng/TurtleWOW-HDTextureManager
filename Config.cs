using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace HDTextureManager
{
    /// <summary>
    /// 全局配置（存储在 %APPDATA%，包含游戏路径等设置）
    /// </summary>
    public class GlobalConfig
    {
        public string GamePath { get; set; } = string.Empty;
        public string Language { get; set; } = GetSystemLanguage();

        /// <summary>
        /// 根据系统语言获取默认语言
        /// </summary>
        private static string GetSystemLanguage()
        {
            var systemCulture = CultureInfo.CurrentUICulture;
            var cultureName = systemCulture.Name.ToLowerInvariant();
            
            // 检查各种语言
            if (cultureName.StartsWith("zh"))  // 中文
                return "zh-CN";
            else if (cultureName.StartsWith("es"))  // 西班牙语
                return "es";
            else if (cultureName.StartsWith("de"))  // 德语
                return "de";
            else if (cultureName.StartsWith("pt"))  // 葡萄牙语
                return "pt";
            else if (cultureName.StartsWith("ru"))  // 俄语
                return "ru";
            
            // 默认英语
            return "en";
        }

        private static readonly string GlobalConfigDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HDTextureManager"
        );

        private static readonly string GlobalConfigPath = Path.Combine(GlobalConfigDir, "config.json");

        public static GlobalConfig Load()
        {
            try
            {
                if (File.Exists(GlobalConfigPath))
                {
                    var json = File.ReadAllText(GlobalConfigPath);
                    return JsonConvert.DeserializeObject<GlobalConfig>(json) ?? new GlobalConfig();
                }
            }
            catch (Exception)
            {
                // 加载失败时返回默认配置
            }
            return new GlobalConfig();
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(GlobalConfigDir);
                var json = JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(GlobalConfigPath, json);
            }
            catch (Exception)
            {
                // 保存失败时静默处理
            }
        }
    }

    /// <summary>
    /// 游戏补丁配置（存储在游戏目录的 Data 文件夹，包含补丁版本信息）
    /// </summary>
    public class GameConfig
    {
        /// <summary>
        /// 已安装补丁的版本记录
        /// Key: 模块唯一ID (如 "PATCH-A", "PATCH-L-LessThick"), Value: 版本号 (如 "v5.3.1")
        /// </summary>
        public Dictionary<string, string> InstalledVersions { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// 获取游戏补丁配置文件路径
        /// </summary>
        private static string GetConfigPath(string gamePath)
        {
            if (string.IsNullOrEmpty(gamePath))
                return null;
            return Path.Combine(gamePath, "Data", "HDTextureManager.json");
        }

        public static GameConfig Load(string gamePath)
        {
            if (string.IsNullOrEmpty(gamePath))
                return new GameConfig();

            var configPath = GetConfigPath(gamePath);
            if (configPath == null || !File.Exists(configPath))
                return new GameConfig();

            try
            {
                var json = File.ReadAllText(configPath);
                var config = JsonConvert.DeserializeObject<GameConfig>(json);
                return config ?? new GameConfig();
            }
            catch (Exception)
            {
                return new GameConfig();
            }
        }

        public void Save(string gamePath)
        {
            if (string.IsNullOrEmpty(gamePath))
            {
                return;
            }

            try
            {
                var configPath = GetConfigPath(gamePath);
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                var json = JsonConvert.SerializeObject(this, Newtonsoft.Json.Formatting.Indented);
                File.WriteAllText(configPath, json);
            }
            catch (Exception)
            {
                // 保存失败时静默处理
            }
        }

        /// <summary>
        /// 清理不存在的补丁记录
        /// </summary>
        public void CleanupMissingPatches(List<string> existingPatchIds)
        {
            var toRemove = InstalledVersions.Keys.Where(k => !existingPatchIds.Contains(k)).ToList();
            foreach (var key in toRemove)
            {
                InstalledVersions.Remove(key);
            }
        }
    }

    /// <summary>
    /// 配置管理器（整合全局配置和游戏配置）
    /// </summary>
    public class Config
    {
        private GlobalConfig _globalConfig;
        private GameConfig _gameConfig;

        /// <summary>
        /// 游戏路径（来自全局配置）
        /// </summary>
        public string GamePath
        {
            get => _globalConfig.GamePath;
            set => _globalConfig.GamePath = value;
        }

        /// <summary>
        /// 语言设置（来自全局配置）
        /// </summary>
        public string Language
        {
            get => _globalConfig.Language;
            set => _globalConfig.Language = value;
        }

        /// <summary>
        /// 已安装补丁版本（来自游戏配置）
        /// </summary>
        public Dictionary<string, string> InstalledVersions => _gameConfig?.InstalledVersions ?? new Dictionary<string, string>();

        public Config()
        {
            _globalConfig = new GlobalConfig();
            _gameConfig = new GameConfig();
        }

        /// <summary>
        /// 加载配置（全局配置 + 游戏配置）
        /// </summary>
        public static Config Load()
        {
            var config = new Config();
            
            // 加载全局配置
            config._globalConfig = GlobalConfig.Load();
            
            // 如果有游戏路径，加载游戏配置
            if (!string.IsNullOrEmpty(config.GamePath))
            {
                config._gameConfig = GameConfig.Load(config.GamePath);
            }

            return config;
        }

        /// <summary>
        /// 重新加载游戏配置（当游戏路径改变时调用）
        /// </summary>
        public void ReloadGameConfig()
        {
            if (!string.IsNullOrEmpty(GamePath))
            {
                _gameConfig = GameConfig.Load(GamePath);
            }
            else
            {
                _gameConfig = new GameConfig();
            }
        }

        /// <summary>
        /// 保存所有配置
        /// </summary>
        public void Save()
        {
            _globalConfig.Save();
            _gameConfig?.Save(GamePath);
        }

        /// <summary>
        /// 保存全局配置（仅游戏路径等设置）
        /// </summary>
        public void SaveGlobal()
        {
            _globalConfig.Save();
        }

        /// <summary>
        /// 保存游戏配置（仅补丁信息）
        /// </summary>
        public void SaveGameConfig()
        {
            _gameConfig?.Save(GamePath);
        }

        /// <summary>
        /// 清理不存在的补丁记录
        /// </summary>
        public void CleanupMissingPatches(List<string> existingPatchIds)
        {
            _gameConfig?.CleanupMissingPatches(existingPatchIds);
        }
    }
}
