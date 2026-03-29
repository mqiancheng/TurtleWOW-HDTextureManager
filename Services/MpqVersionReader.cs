using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace HDTextureManager.Services
{
    /// <summary>
    /// MPQ 文件版本读取器
    /// 使用 StormLib 读取 MPQ 文件中的 Patch.toc 获取版本信息
    /// </summary>
    public static class MpqVersionReader
    {
        #region StormLib P/Invoke Declarations

        private const string StormLibDll = "StormLib.dll";

        // MPQ 打开标志
        private const uint MPQ_OPEN_READ_ONLY = 0x00000100;
        private const uint MPQ_OPEN_NO_LISTFILE = 0x00010000; // 不加载 (listfile)

        // 文件打开标志
        private const uint SFILE_OPEN_FROM_MPQ = 0x00000000;

        [DllImport(StormLibDll, CallingConvention = CallingConvention.Winapi, SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool SFileOpenArchive(
            string szMpqName,
            uint dwPriority,
            uint dwFlags,
            out IntPtr phMpq);

        [DllImport(StormLibDll, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        private static extern bool SFileCloseArchive(IntPtr hMpq);

        [DllImport(StormLibDll, CallingConvention = CallingConvention.Winapi, SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern bool SFileOpenFileEx(
            IntPtr hMpq,
            string szFileName,
            uint dwSearchScope,
            out IntPtr phFile);

        [DllImport(StormLibDll, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        private static extern bool SFileCloseFile(IntPtr hFile);

        [DllImport(StormLibDll, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        private static extern bool SFileReadFile(
            IntPtr hFile,
            byte[] lpBuffer,
            uint dwToRead,
            out uint pdwRead,
            IntPtr lpOverlapped);

        [DllImport(StormLibDll, CallingConvention = CallingConvention.Winapi, SetLastError = true)]
        private static extern uint SFileGetFileSize(IntPtr hFile, out uint pdwFileSizeHigh);

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        #endregion

        /// <summary>
        /// 确保 StormLib.dll 已加载
        /// </summary>
        private static bool EnsureStormLibLoaded()
        {
            if (_stormLibLoaded)
                return true;

            // 使用 StormLibLoader 初始化（依赖 Costura.Fody 自动加载）
            if (StormLibLoader.Initialize())
            {
                _stormLibLoaded = true;
                return true;
            }

            // 备用：尝试加载系统路径中的 StormLib
            var systemHandle = LoadLibrary(StormLibDll);
            if (systemHandle != IntPtr.Zero)
            {
                _stormLibLoaded = true;
                return true;
            }

            return false;
        }

        private static bool _stormLibLoaded = false;

        /// <summary>
        /// 检查 StormLib 是否可用
        /// </summary>
        public static bool IsAvailable => EnsureStormLibLoaded();

        /// <summary>
        /// 从 MPQ 文件中读取版本号
        /// </summary>
        /// <param name="mpqFilePath">MPQ 文件路径</param>
        /// <returns>版本号（如 "v5.0.0"），失败返回 null</returns>
        public static string ReadVersionFromMpq(string mpqFilePath)
        {
            if (!File.Exists(mpqFilePath))
            {
                return null;
            }

            // 确保 StormLib 已加载
            if (!EnsureStormLibLoaded())
            {
                return null;
            }

            // 检查文件是否可访问（带重试机制）
            bool fileAccessible = false;
            Exception lastException = null;
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    using (var fs = File.OpenRead(mpqFilePath))
                    {
                        fileAccessible = true;
                        break;
                    }
                }
                catch (IOException ex) when (i < 2)
                {
                    // 文件被占用，等待后重试
                    lastException = ex;
                    System.Threading.Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    break;
                }
            }
            
            if (!fileAccessible)
            {
                return null;
            }

            IntPtr hMpq = IntPtr.Zero;
            IntPtr hFile = IntPtr.Zero;

            try
            {
                // 尝试使用 Unicode 路径打开 MPQ 文件
                // 添加 MPQ_OPEN_NO_LISTFILE 标志以提高兼容性
                if (!SFileOpenArchive(mpqFilePath, 0, MPQ_OPEN_READ_ONLY | MPQ_OPEN_NO_LISTFILE, out hMpq))
                {
                    // 尝试不使用 NO_LISTFILE 标志
                    if (!SFileOpenArchive(mpqFilePath, 0, MPQ_OPEN_READ_ONLY, out hMpq))
                    {
                        return null;
                    }
                }

                // 尝试多种可能的路径读取 Patch.toc
                string[] possiblePaths = new[]
                {
                    "Patch.toc",
                    "patch.toc",
                    "PATCH.TOC",
                    "Interface\\AddOns\\Patch\\Patch.toc",
                    "Interface\\AddOns\\patch\\patch.toc",
                    "INTERFACE\\ADDONS\\PATCH\\PATCH.TOC",
                    "AddOns\\Patch\\Patch.toc",
                    "AddOns\\patch\\patch.toc",
                };

                string tocContent = null;
                string foundPath = null;

                foreach (var path in possiblePaths)
                {
                    tocContent = ReadFileFromMpq(hMpq, path, out hFile);
                    if (tocContent != null)
                    {
                        foundPath = path;
                        break;
                    }
                }

                if (tocContent == null)
                {
                    return null;
                }

                // 解析版本号
                var version = ExtractVersionFromToc(tocContent);

                return version;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (hFile != IntPtr.Zero)
                    SFileCloseFile(hFile);
                if (hMpq != IntPtr.Zero)
                    SFileCloseArchive(hMpq);
            }
        }

        /// <summary>
        /// 获取系统错误消息
        /// </summary>
        private static string GetSystemErrorMessage(int errorCode)
        {
            try
            {
                return new System.ComponentModel.Win32Exception(errorCode).Message;
            }
            catch
            {
                return $"未知错误 (0x{errorCode:X8})";
            }
        }

        /// <summary>
        /// 从 MPQ 中读取指定文件的内容
        /// </summary>
        private static string ReadFileFromMpq(IntPtr hMpq, string fileName, out IntPtr hFile)
        {
            hFile = IntPtr.Zero;

            try
            {
                // 打开文件
                if (!SFileOpenFileEx(hMpq, fileName, SFILE_OPEN_FROM_MPQ, out hFile))
                {
                    return null;
                }

                // 获取文件大小
                uint fileSize = SFileGetFileSize(hFile, out _);
                if (fileSize == 0 || fileSize > 1024 * 1024) // 限制最大 1MB
                {
                    SFileCloseFile(hFile);
                    hFile = IntPtr.Zero;
                    return null;
                }

                // 读取文件内容
                byte[] buffer = new byte[fileSize];
                if (!SFileReadFile(hFile, buffer, fileSize, out uint bytesRead, IntPtr.Zero))
                {
                    SFileCloseFile(hFile);
                    hFile = IntPtr.Zero;
                    return null;
                }

                // 转换为字符串（尝试 UTF-8 编码）
                return Encoding.UTF8.GetString(buffer, 0, (int)bytesRead);
            }
            catch
            {
                if (hFile != IntPtr.Zero)
                {
                    SFileCloseFile(hFile);
                    hFile = IntPtr.Zero;
                }
                return null;
            }
        }

        /// <summary>
        /// 从 TOC 内容中提取版本号
        /// </summary>
        private static string ExtractVersionFromToc(string tocContent)
        {
            if (string.IsNullOrEmpty(tocContent))
                return null;

            // 匹配 ## Version: x.x.x 格式
            var match = Regex.Match(tocContent, @"##\s*Version:\s*v?(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return "v" + match.Groups[1].Value;
            }

            // 尝试其他可能的格式
            match = Regex.Match(tocContent, @"Version[:\s]+v?(\d+\.\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return "v" + match.Groups[1].Value;
            }

            return null;
        }

        /// <summary>
        /// 从 MPQ 文件中读取变体信息
        /// </summary>
        /// <param name="mpqFilePath">MPQ 文件路径</param>
        /// <returns>变体名称（如 "Less Thick", "Performance", "Standard"），失败返回 null</returns>
        public static string ReadVariantFromMpq(string mpqFilePath)
        {
            if (!File.Exists(mpqFilePath))
            {
                return null;
            }

            // 确保 StormLib 已加载
            if (!EnsureStormLibLoaded())
            {
                return null;
            }

            // 尝试打开 MPQ 文件（带重试机制）
            IntPtr hMpq = IntPtr.Zero;
            bool opened = false;
            
            for (int i = 0; i < 3; i++)
            {
                if (SFileOpenArchive(mpqFilePath, 0, MPQ_OPEN_READ_ONLY | MPQ_OPEN_NO_LISTFILE, out hMpq))
                {
                    opened = true;
                    break;
                }
                
                // 尝试不使用 NO_LISTFILE 标志
                if (SFileOpenArchive(mpqFilePath, 0, MPQ_OPEN_READ_ONLY, out hMpq))
                {
                    opened = true;
                    break;
                }
                
                if (i < 2)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            
            if (!opened)
            {
                return null;
            }

            IntPtr hFile = IntPtr.Zero;
            
            try
            {

                // 尝试多种可能的路径读取 Patch.toc
                string[] possiblePaths = new[]
                {
                    "Patch.toc",
                    "patch.toc",
                    "PATCH.TOC",
                    "Interface\\AddOns\\Patch\\Patch.toc",
                    "Interface\\AddOns\\patch\\patch.toc",
                    "AddOns\\Patch\\Patch.toc",
                    "AddOns\\patch\\patch.toc",
                };

                string tocContent = null;

                foreach (var path in possiblePaths)
                {
                    tocContent = ReadFileFromMpq(hMpq, path, out hFile);
                    if (tocContent != null)
                        break;
                }

                if (tocContent == null)
                {
                    return null;
                }

                // 解析变体信息
                var variant = ExtractVariantFromToc(tocContent);

                return variant;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (hFile != IntPtr.Zero)
                    SFileCloseFile(hFile);
                if (hMpq != IntPtr.Zero)
                    SFileCloseArchive(hMpq);
            }
        }

        /// <summary>
        /// 从 TOC 内容中提取变体信息
        /// 解析格式: ## Title: Project Reforged - XXXX (Variant-name)
        /// 如果没有括号中的描述，则返回 "Standard"
        /// </summary>
        private static string ExtractVariantFromToc(string tocContent)
        {
            if (string.IsNullOrEmpty(tocContent))
                return "Standard";

            // 匹配 ## Title: ... (Variant-name) 格式
            // 例如: ## Title: Project Reforged - A Little Extra for Females (Less-thick)
            var match = Regex.Match(tocContent, @"##\s*Title:[^\(]*\(([^\)]+)\)", RegexOptions.IgnoreCase);
            
            if (match.Success)
            {
                var variantInParens = match.Groups[1].Value.Trim();
                
                // 标准化变体名称
                // 处理 "Less-thick", "Less-Thick", "less thick" 等变体
                if (variantInParens.IndexOf("Thicc", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    variantInParens.IndexOf("Thick", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "Less Thick";
                }
                else if (variantInParens.IndexOf("Performance", StringComparison.OrdinalIgnoreCase) >= 0 ||
                         variantInParens.Equals("Perf", StringComparison.OrdinalIgnoreCase))
                {
                    // "Perf" 是 Performance 的缩写
                    return "Performance";
                }
                else if (variantInParens.IndexOf("Regular", StringComparison.OrdinalIgnoreCase) >= 0 || 
                         variantInParens.IndexOf("Standard", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return "Standard";
                }
                else
                {
                    // 其他未识别的变体，返回原始值（首字母大写）
                    var result = char.ToUpperInvariant(variantInParens[0]) + variantInParens.Substring(1);
                    return result;
                }
            }

            // 如果没有括号中的描述，默认为 Standard
            // 检查是否包含 Title 字段来确认这是有效的 TOC
            if (Regex.IsMatch(tocContent, @"##\s*Title:", RegexOptions.IgnoreCase))
            {
                return "Standard";
            }

            // 无法确定，返回 null
            return null;
        }
    }
}
