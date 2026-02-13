using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using eyesharp.Models;

namespace eyesharp.Services
{
    /// <summary>
    /// 配置文件服务实现
    /// </summary>
    public class ConfigService : IConfigService
    {
        private const string ConfigFileName = "config.json";
        private const string BackupConfigFileName = "config.backup.json";
        private const int MaxBackupFiles = 5;

        private readonly string _configDirectory;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly ILogService _logService;

        /// <summary>
        /// 配置文件损坏事件
        /// </summary>
        public event EventHandler<ConfigCorruptedEventArgs>? ConfigCorrupted;

        public ConfigService(ILogService logService)
        {
            _configDirectory = AppDomain.CurrentDomain.BaseDirectory;
            _logService = logService;

            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
        }

        /// <summary>
        /// 获取配置文件完整路径
        /// </summary>
        public string GetConfigFilePath()
        {
            return Path.Combine(_configDirectory, ConfigFileName);
        }

        /// <summary>
        /// 检查配置文件是否存在
        /// </summary>
        public bool ConfigExists()
        {
            return File.Exists(GetConfigFilePath());
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        public async Task<AppConfig> LoadConfigAsync()
        {
            var configPath = GetConfigFilePath();

            if (!File.Exists(configPath))
            {
                _logService.Info("配置文件不存在，使用默认配置");
                return GetDefaultConfig();
            }

            try
            {
                var json = await File.ReadAllTextAsync(configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json, _jsonOptions);

                if (config == null)
                {
                    throw new InvalidOperationException("反序列化配置失败");
                }

                // 验证配置值的有效性
                ValidateAndFixConfig(config);

                _logService.Info("配置加载成功");
                return config;
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "配置文件损坏或格式错误");

                // 备份损坏的配置文件
                await BackupCorruptedConfigAsync(configPath);

                // 触发配置损坏事件
                OnConfigCorrupted();

                // 返回默认配置
                return GetDefaultConfig();
            }
        }

        /// <summary>
        /// 保存配置
        /// </summary>
        public async Task SaveConfigAsync(AppConfig config)
        {
            var configPath = GetConfigFilePath();

            // 确保目录存在
            var directory = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // 如果配置文件已存在，先备份
            if (File.Exists(configPath))
            {
                await BackupConfigAsync(configPath);
            }

            var json = JsonSerializer.Serialize(config, _jsonOptions);
            await File.WriteAllTextAsync(configPath, json);

            _logService.Info("配置已保存");
        }

        /// <summary>
        /// 获取默认配置
        /// </summary>
        private static AppConfig GetDefaultConfig()
        {
            return new AppConfig
            {
                RestIntervalMinutes = 30,
                RestDurationSeconds = 20,
                IsForcedMode = true,
                RestWindowMode = "black",
                CustomImagePath = "",
                PasswordHash = "",
                AutoStart = false,
                LogLevel = "INFO"
            };
        }

        /// <summary>
        /// 验证并修复配置值
        /// </summary>
        private static void ValidateAndFixConfig(AppConfig config)
        {
            // 休息间隔：1-120 分钟
            if (config.RestIntervalMinutes < 1)
                config.RestIntervalMinutes = 1;
            if (config.RestIntervalMinutes > 120)
                config.RestIntervalMinutes = 120;

            // 休息时长：5-300 秒
            if (config.RestDurationSeconds < 5)
                config.RestDurationSeconds = 5;
            if (config.RestDurationSeconds > 300)
                config.RestDurationSeconds = 300;

            // 休息窗口模式
            if (config.RestWindowMode != "black" && config.RestWindowMode != "image")
                config.RestWindowMode = "black";

            // 日志级别
            var validLogLevels = new[] { "DEBUG", "INFO", "WARN", "ERROR" };
            if (!validLogLevels.Contains(config.LogLevel?.ToUpper()))
                config.LogLevel = "INFO";
        }

        /// <summary>
        /// 备份配置文件
        /// </summary>
        private async Task BackupConfigAsync(string configPath)
        {
            try
            {
                var backupPath = GetBackupFilePath();

                // 如果已有备份文件，先删除
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                // 复制当前配置为备份
                File.Copy(configPath, backupPath);

                _logService.Debug($"配置已备份到: {backupPath}");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "备份配置文件失败");
            }
        }

        /// <summary>
        /// 备份损坏的配置文件
        /// </summary>
        private async Task BackupCorruptedConfigAsync(string configPath)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var corruptedBackupPath = Path.Combine(
                    _configDirectory,
                    $"config.corrupted.{timestamp}.json");

                File.Copy(configPath, corruptedBackupPath);

                _logService.Info($"损坏的配置已备份到: {corruptedBackupPath}");

                // 清理旧的损坏配置备份（保留最多5个）
                CleanupOldCorruptedBackups();
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "备份损坏配置文件失败");
            }
        }

        /// <summary>
        /// 获取备份文件路径
        /// </summary>
        private string GetBackupFilePath()
        {
            return Path.Combine(_configDirectory, BackupConfigFileName);
        }

        /// <summary>
        /// 清理旧的损坏配置备份
        /// </summary>
        private void CleanupOldCorruptedBackups()
        {
            try
            {
                var corruptedFiles = Directory.GetFiles(_configDirectory, "config.corrupted.*.json")
                    .OrderByDescending(f => f)
                    .Skip(MaxBackupFiles);

                foreach (var file in corruptedFiles)
                {
                    File.Delete(file);
                    _logService.Debug($"删除旧的损坏配置备份: {file}");
                }
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "清理损坏配置备份失败");
            }
        }

        /// <summary>
        /// 触发配置损坏事件
        /// </summary>
        protected virtual void OnConfigCorrupted()
        {
            ConfigCorrupted?.Invoke(this, new ConfigCorruptedEventArgs());
        }
    }

    /// <summary>
    /// 配置损坏事件参数
    /// </summary>
    public class ConfigCorruptedEventArgs : EventArgs
    {
    }
}
