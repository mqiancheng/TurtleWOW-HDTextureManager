using HDTextureManager.Models;
using HDTextureManager.Services;
using static HDTextureManager.Services.LocalizationService;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;


namespace HDTextureManager
{
    public partial class MainWindow : Window
    {
        private readonly HttpClient _httpClient;
        private readonly HtmlParser _parser;
        private readonly DownloadManager _downloadManager;
        private readonly Config _config;
        private List<PatchModule> _modules = new List<PatchModule>();

        public MainWindow()
        {
            InitializeComponent();

            // 设置版本号
            var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";

            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "HDTextureManager/1.0");
            _parser = new HtmlParser(_httpClient);
            // 加载配置（包括全局配置和游戏配置）
            _config = Config.Load();
            // 初始化下载管理器
            _downloadManager = new DownloadManager(_httpClient, Dispatcher, 
                status => StatusText.Text = status, 
                () => RefreshAllCards(),
                module => RefreshModuleCard(module));

            PathTextBox.Text = _config.GamePath;
            
            // 初始化语言设置
            InitializeLanguage();

            Loaded += async (s, e) => await LoadModulesAsync();
        }

        #region 窗口控制

        private void TitleBar_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // 拖动窗口
            DragMove();
        }

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion

        private void InitializeLanguage()
        {
            // 验证配置的语言是否支持，不支持则使用默认语言
            var targetLanguage = LocalizationService.IsSupportedLanguage(_config.Language) 
                ? _config.Language 
                : LocalizationService.GetDefaultLanguage();
            
            // 如果配置的语言无效，更新配置
            if (_config.Language != targetLanguage)
            {
                _config.Language = targetLanguage;
                _config.SaveGlobal();
            }
            
            // 设置语言下拉框
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (item.Tag.ToString() == targetLanguage)
                {
                    LanguageComboBox.SelectedItem = item;
                    break;
                }
            }
            // 应用语言
            Instance.ChangeLanguage(targetLanguage);
        }

        private string T(string key) => Instance[key];
        private string T(string key, params object[] args) => Instance.GetString(key, args);

        private async System.Threading.Tasks.Task LoadModulesAsync()
        {
            // 显示加载动画
            LoadingOverlay.Visibility = Visibility.Visible;
            
            StatusText.Text = T("Status_Scanning");
            RefreshBtn.IsEnabled = false;

            try
            {
                // 保存当前正在下载的模块状态（用于刷新后恢复显示）
                var downloadingStates = new Dictionary<string, (bool IsDownloading, double DownloadProgress)>();
                if (_modules != null)
                {
                    foreach (var m in _modules.Where(m => m.IsDownloading))
                    {
                        downloadingStates[m.UniqueId] = (m.IsDownloading, m.DownloadProgress);
                    }
                }

                // 1. 检查游戏路径是否已设置
                if (string.IsNullOrEmpty(_config.GamePath) || !Directory.Exists(_config.GamePath))
                {
                    StatusText.Text = T("Status_SetGameDir");
                    RefreshBtn.IsEnabled = true;
                    
                    // 仍然加载远程模块列表，但只显示不处理本地文件
                    _modules = await _parser.ParseModulesAsync();
                    
                    // 渲染模块卡片（显示未安装状态）
                    CorePanel.Children.Clear();
                    OptionalPanel.Children.Clear();
                    AudioPanel.Children.Clear();
                    UltraPanel.Children.Clear();

                    foreach (var module in _modules)
                    {
                        var card = CreateModuleCard(module);
                        switch (module.Category)
                        {
                            case "Core": CorePanel.Children.Add(card); break;
                            case "Optional": OptionalPanel.Children.Add(card); break;
                            case "Audio": AudioPanel.Children.Add(card); break;
                            case "Ultra": UltraPanel.Children.Add(card); break;
                            default: OptionalPanel.Children.Add(card); break;
                        }
                    }
                    
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    return;
                }

                // 2. 检查系统依赖（DXVK 和 VanillaHelpers）
                var sysDepsResult = CheckSystemDependencies();
                if (!sysDepsResult.IsValid)
                {
                    MessageBox.Show(sysDepsResult.Message, T("Msg_InfoTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                    StatusText.Text = sysDepsResult.StatusMessage;
                    RefreshBtn.IsEnabled = true;
                    LoadingOverlay.Visibility = Visibility.Collapsed;
                    return;
                }

                // 3. 扫描本地已安装的补丁（从 MPQ 文件读取版本号）
                var localPatches = new List<LocalPatchInfo>();
                if (!string.IsNullOrEmpty(_config.GamePath) && Directory.Exists(_config.GamePath))
                {
                    localPatches = LocalPatchScanner.ScanPatches(_config.GamePath);
                    if (localPatches.Count > 0)
                    {
                        StatusText.Text = T("Status_Loading", localPatches.Count);
                    }
                }

                // 2. 获取远程模块列表
                _modules = await _parser.ParseModulesAsync();

                // 恢复正在下载的模块状态
                foreach (var module in _modules)
                {
                    if (downloadingStates.TryGetValue(module.UniqueId, out var state))
                    {
                        module.IsDownloading = state.Item1;
                        module.DownloadProgress = state.Item2;
                    }
                }

                // 3. 匹配本地补丁与远程模块
                // 本地补丁版本已从 MPQ 文件的 Patch.toc 中读取
                foreach (var module in _modules)
                {
                    var localFile = FindMatchingLocalPatch(localPatches, module);
                    if (localFile != null)
                    {
                        // ActualFilename 始终使用标准启用格式 patch-X.mpq
                        module.ActualFilename = module.ActiveFilename;
                        module.LocalVersion = localFile.Version; // 从 MPQ 读取的版本
                        module.IsDisabled = localFile.IsDisabled;
                        module.FileSizeMB = localFile.FileSize / (1024.0 * 1024.0);
                        module.LastModified = localFile.LastModified;
                        
                        // 对于启用状态的补丁，如果配置中没有记录变体，保存到配置

                    }
                }
                


                // 4. 清理配置中不存在的补丁记录
                var installedIds = _modules.Where(m => m.IsInstalled).Select(m => m.UniqueId).ToList();
                var toRemove = _config.InstalledVersions.Keys.Where(k => !installedIds.Contains(k)).ToList();
                foreach (var key in toRemove)
                {
                    _config.InstalledVersions.Remove(key);
                }
                _config.Save();

                // 6. 检查是否有未识别的本地文件
                var unknownPatches = localPatches.Where(p => !_modules.Any(m => 
                    m.Id == p.PatchId && (string.IsNullOrEmpty(m.Variant) || 
                    p.FileName.ToLowerInvariant().Contains(m.Variant.ToLowerInvariant().Replace(" ", "-"))))).ToList();
                
                // 6. 渲染UI - 每个变体独立显示为卡片
                CorePanel.Children.Clear();
                OptionalPanel.Children.Clear();
                AudioPanel.Children.Clear();
                UltraPanel.Children.Clear();

                foreach (var module in _modules)
                {
                    var card = CreateModuleCard(module);
                    switch (module.Category)
                    {
                        case "Core": CorePanel.Children.Add(card); break;
                        case "Optional": OptionalPanel.Children.Add(card); break;
                        case "Audio": AudioPanel.Children.Add(card); break;
                        case "Ultra": UltraPanel.Children.Add(card); break;
                        default: OptionalPanel.Children.Add(card); break;
                    }
                }

                var statusMsg = T("Status_Loaded", _modules.Count);
                if (localPatches.Count > 0)
                    statusMsg += T("Status_LoadedLocal", localPatches.Count);
                if (unknownPatches.Count > 0)
                    statusMsg += T("Status_LoadedUnknown", unknownPatches.Count);
                StatusText.Text = statusMsg;
            }
            catch (Exception ex)
            {
                StatusText.Text = T("Status_LoadFailed", ex.Message);
                MessageBox.Show(T("Msg_LoadError", ex.Message), T("Msg_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RefreshBtn.IsEnabled = true;
                LoadingOverlay.Visibility = Visibility.Collapsed;
                // 确保设置按钮和其他按钮始终可用
                SettingsBtn.IsEnabled = true;
                OpenWebBtn.IsEnabled = true;
                LaunchGameBtn.IsEnabled = true;
            }
        }

        /// <summary>
        /// 系统依赖检查结果
        /// </summary>
        private class SystemDependencyResult
        {
            public bool IsValid { get; set; }
            public string Message { get; set; }
            public string StatusMessage { get; set; }
        }

        /// <summary>
        /// 检查系统依赖（DXVK 和 VanillaHelpers）
        /// </summary>
        private SystemDependencyResult CheckSystemDependencies()
        {
            var result = new SystemDependencyResult { IsValid = true };
            
            // 1. 检查 DLL 文件是否存在
            var d3d9Path = Path.Combine(_config.GamePath, "d3d9.dll");
            var vanillaHelpersPath = Path.Combine(_config.GamePath, "VanillaHelpers.dll");
            
            bool hasD3D9 = File.Exists(d3d9Path);
            bool hasVanillaHelpers = File.Exists(vanillaHelpersPath);
            
            if (!hasD3D9 || !hasVanillaHelpers)
            {
                var missingList = new List<string>();
                if (!hasD3D9) missingList.Add("d3d9.dll (DXVK)");
                if (!hasVanillaHelpers) missingList.Add("VanillaHelpers.dll");
                
                result.IsValid = false;
                result.Message = T("Msg_MissingSystemDeps", string.Join("\n", missingList));
                result.StatusMessage = T("Status_MissingSystemDeps");
                return result;
            }
            
            // 2. 检查 dlls.txt 是否包含对应条目
            var dllsTxtPath = Path.Combine(_config.GamePath, "dlls.txt");
            if (File.Exists(dllsTxtPath))
            {
                try
                {
                    var dllsContent = File.ReadAllText(dllsTxtPath).ToLowerInvariant();
                    bool hasVanillaHelpersEntry = dllsContent.Contains("vanillahelpers.dll");
                    bool hasDxvkEntry = dllsContent.Contains("dxvk");
                    
                    if (!hasVanillaHelpersEntry || !hasDxvkEntry)
                    {
                        var missingEntries = new List<string>();
                        if (!hasVanillaHelpersEntry) missingEntries.Add("VanillaHelpers.dll");
                        if (!hasDxvkEntry) missingEntries.Add("dxvk");
                        
                        result.IsValid = false;
                        result.Message = T("Msg_DisabledSystemDeps", string.Join("\n", missingEntries));
                        result.StatusMessage = T("Status_DisabledSystemDeps");
                        return result;
                    }
                }
                catch (Exception)
                {
                    // 如果读取失败，不阻止用户继续
                }
            }
            
            return result;
        }

        /// <summary>
        /// 根据模块信息查找匹配的本地补丁文件，支持变体版本
        /// </summary>
        private LocalPatchInfo FindMatchingLocalPatch(List<LocalPatchInfo> localPatches, PatchModule module)
        {
            // 首先按 PATCH ID 筛选
            var candidates = localPatches.Where(p => p.PatchId == module.Id).ToList();
            if (candidates.Count == 0) return null;
            
            // 如果没有变体，返回 Standard 变体或没有变体后缀的文件
            if (string.IsNullOrEmpty(module.Variant) || module.Variant == "Standard")
            {
                // 优先返回明确标记为 Standard 变体的文件
                var standardCandidate = candidates.FirstOrDefault(p => 
                    p.Variant?.Equals("Standard", StringComparison.OrdinalIgnoreCase) == true);
                if (standardCandidate != null) return standardCandidate;
                
                // 其次返回不带变体后缀且未被识别为其他变体的文件（如 patch-a.mpq）
                // 注意：如果文件已被识别为其他变体（如 Less Thick），则不应该匹配到 Standard
                return candidates.FirstOrDefault(p => 
                {
                    var fileName = Path.GetFileNameWithoutExtension(p.FileName).ToLowerInvariant();
                    // 匹配 patch-a.mpq 或 _patch-a.mpq，但不匹配 patch-a-variant.mpq
                    var isSimpleName = Regex.IsMatch(fileName, @"^_?patch-[a-z]$");
                    // 确保该文件未被识别为其他非 Standard 变体
                    var isNotOtherVariant = string.IsNullOrEmpty(p.Variant) || 
                                           p.Variant.Equals("Standard", StringComparison.OrdinalIgnoreCase);
                    return isSimpleName && isNotOtherVariant;
                });
            }
            
            // 有变体的情况，优先使用从 MPQ 读取的变体信息进行匹配
            var variantMatch = candidates.FirstOrDefault(p => 
                p.Variant?.Equals(module.Variant, StringComparison.OrdinalIgnoreCase) == true);
            if (variantMatch != null) return variantMatch;
            
            // 回退到根据文件名匹配（兼容旧文件）
            var variantSuffix = module.Variant.ToLowerInvariant().Replace(" ", "-");
            return candidates.FirstOrDefault(p => 
            {
                var fileName = p.FileName.ToLowerInvariant();
                return fileName.Contains($"-{variantSuffix}");
            });
        }

        /// <summary>
        /// <summary>
        /// 为单个模块创建卡片
        /// </summary>
        private Border CreateModuleCard(PatchModule module)
        {
            var border = new Border
            {
                Style = (Style)FindResource("CardStyle"),
                Width = 280,
                MinHeight = 150,
                Tag = module  // 设置 Tag 以便后续查找和更新
            };

            // 根据状态设置边框颜色
            UpdateCardBorderStyle(border, module);

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // ========== 头部：ID 和 版本信息 ==========
            var header = new Grid();
            header.ColumnDefinitions.Add(new ColumnDefinition());
            header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 左侧：Patch ID + 变体名称
            var idText = new TextBlock
            {
                Text = string.IsNullOrEmpty(module.Variant) ? module.Id : $"{module.Id} - {module.Variant}",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF5A623")),
                VerticalAlignment = VerticalAlignment.Center
            };
            header.Children.Add(idText);

            // 右侧：版本信息
            var versionPanel = CreateVersionPanel(module);
            Grid.SetColumn(versionPanel, 1);
            header.Children.Add(versionPanel);
            grid.Children.Add(header);

            // ========== 名称 ==========
            var nameText = new TextBlock
            {
                Text = module.Name,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                Foreground = new SolidColorBrush(Colors.White),
                TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(0, 8, 0, 4)
            };
            Grid.SetRow(nameText, 1);
            grid.Children.Add(nameText);

            // ========== 描述 ==========
            var descText = new TextBlock
            {
                Text = string.IsNullOrEmpty(module.Description) ? T("Module_NoDescription") : module.Description,
                FontSize = 11,
                Foreground = new SolidColorBrush(Colors.White),
                Opacity = 0.6,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16,
                MaxHeight = 48
            };
            Grid.SetRow(descText, 2);
            grid.Children.Add(descText);

            // ========== 依赖 ==========
            if (module.Dependencies.Count > 0)
            {
                var depText = new TextBlock
                {
                    Text = T("Module_Dependencies", string.Join(", ", module.Dependencies)),
                    FontSize = 10,
                    Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF4A9EFF")),
                    Opacity = 0.8,
                    Margin = new Thickness(0, 4, 0, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                Grid.SetRow(depText, 3);
                grid.Children.Add(depText);
            }

            // ========== 按钮区域 ==========
            var btnPanel = CreateActionButtons(module);
            Grid.SetRow(btnPanel, 4);
            grid.Children.Add(btnPanel);

            border.Child = grid;
            return border;
        }

        /// <summary>
        /// 更新卡片边框样式
        /// </summary>
        private void UpdateCardBorderStyle(Border border, PatchModule module)
        {
            if (!module.IsInstalled)
            {
                border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#33FFFFFF"));
                border.BorderThickness = new Thickness(1);
                return;
            }

            // 检查是否版本未知
            var hasUnknownVersion = module.LocalVersion == "未知";

            if (module.IsDisabled)
            {
                border.BorderBrush = new SolidColorBrush(Colors.Gray);
                border.BorderThickness = new Thickness(2);
                border.Background = new SolidColorBrush(Color.FromArgb(0x10, 0x80, 0x80, 0x80));
                border.Opacity = 0.7;
            }
            else if (module.NeedsUpdate || hasUnknownVersion)
            {
                // 需要更新或版本未知时显示金色边框
                border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF5A623"));
                border.BorderThickness = new Thickness(2);
                border.Background = new SolidColorBrush(Color.FromArgb(0x15, 0xF5, 0xA6, 0x23));
            }
            else
            {
                border.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF00D084"));
                border.BorderThickness = new Thickness(2);
            }
        }

        /// <summary>
        /// 创建版本信息面板
        /// </summary>
        private StackPanel CreateVersionPanel(PatchModule module)
        {
            var versionPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            // 最新版本显示
            var latestVerText = new TextBlock
            {
                Text = module.Version == "unknown" ? T("Module_LatestVersionUnknown") : T("Module_LatestVersion", module.Version),
                FontSize = 11,
                Foreground = module.Version == "unknown" ? new SolidColorBrush(Colors.Gray) : new SolidColorBrush(Colors.White),
                Opacity = module.Version == "unknown" ? 0.5 : 0.9,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            versionPanel.Children.Add(latestVerText);

            // 已安装版本（如果已安装）
            if (module.IsInstalled)
            {
                var installedPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 2, 0, 0)
                };

                var versionText = module.LocalVersion ?? T("Module_VersionUnknown");
                var hasUnknownVersion = versionText == "未知";
                
                var localVerText = new TextBlock
                {
                    Text = T("Module_LocalVersion", versionText),
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold
                };

                if (hasUnknownVersion)
                {
                    localVerText.Foreground = new SolidColorBrush(Colors.Orange);
                    localVerText.ToolTip = T("Module_VersionTooltip");
                }
                else if (module.NeedsUpdate)
                {
                    localVerText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFF5A623"));
                }
                else
                {
                    localVerText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF00D084"));
                }

                installedPanel.Children.Add(localVerText);

                // 显示对勾标记：已是最新时才显示，未知版本时不显示
                if (!module.NeedsUpdate && !hasUnknownVersion)
                {
                    var checkMark = new TextBlock
                    {
                        Text = " ✓",
                        FontSize = 10,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF00D084")),
                        FontWeight = FontWeights.Bold
                    };
                    installedPanel.Children.Add(checkMark);
                }

                versionPanel.Children.Add(installedPanel);

                // 显示文件大小
                if (module.FileSizeMB > 0)
                {
                    var sizeText = new TextBlock
                    {
                        Text = T("Module_SizeMB", module.FileSizeMB),
                        FontSize = 9,
                        Foreground = new SolidColorBrush(Colors.White),
                        Opacity = 0.5,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 2, 0, 0)
                    };
                    versionPanel.Children.Add(sizeText);
                }
            }

            return versionPanel;
        }

        /// <summary>
        /// 创建操作按钮
        /// </summary>
        private StackPanel CreateActionButtons(PatchModule module)
        {
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 10, 0, 0)
            };

            // 主操作按钮
            string btnText;
            Style btnStyle;
            bool isEnabled;

            var hasUnknownVersion = module.IsInstalled && module.LocalVersion == "未知";

            // 检查是否有未完成的下载（断点续传）
            var hasIncompleteDownload = !string.IsNullOrEmpty(_config.GamePath) && 
                                        DownloadService.HasIncompleteDownload(module, _config.GamePath);

            // 检查是否已暂停
            bool isPaused = module.IsDownloading && _downloadManager.IsPaused(module.UniqueId);
            
            // 检查是否正在下载（刷新后恢复状态）
            if (module.IsDownloading && !isPaused)
            {
                btnText = T("Action_Downloading");
                btnStyle = (Style)FindResource("PrimaryButton");
                isEnabled = false;
            }
            else if (isPaused)
            {
                btnText = T("Action_Paused");
                btnStyle = (Style)FindResource("ActionButton");
                isEnabled = true;
            }
            else if (hasIncompleteDownload)
            {
                var incompleteSize = DownloadService.GetIncompleteDownloadSize(module, _config.GamePath);
                var sizeText = incompleteSize > 1024 * 1024 
                    ? $"{incompleteSize / (1024.0 * 1024.0):F1} MB" 
                    : $"{incompleteSize / 1024.0:F0} KB";
                btnText = T("Action_Resume", sizeText);
                btnStyle = (Style)FindResource("PrimaryButton");
                isEnabled = true;
            }
            else if (!module.IsInstalled)
            {
                btnText = T("Action_Download");
                btnStyle = (Style)FindResource("PrimaryButton");
                isEnabled = true;
            }
            else if (module.IsDisabled)
            {
                btnText = T("Action_Disabled");
                btnStyle = (Style)FindResource("ActionButton");
                isEnabled = false;
            }
            else if (hasUnknownVersion)
            {
                // 版本未知，允许下载
                btnText = T("Action_Download");
                btnStyle = (Style)FindResource("PrimaryButton");
                isEnabled = true;
            }
            else if (module.NeedsUpdate)
            {
                btnText = T("Action_Update", module.Version);
                btnStyle = (Style)FindResource("PrimaryButton");
                isEnabled = true;
            }
            else
            {
                btnText = T("Action_Latest");
                btnStyle = (Style)FindResource("SuccessButton");
                isEnabled = false;
            }

            // 进度条（如果正在下载则显示）
            var progressBar = new ProgressBar
            {
                Style = (Style)FindResource("DownloadProgressBar"),
                Width = 100,
                Margin = new Thickness(0, 4, 8, 0),
                Maximum = 100,
                Value = module.DownloadProgress,
                Visibility = module.IsDownloading ? Visibility.Visible : Visibility.Collapsed
            };

            var actionBtn = new Button
            {
                Content = btnText,
                Style = btnStyle,
                IsEnabled = isEnabled,
                Width = 100,
                Height = 32,
                Margin = new Thickness(0, 0, 8, 0),
                Tag = progressBar // 将进度条关联到按钮
            };

            actionBtn.Click += async (s, e) =>
            {
                // 防止重复点击
                if (module.IsDownloading)
                    return;

                // 禁用按钮并显示进度条
                actionBtn.IsEnabled = false;
                actionBtn.Content = T("Action_Downloading");
                progressBar.Visibility = Visibility.Visible;
                progressBar.Value = 0;

                try
                {
                    // 创建进度报告
                    var progress = new Progress<double>(p =>
                    {
                        Dispatcher.Invoke(() =>
                        {
                            progressBar.Value = p;
                            StatusText.Text = T("Status_Downloading", module.Id, p);
                        });
                    });

                    await HandleModuleActionWithProgress(module, progress);
                }
                finally
                {
                    // 恢复按钮状态
                    Dispatcher.Invoke(() =>
                    {
                        actionBtn.IsEnabled = true;
                        actionBtn.Content = btnText;
                        progressBar.Visibility = Visibility.Collapsed;
                    });
                }
            };

            // 将按钮和进度条放入一个垂直面板
            var btnContainer = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(0, 0, 8, 0)
            };
            btnContainer.Children.Add(actionBtn);
            btnContainer.Children.Add(progressBar);
            btnPanel.Children.Add(btnContainer);

            // 下载控制按钮（暂停/继续/取消）
            if (module.IsDownloading)
            {
                // 使用已声明的 isPaused 变量
                
                // 暂停/继续按钮
                var pauseResumeBtn = new Button
                {
                    Content = isPaused ? T("Action_Resume", "") : T("Action_Paused"),
                    Style = isPaused ? (Style)FindResource("SuccessButton") : (Style)FindResource("ActionButton"),
                    Width = 60,
                    Height = 32,
                    Margin = new Thickness(0, 0, 8, 0),
                    ToolTip = isPaused ? "Resume" : "Pause"
                };
                pauseResumeBtn.Click += (s, e) =>
                {
                    if (isPaused)
                    {
                        _downloadManager.ResumeDownload(module.UniqueId);
                    }
                    else
                    {
                        _downloadManager.PauseDownload(module.UniqueId);
                    }
                    UpdateModuleCardUI(module);
                };
                btnPanel.Children.Add(pauseResumeBtn);

                // 取消按钮
                var cancelBtn = new Button
                {
                    Content = T("Action_Cancel"),
                    Style = (Style)FindResource("ActionButton"),
                    Width = 60,
                    Height = 32,
                    Foreground = new SolidColorBrush(Colors.Red),
                    ToolTip = "Cancel"
                };
                cancelBtn.Click += (s, e) =>
                {
                    CancelDownload(module);
                };
                btnPanel.Children.Add(cancelBtn);
            }

            // 停用/启用按钮
            if (module.IsInstalled)
            {
                if (module.IsDisabled)
                {
                    var enableBtn = new Button
                    {
                        Content = T("Action_Enable"),
                        Style = (Style)FindResource("SuccessButton"),
                        Width = 60,
                        Height = 32,
                        ToolTip = "Enable"
                    };
                    enableBtn.Click += async (s, e) => await ToggleModuleStatusWithConflictCheck(module, false);
                    btnPanel.Children.Add(enableBtn);
                }
                else
                {
                    var disableBtn = new Button
                    {
                        Content = T("Action_Disable"),
                        Style = (Style)FindResource("ActionButton"),
                        Width = 60,
                        Height = 32,
                        Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFA500")),
                        BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#66FFA500")),
                        ToolTip = "Disable"
                    };
                    disableBtn.Click += async (s, e) => await ToggleModuleStatusWithConflictCheck(module, true);
                    btnPanel.Children.Add(disableBtn);
                }
            }

            return btnPanel;
        }

        private async System.Threading.Tasks.Task HandleModuleAction(PatchModule module)
        {
            await HandleModuleActionWithProgress(module, null);
        }

        private async System.Threading.Tasks.Task HandleModuleActionWithProgress(PatchModule module, IProgress<double> progress)
        {
            if (string.IsNullOrEmpty(_config.GamePath))
            {
                MessageBox.Show(T("Msg_SetGameDir"), T("Msg_InfoTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                SettingsPanelCanvas.Visibility = Visibility.Visible;
                SettingsPanelCanvas.IsHitTestVisible = true;
                return;
            }

            var missingDeps = module.Dependencies
                .Where(d => !_modules.Any(m => m.Id == d && m.IsInstalled))
                .ToList();

            if (missingDeps.Any())
            {
                var result = MessageBox.Show(
                    T("Msg_MissingDeps", string.Join(", ", missingDeps)),
                    T("Msg_InfoTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    foreach (var dep in missingDeps)
                    {
                        var depModule = _modules.FirstOrDefault(m => m.Id == dep);
                        if (depModule != null)
                            await DownloadModule(depModule);
                    }
                }
                else return;
            }

            if (progress != null)
                await DownloadModule(module, progress);
            else
                await DownloadModule(module);
        }

        private async System.Threading.Tasks.Task DownloadModule(PatchModule module)
        {
            await DownloadModule(module, null);
        }

        private async System.Threading.Tasks.Task DownloadModule(PatchModule module, IProgress<double> progress)
        {
            module.IsDownloading = true;
            UpdateModuleCardUI(module);

            try
            {
                // 1. 首先禁用同 PATCH ID 的其他已启用变体
                var otherEnabledVariants = _modules
                    .Where(m => m.Id == module.Id && m.UniqueId != module.UniqueId && m.IsInstalled && !m.IsDisabled)
                    .ToList();

                foreach (var other in otherEnabledVariants)
                {
                    await ToggleModuleStatusInternal(other, true);
                    other.IsDisabled = true;
                    UpdateModuleCardUI(other);
                }

                // 2. 使用下载管理器开始下载
                await _downloadManager.StartDownloadAsync(module, _config.GamePath);
            }
            catch (OperationCanceledException)
            {
                // 用户取消下载
                module.IsDownloading = false;
                UpdateModuleCardUI(module);
            }
            catch (Exception ex)
            {
                StatusText.Text = T("Status_DownloadFailed", ex.Message);
                MessageBox.Show(T("Msg_DownloadFailed", ex.Message), T("Msg_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                module.IsDownloading = false;
                UpdateModuleCardUI(module);
            }
        }

        /// <summary>
        /// 取消/暂停下载
        /// </summary>
        private void CancelDownload(PatchModule module)
        {
            _downloadManager.CancelDownload(module.UniqueId);
            module.IsDownloading = false;
            UpdateModuleCardUI(module);
        }

        /// <summary>
        /// 刷新所有卡片（用于下载管理器回调）
        /// </summary>
        private void RefreshAllCards()
        {
            // 更新正在下载的模块
            foreach (var module in _modules.Where(m => m.IsDownloading))
            {
                UpdateModuleCardUI(module);
            }
        }

        /// <summary>
        /// 刷新指定模块的卡片（下载完成后调用）
        /// </summary>
        private void RefreshModuleCard(PatchModule module)
        {
            UpdateModuleCardUI(module);
        }

        // 快速刷新 - 只扫描本地，不访问网络
        private async System.Threading.Tasks.Task RefreshLocalOnlyAsync()
        {
            StatusText.Text = T("Status_Scanning");

            try
            {
                // 只扫描本地，不访问网络
                var localPatches = new List<LocalPatchInfo>();
                if (!string.IsNullOrEmpty(_config.GamePath) && Directory.Exists(_config.GamePath))
                {
                    localPatches = LocalPatchScanner.ScanPatches(_config.GamePath);
                }

                // 更新现有模块的本地状态
                foreach (var module in _modules)
                {
                    var localFile = FindMatchingLocalPatch(localPatches, module);

                    if (localFile != null)
                    {
                        // 无论本地文件是什么状态，ActualFilename 始终使用标准启用格式
                        // 因为启用时的文件名统一为 patch-X.mpq
                        module.ActualFilename = module.ActiveFilename;
                        module.IsDisabled = localFile.IsDisabled;
                        module.FileSizeMB = localFile.FileSize / (1024.0 * 1024.0);
                        module.LastModified = localFile.LastModified;

                        if (string.IsNullOrEmpty(module.LocalVersion) || module.LocalVersion == "未知")
                        {
                            module.LocalVersion = localFile.Version;
                        }
                    }
                    else
                    {
                        module.ActualFilename = null;
                        module.LocalVersion = null;
                        module.IsDisabled = false;
                    }
                }

                // 保存配置
                _config.Save();

                // 重新渲染
                await LoadModulesAsync();
            }
            catch (Exception ex)
            {
                StatusText.Text = T("Status_RefreshFailed", ex.Message);
            }
        }

        /// <summary>
        /// 切换模块状态，带有冲突检查和依赖管理
        /// - 启用时：自动启用所有依赖的补丁（级联启用）
        /// - 停用时：自动停用所有依赖它的补丁（级联停用）
        /// 注意：此方法不访问网络，只操作本地文件
        /// </summary>
        private async Task ToggleModuleStatusWithConflictCheck(PatchModule module, bool disable)
        {
            if (module.IsDisabled == disable) return;

            if (!disable)
            {
                // ===== 启用模式 =====
                // 1. 先检查所有依赖是否已安装
                var missingDeps = GetMissingDependencies(module);
                if (missingDeps.Any())
                {
                    var depNames = string.Join(", ", missingDeps);
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(
                            T("Msg_CannotEnable", module.Id, depNames),
                            T("Msg_InfoTitle"),
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    });
                    return;
                }

                // 2. 先停用同ID的其他变体
                var otherVariants = _modules
                    .Where(m => m.Id == module.Id && m.UniqueId != module.UniqueId && m.IsInstalled && !m.IsDisabled)
                    .ToList();

                foreach (var other in otherVariants)
                {
                    await ToggleModuleStatusInternal(other, true);
                }

                // 3. 启用当前模块
                await ToggleModuleStatusInternal(module, false);
                
                // 4. 级联启用所有依赖
                await EnableDependenciesRecursive(module);
                
                // 更新 UI
                UpdateModuleCardUI(module);
                foreach (var other in otherVariants)
                {
                    UpdateModuleCardUI(other);
                }
            }
            else
            {
                // ===== 停用模式 =====
                // 1. 先停用所有依赖它的补丁（级联停用）
                await DisableDependentsRecursive(module);
                
                // 2. 停用当前模块
                await ToggleModuleStatusInternal(module, true);
                
                // 更新 UI
                UpdateModuleCardUI(module);
            }
        }

        /// <summary>
        /// 获取缺失的依赖补丁列表
        /// </summary>
        private List<string> GetMissingDependencies(PatchModule module)
        {
            var missing = new List<string>();
            
            foreach (var depId in module.Dependencies)
            {
                // 检查是否有任何变体已安装
                var hasInstalledVariant = _modules.Any(m => m.Id == depId && m.IsInstalled);
                if (!hasInstalledVariant)
                {
                    missing.Add(depId);
                }
            }
            
            return missing.Distinct().ToList();
        }

        /// <summary>
        /// 递归启用所有依赖的补丁（假设依赖已安装，只处理被禁用的，防止循环依赖）
        /// </summary>
        private async Task EnableDependenciesRecursive(PatchModule module, HashSet<string> processed = null)
        {
            processed ??= new HashSet<string>();
            
            // 防止循环
            if (processed.Contains(module.UniqueId))
                return;
            processed.Add(module.UniqueId);
            
            foreach (var depId in module.Dependencies)
            {
                // 找到已安装的依赖模块（任意变体，优先找未禁用的）
                var depModule = _modules.FirstOrDefault(m => m.Id == depId && m.IsInstalled && !m.IsDisabled)
                    ?? _modules.FirstOrDefault(m => m.Id == depId && m.IsInstalled);
                
                // 如果找不到或依赖已启用，跳过
                if (depModule == null || !depModule.IsDisabled) continue;
                
                // 停用同ID的其他变体
                var otherVariants = _modules
                    .Where(m => m.Id == depId && m.UniqueId != depModule.UniqueId && m.IsInstalled && !m.IsDisabled)
                    .ToList();
                foreach (var other in otherVariants)
                {
                    await ToggleModuleStatusInternal(other, true);
                    UpdateModuleCardUI(other);
                }
                
                // 启用依赖
                await ToggleModuleStatusInternal(depModule, false);
                UpdateModuleCardUI(depModule);
                
                // 递归启用依赖的依赖
                await EnableDependenciesRecursive(depModule, processed);
            }
        }

        /// <summary>
        /// 递归停用所有依赖它的补丁（防止循环依赖）
        /// </summary>
        private async Task DisableDependentsRecursive(PatchModule module, HashSet<string> processed = null)
        {
            processed ??= new HashSet<string>();
            
            // 防止循环
            if (processed.Contains(module.UniqueId))
                return;
            processed.Add(module.UniqueId);
            
            // 找到所有依赖此模块的已启用补丁
            var dependents = _modules
                .Where(m => m.IsInstalled && !m.IsDisabled && m.Dependencies.Contains(module.Id))
                .ToList();
            
            foreach (var dependent in dependents)
            {
                // 递归停用依赖它的补丁
                await DisableDependentsRecursive(dependent, processed);
                
                // 停用它自己
                await ToggleModuleStatusInternal(dependent, true);
                UpdateModuleCardUI(dependent);
            }
        }

        /// <summary>
        /// 只更新单个模块卡片的 UI，不重新加载所有模块
        /// </summary>
        private void UpdateModuleCardUI(PatchModule module)
        {
            // 找到该模块对应的卡片并更新
            // 由于 WPF 的数据绑定是引用绑定，直接更新 module 的属性后
            // 需要刷新卡片显示
            Dispatcher.Invoke(() =>
            {
                // 重新渲染该模块的卡片
                // 找到包含该模块的面板
                Panel parentPanel = module.Category switch
                {
                    "Core" => CorePanel,
                    "Optional" => OptionalPanel,
                    "Audio" => AudioPanel,
                    "Ultra" => UltraPanel,
                    _ => OptionalPanel
                };

                // 查找并替换该模块的卡片
                for (int i = 0; i < parentPanel.Children.Count; i++)
                {
                    if (parentPanel.Children[i] is Border border && border.Tag == module)
                    {
                        // 创建新的卡片替换旧的
                        var newCard = CreateModuleCard(module);
                        parentPanel.Children.RemoveAt(i);
                        parentPanel.Children.Insert(i, newCard);
                        break;
                    }
                }


            });
        }

        /// <summary>
        /// 内部方法：切换模块状态（不刷新UI）
        /// 启用时统一使用 patch-X.mpq（不带变体后缀），禁用时使用 _patch-X-变体名称.mpq（带变体后缀）
        /// </summary>
        private async Task ToggleModuleStatusInternal(PatchModule module, bool disable)
        {
            await Task.Run(() =>
            {
                try
                {
                    var dataPath = System.IO.Path.Combine(_config.GamePath, "Data");

                    // 确定源文件名和目标文件名
                    string sourceFileName;
                    string targetFileName;

                    if (disable)
                    {
                        // 禁用：从 patch-X.mpq 重命名为 _patch-X-变体名称.mpq
                        sourceFileName = module.ActiveFilename; // patch-X.mpq
                        targetFileName = module.DisabledFilename; // _patch-X-变体名称.mpq
                    }
                    else
                    {
                        // 启用：从 _patch-X-变体名称.mpq 重命名为 patch-X.mpq
                        sourceFileName = module.DisabledFilename; // _patch-X-变体名称.mpq
                        targetFileName = module.ActiveFilename; // patch-X.mpq
                        
                        // 如果文件不存在，尝试查找其他可能的禁用文件名（兼容其他变体文件）
                        var sourcePath = System.IO.Path.Combine(dataPath, sourceFileName);
                        if (!File.Exists(sourcePath))
                        {
                            // 尝试查找任何以 _patch-X 开头的禁用文件
                            var disabledFiles = Directory.GetFiles(dataPath, $"_{module.Id.Replace("PATCH-", "patch-")}*.mpq")
                                .Where(f => Path.GetFileName(f).StartsWith("_"))
                                .ToList();
                            
                            if (disabledFiles.Count > 0)
                            {
                                sourceFileName = Path.GetFileName(disabledFiles.First());
                            }
                        }
                    }

                    var sourceFile = System.IO.Path.Combine(dataPath, sourceFileName);
                    var targetFile = System.IO.Path.Combine(dataPath, targetFileName);

                    if (!File.Exists(sourceFile))
                    {
                        // 停用时，如果源文件不存在，可能是已经停用过了
                        // 检查目标文件是否已存在（已停用状态）
                        if (disable && File.Exists(targetFile))
                        {
                            module.IsDisabled = true;
                            module.ActualFilename = module.DisabledFilename.TrimStart('_');
                            return;
                        }
                        
                        // 启用时，如果禁用文件不存在，但启用文件存在，说明已经是启用状态
                        if (!disable && File.Exists(targetFile))
                        {
                            module.IsDisabled = false;
                            module.ActualFilename = module.ActiveFilename;
                            return;
                        }
                        
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(T("Msg_FileNotFound", sourceFileName), T("Msg_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                        return;
                    }

                    if (File.Exists(targetFile))
                    {
                        File.Delete(targetFile);
                    }

                    File.Move(sourceFile, targetFile);

                    module.IsDisabled = disable;
                    // 根据状态设置 ActualFilename
                    if (disable)
                    {
                        // 禁用时：使用带下划线和变体后缀的文件名（不含前导下划线）
                        module.ActualFilename = module.DisabledFilename.TrimStart('_');
                    }
                    else
                    {
                        // 启用时：使用标准启用格式 patch-X.mpq
                        module.ActualFilename = module.ActiveFilename;
                    }

                    Dispatcher.Invoke(() =>
                    {
                        StatusText.Text = disable ? T("Status_ModuleDisabled", module.Id) : T("Status_ModuleEnabled", module.Id);
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(T("Msg_OperationFailed", ex.Message), T("Msg_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
            });
        }

        private async void RefreshBtn_Click(object sender, RoutedEventArgs e)
        {
            await LoadModulesAsync();
        }

        private void SettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            var showStoryboard = (Storyboard)FindResource("SettingsPanelShowAnimation");
            var hideStoryboard = (Storyboard)FindResource("SettingsPanelHideAnimation");
            
            if (SettingsPanelCanvas.Visibility == Visibility.Collapsed)
            {
                // 显示面板
                SettingsPanelCanvas.Visibility = Visibility.Visible;
                SettingsPanelCanvas.IsHitTestVisible = true;
                showStoryboard.Begin();
                
                // 检查 4GB 补丁状态
                Check4GBPatchStatus();
            }
            else
            {
                // 隐藏面板
                SettingsPanelCanvas.IsHitTestVisible = false;
                hideStoryboard.Begin();
                // 动画完成后隐藏 Canvas
                hideStoryboard.Completed += (s, args) =>
                {
                    SettingsPanelCanvas.Visibility = Visibility.Collapsed;
                };
            }
        }

        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string cultureName)
            {
                if (_config.Language != cultureName)
                {
                    _config.Language = cultureName;
                    _config.SaveGlobal();
                    Instance.ChangeLanguage(cultureName);
                    
                    // 重新加载界面以应用新语言
                    _ = LoadModulesAsync();
                }
            }
        }

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = T("Settings_GamePath"),
                Filter = "WoW.exe|WoW.exe",
                CheckFileExists = true,
                CheckPathExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                var exePath = dialog.FileName;
                var fileName = Path.GetFileName(exePath);
                
                // 验证选择的是 WoW.exe
                if (!fileName.Equals("WoW.exe", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(T("Msg_SelectWowExe"), T("Msg_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                
                // 从 WoW.exe 路径提取游戏目录
                var newPath = Path.GetDirectoryName(exePath);
                
                // 更新游戏路径
                _config.GamePath = newPath;
                
                // 重新加载游戏配置（从新路径）
                _config.ReloadGameConfig();
                
                PathTextBox.Text = newPath;
                _config.SaveGlobal(); // 只保存全局配置（路径）
                
                // 重新加载模块以应用新路径
                _ = LoadModulesAsync();
            }
        }

        private void LaunchBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_config.GamePath))
            {
                MessageBox.Show(T("Msg_SetGameDir"));
                return;
            }

            var exePath = System.IO.Path.Combine(_config.GamePath, "WoW.exe");
            if (!File.Exists(exePath))
            {
                MessageBox.Show(T("Msg_GameNotFound"));
                return;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = _config.GamePath
                };
                Process.Start(startInfo);
                StatusText.Text = T("Status_GameStarted");
            }
            catch (Exception ex)
            {
                MessageBox.Show(T("Msg_LaunchFailed", ex.Message));
            }
        }

        private void OpenWebBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("https://projectreforged.github.io/downloads/index.html");
                StatusText.Text = T("Status_WebOpened");
            }
            catch (Exception ex)
            {
                MessageBox.Show(T("Msg_OpenWebFailed", ex.Message));
            }
        }

        private void ClearCacheBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_config.GamePath) || !Directory.Exists(_config.GamePath))
            {
                MessageBox.Show(T("Msg_SetGameDir"), T("Msg_InfoTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                var dataPath = Path.Combine(_config.GamePath, "Data");
                if (!Directory.Exists(dataPath))
                {
                    MessageBox.Show(T("Msg_DataDirNotExist"), T("Msg_InfoTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // 查找所有未完成的下载文件
                var tempFiles = Directory.GetFiles(dataPath, "*.downloading");
                if (tempFiles.Length == 0)
                {
                    MessageBox.Show(T("Msg_NoTempFiles"), T("Msg_InfoTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var totalSizeMB = tempFiles.Sum(f => new FileInfo(f).Length) / (1024.0 * 1024.0);
                var result = MessageBox.Show(T("Msg_ConfirmClearCache", tempFiles.Length, totalSizeMB), 
                    T("Msg_ConfirmTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    int deletedCount = 0;
                    long deletedSize = 0;

                    foreach (var file in tempFiles)
                    {
                        try
                        {
                            var fileInfo = new FileInfo(file);
                            deletedSize += fileInfo.Length;
                            File.Delete(file);
                            deletedCount++;
                        }
                        catch (Exception)
                        {
                            // 忽略删除失败的文件
                        }
                    }

                    var sizeText = deletedSize > 1024 * 1024 
                        ? $"{deletedSize / (1024.0 * 1024.0):F1} MB" 
                        : $"{deletedSize / 1024.0:F0} KB";
                    
                    StatusText.Text = T("Status_CacheCleared", deletedCount, sizeText);
                    MessageBox.Show(T("Msg_CacheCleared", deletedCount, sizeText), 
                        T("Msg_InfoTitle"), MessageBoxButton.OK, MessageBoxImage.Information);

                    // 刷新 UI
                    _ = LoadModulesAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(T("Msg_ClearFailed", ex.Message), T("Msg_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region 4GB 补丁

        /// <summary>
        /// 检查 4GB 补丁的当前状态（带重试机制）
        /// </summary>
        private void Check4GBPatchStatus()
        {
            // 检查游戏路径是否已设置
            if (string.IsNullOrEmpty(_config.GamePath) || !Directory.Exists(_config.GamePath))
            {
                Patch4GBToggle.IsEnabled = false;
                Patch4GBToggle.IsChecked = false;
                return;
            }
            
            var wowExePath = Path.Combine(_config.GamePath, "WoW.exe");
            if (!File.Exists(wowExePath))
            {
                Patch4GBToggle.IsEnabled = false;
                return;
            }

            // 尝试读取文件，带重试机制
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    // 使用 ReadWrite 共享模式，允许其他进程访问
                    using (var fs = new FileStream(wowExePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        fs.Position = 294;
                        int value = fs.ReadByte();
                        Patch4GBToggle.IsChecked = (value == 47);  // 47=开启, 15=关闭
                        Patch4GBToggle.IsEnabled = true;
                        return;
                    }
                }
                catch (IOException)
                {
                    // 文件被锁定，等待后重试
                    if (attempt < 2)
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                }
                catch (Exception)
                {
                    Patch4GBToggle.IsEnabled = false;
                    return;
                }
            }

            // 重试失败，禁用开关
            Patch4GBToggle.IsEnabled = false;
        }

        /// <summary>
        /// 修改文件指定偏移位置的值（带重试机制）
        /// </summary>
        private bool PatchFile(string filePath, int offset, int[] bytes)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using (var writer = new BinaryWriter(File.Open(filePath, FileMode.Open, FileAccess.Write, FileShare.Read)))
                    {
                        for (int i = 0; i < bytes.Length; i++)
                        {
                            writer.BaseStream.Position = offset + i;
                            writer.BaseStream.WriteByte((byte)bytes[i]);
                        }
                    }
                    return true;
                }
                catch (IOException)
                {
                    if (attempt < 2)
                    {
                        System.Threading.Thread.Sleep(100);
                    }
                }
            }
            return false;
        }

        private void Patch4GBToggle_Checked(object sender, RoutedEventArgs e)
        {
            var wowExePath = Path.Combine(_config.GamePath, "WoW.exe");
            if (!File.Exists(wowExePath))
            {
                MessageBox.Show(T("Msg_GameNotFound"), T("Msg_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                Patch4GBToggle.IsChecked = false;
                return;
            }

            if (PatchFile(wowExePath, 294, new int[] { 47 }))  // 47=开启
            {
                StatusText.Text = "4GB 补丁已开启";
            }
            else
            {
                MessageBox.Show("无法修改 WoW.exe，文件被其他进程占用。\n请关闭游戏或其他可能占用该文件的程序后重试。", 
                    T("Msg_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                Patch4GBToggle.IsChecked = false;
            }
        }

        private void Patch4GBToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            var wowExePath = Path.Combine(_config.GamePath, "WoW.exe");
            if (!File.Exists(wowExePath))
            {
                MessageBox.Show(T("Msg_GameNotFound"), T("Msg_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Warning);
                Patch4GBToggle.IsChecked = true;
                return;
                }

            if (PatchFile(wowExePath, 294, new int[] { 15 }))  // 15=关闭
            {
                StatusText.Text = "4GB 补丁已关闭";
            }
            else
            {
                MessageBox.Show("无法修改 WoW.exe，文件被其他进程占用。\n请关闭游戏或其他可能占用该文件的程序后重试。", 
                    T("Msg_ErrorTitle"), MessageBoxButton.OK, MessageBoxImage.Error);
                Patch4GBToggle.IsChecked = true;
            }
        }

        #endregion
    }
}
