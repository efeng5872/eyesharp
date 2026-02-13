using System;
using NLog;
using NLog.Config;

namespace eyesharp.Services
{
    /// <summary>
    /// 日志服务实现
    /// </summary>
    public class LogService : ILogService
    {
        private readonly Logger _logger;

        public LogService()
        {
            _logger = LogManager.GetCurrentClassLogger();
        }

        /// <summary>
        /// 设置日志级别
        /// </summary>
        public void SetLogLevel(string logLevel)
        {
            var level = LogLevel.FromString(logLevel.ToLower());

            foreach (var rule in LogManager.Configuration.LoggingRules)
            {
                rule.SetLoggingLevels(level, LogLevel.Fatal);
            }

            LogManager.ReconfigExistingLoggers();
        }

        /// <summary>
        /// 记录调试日志
        /// </summary>
        public void Debug(string message, params object[] args)
        {
            _logger.Debug(message, args);
        }

        /// <summary>
        /// 记录信息日志
        /// </summary>
        public void Info(string message, params object[] args)
        {
            _logger.Info(message, args);
        }

        /// <summary>
        /// 记录警告日志
        /// </summary>
        public void Warn(string message, params object[] args)
        {
            _logger.Warn(message, args);
        }

        /// <summary>
        /// 记录错误日志
        /// </summary>
        public void Error(string message, params object[] args)
        {
            _logger.Error(message, args);
        }

        /// <summary>
        /// 记录异常日志
        /// </summary>
        public void Error(Exception ex, string message, params object[] args)
        {
            _logger.Error(ex, message, args);
        }
    }
}
