using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace eyesharp.Helpers
{
    /// <summary>
    /// 开机自启动辅助类
    /// </summary>
    public static class AutoStartHelper
    {
        private const string RegistryKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        private const string AppName = "EyeSharp";

        /// <summary>
        /// 检查是否已设置为开机自启动
        /// </summary>
        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
                if (key == null) return false;

                var value = key.GetValue(AppName);
                if (value == null) return false;

                // 检查路径是否匹配当前程序（去除可能的引号）
                var currentPath = GetExecutablePath();
                var registryPath = value.ToString()?.Trim('"') ?? string.Empty;
                return string.Equals(registryPath, currentPath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 设置开机自启动
        /// </summary>
        public static bool SetAutoStart(bool enable)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
                if (key == null) return false;

                if (enable)
                {
                    var exePath = GetExecutablePath();
                    // 路径包含空格时必须用引号包裹
                    var registryValue = exePath.Contains(" ") ? $"\"{exePath}\"" : exePath;
                    key.SetValue(AppName, registryValue);
                }
                else
                {
                    key.DeleteValue(AppName, false);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// 获取当前可执行文件路径（用于日志诊断）
        /// </summary>
        public static string GetCurrentExecutablePath()
        {
            return GetExecutablePath();
        }

        /// <summary>
        /// 获取当前可执行文件路径（确保返回 .exe 路径）
        /// </summary>
        private static string GetExecutablePath()
        {
            // 方案1：使用进程主模块路径（最可靠，适用于单文件发布）
            try
            {
                using var process = Process.GetCurrentProcess();
                var mainModulePath = process.MainModule?.FileName;
                if (!string.IsNullOrWhiteSpace(mainModulePath) &&
                    mainModulePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return mainModulePath;
                }
            }
            catch
            {
                // 忽略异常，继续尝试其他方案
            }

            // 方案2：Environment.ProcessPath（可能返回 DLL 路径，需要修正）
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                // 单文件发布时，ProcessPath 可能指向临时目录中的 DLL
                // 需要将 .dll 替换为 .exe
                if (processPath.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    var exePath = processPath.Substring(0, processPath.Length - 4) + ".exe";
                    if (File.Exists(exePath))
                    {
                        return exePath;
                    }
                }
                return processPath;
            }

            // 方案3：备选方案
            return Path.Combine(AppContext.BaseDirectory, "eyesharp.exe");
        }
    }
}
