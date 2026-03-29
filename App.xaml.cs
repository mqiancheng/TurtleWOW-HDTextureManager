using System.Windows;
using HDTextureManager.Services;

namespace HDTextureManager
{
    public partial class App : Application
    {
        protected override void OnExit(ExitEventArgs e)
        {
            // 应用退出时清理 StormLib 临时文件
            StormLibLoader.Cleanup();
            base.OnExit(e);
        }
    }
}
