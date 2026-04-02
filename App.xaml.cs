using System;
using System.Management;
using System.Windows;
using HDTextureManager.Services;

namespace HDTextureManager
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                // 1) 先加载用户配置并设置本地化（确保提示使用用户选择的语言）
                var cfg = Config.Load();
                var langToUse = string.IsNullOrEmpty(cfg.Language) ? LocalizationService.GetDefaultLanguage() : cfg.Language;
                LocalizationService.Instance.ChangeLanguage(langToUse);
                System.Diagnostics.Debug.WriteLine("当前语言: " + LocalizationService.Instance.CurrentLanguage);

                // 2) 再做显卡检测与本地化提示
                var (hasAmd, hasNvidia) = GetGpuVendors();

                if (hasAmd && hasNvidia)
                {
                    MessageBox.Show(
                        LocalizationService.Instance.GetString("Msg_AmdNvidiaMixed"),
                        LocalizationService.Instance["Msg_InfoTitle"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                else if (hasAmd)
                {
                    MessageBox.Show(
                        LocalizationService.Instance.GetString("Msg_AmdOnly"),
                        LocalizationService.Instance["Msg_InfoTitle"],
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GPU 检测失败: " + ex);
            }

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            StormLibLoader.Cleanup();
            base.OnExit(e);
        }

        // 返回 (hasAmd, hasNvidia)
        private (bool, bool) GetGpuVendors()
        {
            bool foundAmd = false;
            bool foundNvidia = false;

            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT AdapterCompatibility, PNPDeviceID, Name FROM Win32_VideoController"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        var adapter = (mo["AdapterCompatibility"] as string) ?? string.Empty;
                        var pnp = (mo["PNPDeviceID"] as string) ?? string.Empty;
                        var name = (mo["Name"] as string) ?? string.Empty;

                        var combined = (adapter + "|" + pnp + "|" + name).ToLowerInvariant();

                        if (!foundNvidia && (combined.Contains("nvidia") || combined.Contains("ven_10de")))
                            foundNvidia = true;

                        if (!foundAmd && (combined.Contains("amd") || combined.Contains("advanced micro devices") || combined.Contains("ven_1002")))
                            foundAmd = true;

                        // 提前结束（若已同时找到两者）
                        if (foundAmd && foundNvidia)
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("GetGpuVendors 异常: " + ex);
            }

            return (foundAmd, foundNvidia);
        }
    }
}
