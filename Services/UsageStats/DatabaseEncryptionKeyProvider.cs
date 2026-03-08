using System;
using System.Security.Cryptography;
using System.Text;

namespace eyesharp.Services.UsageStats
{
    /// <summary>
    /// 数据库加密密钥提供器
    /// 从机器特征派生加密密钥
    /// </summary>
    public static class DatabaseEncryptionKeyProvider
    {
        private const string KeySalt = "EyeSharpSalt_v1";

        /// <summary>
        /// 获取数据库加密密钥（256位，Hex编码）
        /// </summary>
        public static string GetEncryptionKey()
        {
            try
            {
                // 组合多个机器特征
                var machineGuid = GetMachineGuid();
                var systemInfo = GetSystemInfo();

                // 组合特征字符串
                var combined = $"{machineGuid}:{systemInfo}:{KeySalt}";

                // 使用SHA-256哈希生成256位密钥
                using var sha256 = SHA256.Create();
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(combined));
                return Convert.ToHexString(hash);
            }
            catch (Exception ex)
            {
                // 如果获取机器特征失败，使用备用方案
                var fallbackKey = GetFallbackKey();
                System.Diagnostics.Debug.WriteLine($"[DatabaseEncryption] 使用备用密钥: {ex.Message}");
                return fallbackKey;
            }
        }

        /// <summary>
        /// 获取Windows MachineGuid
        /// </summary>
        private static string GetMachineGuid()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                    @"SOFTWARE\Microsoft\Cryptography");
                if (key != null)
                {
                    var guid = key.GetValue("MachineGuid")?.ToString();
                    if (!string.IsNullOrEmpty(guid))
                    {
                        return guid;
                    }
                }
            }
            catch
            {
                // 忽略异常，返回默认值
            }

            return "UnknownGuid";
        }

        /// <summary>
        /// 获取系统信息组合
        /// </summary>
        private static string GetSystemInfo()
        {
            try
            {
                var processorCount = Environment.ProcessorCount;
                var osVersion = Environment.OSVersion.Version.ToString();
                var machineName = Environment.MachineName;
                var userName = Environment.UserName;

                return $"{processorCount}:{osVersion}:{machineName}:{userName}";
            }
            catch
            {
                return "UnknownSystem";
            }
        }

        /// <summary>
        /// 备用密钥生成（基于用户目录）
        /// </summary>
        private static string GetFallbackKey()
        {
            var userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var fallback = $"{userPath}:{KeySalt}:Fallback";

            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(fallback));
            return Convert.ToHexString(hash);
        }
    }
}
