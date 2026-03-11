using Microsoft.Win32;
using System;
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

                // 检查路径是否匹配当前程序
                var currentPath = GetExecutablePath();
                return value.ToString() == currentPath;
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
                    key.SetValue(AppName, exePath);
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
        /// 获取当前可执行文件路径
        /// </summary>
        private static string GetExecutablePath()
        {
            // .NET 8: 单文件发布场景优先使用进程路径
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                return processPath;
            }

            // 备选方案
            return Path.Combine(AppContext.BaseDirectory, "eyesharp.exe");
        }
    }
}
