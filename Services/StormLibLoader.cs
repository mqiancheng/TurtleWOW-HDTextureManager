using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HDTextureManager.Services
{
    /// <summary>
    /// StormLib.dll 加载器
    /// 从嵌入式资源提取 StormLib.dll 到临时目录并加载
    /// 应用退出时自动清理临时文件
    /// </summary>
    public static class StormLibLoader
    {
        private static bool _initialized = false;
        private static string _tempDllPath = null;

        /// <summary>
        /// 检查 StormLib.dll 是否已准备好
        /// </summary>
        public static bool IsReady => _initialized || Initialize();

        /// <summary>
        /// 初始化 StormLib.dll
        /// 从嵌入式资源提取 DLL 并加载
        /// </summary>
        public static bool Initialize()
        {
            if (_initialized)
                return true;

            try
            {
                // 1. 首先检查是否已有 StormLib.dll 在内存中
                var moduleHandle = GetModuleHandle("StormLib.dll");
                if (moduleHandle != IntPtr.Zero)
                {
                    _initialized = true;
                    return true;
                }

                // 2. 从嵌入式资源提取 DLL 到临时目录
                var extractedPath = ExtractDllFromResource();
                if (string.IsNullOrEmpty(extractedPath))
                {
                    return false;
                }

                // 3. 加载 DLL
                var handle = LoadLibrary(extractedPath);
                if (handle != IntPtr.Zero)
                {
                    _tempDllPath = extractedPath;
                    _initialized = true;
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 从嵌入式资源提取 DLL 到临时目录
        /// </summary>
        private static string ExtractDllFromResource()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                
                // 获取资源名称
                string resourceName = GetResourceName();
                if (string.IsNullOrEmpty(resourceName))
                {
                    return null;
                }

                // 创建临时目录
                var tempDir = Path.Combine(Path.GetTempPath(), "TurtleWOW_HDTM");
                Directory.CreateDirectory(tempDir);

                // 临时文件路径
                var dllPath = Path.Combine(tempDir, "StormLib.dll");

                // 从资源提取
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        return null;
                    }

                    // 检查文件是否已存在且相同
                    if (File.Exists(dllPath))
                    {
                        var existingSize = new FileInfo(dllPath).Length;
                        if (existingSize == stream.Length)
                        {
                            return dllPath;
                        }
                        // 文件不同，删除旧的
                        try { File.Delete(dllPath); } catch { }
                    }

                    // 写入文件
                    using (var fileStream = new FileStream(dllPath, FileMode.Create, FileAccess.Write))
                    {
                        stream.CopyTo(fileStream);
                    }

                    return dllPath;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// 获取资源名称
        /// </summary>
        private static string GetResourceName()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames();

            // 检查是否为 64 位进程
            bool is64Bit = IntPtr.Size == 8;

            // 优先查找特定架构的资源
            var targetResource = is64Bit
                ? "StormLib_x64.dll"
                : "StormLib_x86.dll";

            foreach (var name in resourceNames)
            {
                if (name.Equals(targetResource, StringComparison.OrdinalIgnoreCase))
                    return name;
            }

            // 尝试其他 StormLib 相关资源
            foreach (var name in resourceNames)
            {
                if (name.IndexOf("StormLib", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    return name;
            }

            return null;
        }

        /// <summary>
        /// 清理临时 DLL 文件
        /// 应在应用退出时调用
        /// </summary>
        public static void Cleanup()
        {
            try
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "TurtleWOW_HDTM");
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
            catch (Exception)
            }
        }

        #region P/Invoke

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        #endregion
    }
}
