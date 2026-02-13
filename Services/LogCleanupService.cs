using System;
using System.IO;
using System.Linq;
using eyesharp.Helpers;

namespace eyesharp.Services
{
    /// <summary>
    /// 日志清理服务
    /// </summary>
    public class LogCleanupService
    {
        private const int MaxLogDays = 30; // 保留30天的日志

        private readonly ILogService _logService;
        private readonly string _logsDirectory;
        private readonly string _archiveDirectory;

        public LogCleanupService(ILogService logService)
        {
            _logService = logService;
            _logsDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            _archiveDirectory = Path.Combine(_logsDirectory, "archive");
        }

        /// <summary>
        /// 清理旧日志文件
        /// </summary>
        public void CleanupOldLogs()
        {
            try
            {
                // 确保日志目录存在
                if (!Directory.Exists(_logsDirectory))
                {
                    return;
                }

                var cutoffDate = DateTime.Now.AddDays(-MaxLogDays);
                int deletedCount = 0;

                // 清理主日志目录中的旧文件
                deletedCount += CleanupDirectory(_logsDirectory, cutoffDate);

                // 清理归档目录中的旧文件
                if (Directory.Exists(_archiveDirectory))
                {
                    deletedCount += CleanupDirectory(_archiveDirectory, cutoffDate);
                }

                if (deletedCount > 0)
                {
                    _logService.Info($"日志清理完成，删除了 {deletedCount} 个超过 {MaxLogDays} 天的旧日志文件");
                }
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "清理日志文件失败");
            }
        }

        /// <summary>
        /// 清理指定目录中的旧文件
        /// </summary>
        private int CleanupDirectory(string directory, DateTime cutoffDate)
        {
            int deletedCount = 0;

            try
            {
                var files = Directory.GetFiles(directory, "*.log")
                    .Where(f => IsOldLogFile(f, cutoffDate));

                foreach (var file in files)
                {
                    try
                    {
                        File.Delete(file);
                        deletedCount++;
                        _logService.Debug($"删除旧日志: {Path.GetFileName(file)}");
                    }
                    catch (Exception ex)
                    {
                        _logService.Error(ex, $"删除日志文件失败: {file}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Error(ex, $"扫描日志目录失败: {directory}");
            }

            return deletedCount;
        }

        /// <summary>
        /// 判断文件是否为需要清理的旧日志
        /// </summary>
        private bool IsOldLogFile(string filePath, DateTime cutoffDate)
        {
            try
            {
                // 检查文件修改时间
                var fileTime = File.GetLastWriteTime(filePath);
                if (fileTime < cutoffDate)
                {
                    return true;
                }

                // 检查文件名中的日期（格式：eyesharp-YYYY-MM-DD.log）
                var fileName = Path.GetFileNameWithoutExtension(filePath);
                if (fileName.StartsWith("eyesharp-"))
                {
                    var datePart = fileName.Substring(9); // 跳过 "eyesharp-"
                    if (DateTime.TryParseExact(datePart, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var fileDate))
                    {
                        return fileDate < cutoffDate.Date;
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}
