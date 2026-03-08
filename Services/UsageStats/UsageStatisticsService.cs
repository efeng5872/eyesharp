using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using eyesharp.Models;

namespace eyesharp.Services.UsageStats
{
    /// <summary>
    /// 使用统计服务实现
    /// </summary>
    public class UsageStatisticsService : IUsageStatisticsService, IDisposable
    {
        private readonly ILogService _logService;
        private readonly UsageStatisticsDbContext _dbContext;
        private readonly object _lock = new();

        // 当前小时累计数据
        private HourlyActivityRecord _currentHourRecord = new();
        private DateTime _currentHour;
        private bool _isInitialized = false;

        public UsageStatisticsService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _dbContext = new UsageStatisticsDbContext(logService);
            _currentHour = DateTime.Now.Date.AddHours(DateTime.Now.Hour);
        }

        /// <inheritdoc />
        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                return;
            }

            _logService.Info("[UsageStatistics] 初始化使用统计服务...");

            await _dbContext.InitializeAsync();

            // 加载当前小时已有数据（如果有）
            var existingRecord = await _dbContext.GetHourlyRecordsAsync(_currentHour, _currentHour.AddHours(1).AddSeconds(-1));
            if (existingRecord.Count > 0)
            {
                _currentHourRecord = existingRecord[0];
                _logService.Debug($"[UsageStatistics] 加载当前小时已有数据: 按键{_currentHourRecord.KeyPressCount}次");
            }

            _isInitialized = true;
            _logService.Info("[UsageStatistics] 使用统计服务初始化完成");
        }

        /// <inheritdoc />
        public DailyUsageStatistics GetTodayStatistics()
        {
            EnsureInitialized();

            var today = DateTime.Now.Date;
            var summary = _dbContext.GetDailySummaryAsync(today).Result;

            if (summary == null)
            {
                // 如果没有日汇总，返回空对象
                return new DailyUsageStatistics { Date = today };
            }

            // 添加当前小时的数据
            lock (_lock)
            {
                summary.TotalKeyPressCount += _currentHourRecord.KeyPressCount;
                summary.TotalMouseMoveDistance += _currentHourRecord.MouseMoveDistance;
                summary.TotalMouseLeftClickCount += _currentHourRecord.MouseLeftClickCount;
                summary.TotalMouseRightClickCount += _currentHourRecord.MouseRightClickCount;
                summary.TotalMouseMiddleClickCount += _currentHourRecord.MouseMiddleClickCount;
                summary.TotalMouseWheelScrollCount += _currentHourRecord.MouseWheelScrollCount;
                summary.ActiveSeconds += _currentHourRecord.ActiveSeconds;
                summary.IdleSeconds += _currentHourRecord.IdleSeconds;
                summary.LockScreenSeconds += _currentHourRecord.LockScreenSeconds;
            }

            return summary;
        }

        /// <inheritdoc />
        public DailyUsageStatistics GetWeekStatistics()
        {
            EnsureInitialized();

            var today = DateTime.Now.Date;
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);

            var dailyStats = _dbContext.GetRecentDailySummariesAsync(7).Result
                .Where(d => d.Date >= startOfWeek && d.Date <= today)
                .ToList();

            // 添加今天的实时数据
            var todayStats = GetTodayStatistics();
            dailyStats.Add(todayStats);

            return AggregateDailyStatistics(dailyStats, startOfWeek);
        }

        /// <inheritdoc />
        public DailyUsageStatistics GetMonthStatistics()
        {
            EnsureInitialized();

            var today = DateTime.Now;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);

            var daysInMonth = (today.Date - startOfMonth).Days + 1;
            var dailyStats = _dbContext.GetRecentDailySummariesAsync(daysInMonth).Result
                .Where(d => d.Date >= startOfMonth && d.Date <= today.Date)
                .ToList();

            // 添加今天的实时数据
            var todayStats = GetTodayStatistics();
            dailyStats.Add(todayStats);

            return AggregateDailyStatistics(dailyStats, startOfMonth);
        }

        /// <inheritdoc />
        public List<DailyUsageStatistics> GetRecentDailyStatistics(int days)
        {
            EnsureInitialized();

            var endDate = DateTime.Now.Date;
            var startDate = endDate.AddDays(-days + 1);

            var stats = _dbContext.GetRecentDailySummariesAsync(days).Result;

            // 确保包含今天的数据
            if (!stats.Any(s => s.Date == endDate))
            {
                stats.Add(GetTodayStatistics());
            }

            // 按日期排序
            return stats.OrderBy(s => s.Date).ToList();
        }

        /// <inheritdoc />
        public List<HourlyActivityRecord> GetHourlyData(DateTime startDate, DateTime endDate)
        {
            EnsureInitialized();

            var records = _dbContext.GetHourlyRecordsAsync(startDate, endDate).Result;

            // 如果查询包含当前小时，添加当前数据
            if (endDate >= _currentHour)
            {
                lock (_lock)
                {
                    var currentRecord = new HourlyActivityRecord
                    {
                        Hour = _currentHour,
                        ActiveSeconds = _currentHourRecord.ActiveSeconds,
                        IdleSeconds = _currentHourRecord.IdleSeconds,
                        LockScreenSeconds = _currentHourRecord.LockScreenSeconds,
                        LockScreenCount = _currentHourRecord.LockScreenCount,
                        KeyPressCount = _currentHourRecord.KeyPressCount,
                        MouseMoveDistance = _currentHourRecord.MouseMoveDistance,
                        MouseLeftClickCount = _currentHourRecord.MouseLeftClickCount,
                        MouseRightClickCount = _currentHourRecord.MouseRightClickCount,
                        MouseMiddleClickCount = _currentHourRecord.MouseMiddleClickCount,
                        MouseWheelScrollCount = _currentHourRecord.MouseWheelScrollCount
                    };

                    // 如果已有当前小时记录则更新，否则添加
                    var existingIndex = records.FindIndex(r => r.Hour == _currentHour);
                    if (existingIndex >= 0)
                    {
                        records[existingIndex] = currentRecord;
                    }
                    else
                    {
                        records.Add(currentRecord);
                    }
                }
            }

            return records.OrderBy(r => r.Hour).ToList();
        }

        /// <inheritdoc />
        public RealTimeUsageStatus GetRealTimeStatus()
        {
            EnsureInitialized();

            lock (_lock)
            {
                var today = DateTime.Now.Date;
                var todayStats = GetTodayStatistics();

                return new RealTimeUsageStatus
                {
                    CurrentHour = _currentHour,
                    TodayActiveTime = TimeSpan.FromSeconds(todayStats.ActiveSeconds),
                    TodayIdleTime = TimeSpan.FromSeconds(todayStats.IdleSeconds),
                    TodayInputCounter = new InputEventCounter
                    {
                        KeyPressCount = todayStats.TotalKeyPressCount,
                        MouseMoveDistance = todayStats.TotalMouseMoveDistance,
                        MouseLeftClickCount = todayStats.TotalMouseLeftClickCount,
                        MouseRightClickCount = todayStats.TotalMouseRightClickCount,
                        MouseMiddleClickCount = todayStats.TotalMouseMiddleClickCount,
                        MouseWheelScrollCount = todayStats.TotalMouseWheelScrollCount
                    }
                };
            }
        }

        /// <inheritdoc />
        public void HandleActivityStateChanged(ActivityStateChangedEventArgs args)
        {
            if (!_isInitialized)
            {
                return;
            }

            lock (_lock)
            {
                // 检查是否需要切换小时
                var now = DateTime.Now;
                var newHour = now.Date.AddHours(now.Hour);
                if (newHour > _currentHour)
                {
                    // 保存当前小时数据
                    SaveCurrentHourRecordAsync().Wait();

                    // 切换到新小时
                    _currentHour = newHour;
                    _currentHourRecord = new HourlyActivityRecord
                    {
                        Hour = _currentHour,
                        FirstRecordTime = now
                    };

                    _logService.Debug($"[UsageStatistics] 切换到新小时: {_currentHour:yyyy-MM-dd HH:mm}");
                }

                // 根据上一状态累计时长
                var durationSeconds = (int)args.PreviousStateDuration.TotalSeconds;
                switch (args.OldState)
                {
                    case UserActivityState.Active:
                        _currentHourRecord.ActiveSeconds += durationSeconds;
                        break;
                    case UserActivityState.Idle:
                        _currentHourRecord.IdleSeconds += durationSeconds;
                        break;
                    case UserActivityState.Locked:
                        _currentHourRecord.LockScreenSeconds += durationSeconds;
                        break;
                }

                // 累计输入计数
                if (args.CounterSnapshot != null)
                {
                    _currentHourRecord.KeyPressCount += args.CounterSnapshot.KeyPressCount;
                    _currentHourRecord.MouseMoveDistance += args.CounterSnapshot.MouseMoveDistance;
                    _currentHourRecord.MouseLeftClickCount += args.CounterSnapshot.MouseLeftClickCount;
                    _currentHourRecord.MouseRightClickCount += args.CounterSnapshot.MouseRightClickCount;
                    _currentHourRecord.MouseMiddleClickCount += args.CounterSnapshot.MouseMiddleClickCount;
                    _currentHourRecord.MouseWheelScrollCount += args.CounterSnapshot.MouseWheelScrollCount;
                }

                _currentHourRecord.LastUpdateTime = now;
            }
        }

        /// <inheritdoc />
        public void HandleLockScreen()
        {
            if (!_isInitialized)
            {
                return;
            }

            lock (_lock)
            {
                _currentHourRecord.LockScreenCount++;
                _logService.Debug("[UsageStatistics] 记录锁屏事件");
            }
        }

        /// <inheritdoc />
        public void HandleUnlockScreen()
        {
            // 解锁时的处理已在状态变化中处理
            _logService.Debug("[UsageStatistics] 记录解锁事件");
        }

        /// <inheritdoc />
        public async Task SaveAsync()
        {
            EnsureInitialized();

            _logService.Debug("[UsageStatistics] 保存统计数据...");

            // 保存当前小时记录
            await SaveCurrentHourRecordAsync();

            // 更新日汇总
            await UpdateDailySummaryAsync();

            _logService.Debug("[UsageStatistics] 统计数据保存完成");
        }

        /// <inheritdoc />
        public async Task<string> ExportToCsvAsync(DateTime startDate, DateTime endDate)
        {
            EnsureInitialized();

            var records = await _dbContext.GetHourlyRecordsAsync(startDate, endDate);
            var sb = new StringBuilder();

            // 写入CSV头部
            sb.AppendLine("Hour,ActiveSeconds,IdleSeconds,LockScreenSeconds,LockScreenCount," +
                "KeyPressCount,MouseMoveDistance,MouseLeftClickCount,MouseRightClickCount," +
                "MouseMiddleClickCount,MouseWheelScrollCount");

            // 写入数据
            foreach (var record in records)
            {
                sb.AppendLine($"{record.Hour:yyyy-MM-dd HH:mm:ss}," +
                    $"{record.ActiveSeconds},{record.IdleSeconds},{record.LockScreenSeconds},{record.LockScreenCount}," +
                    $"{record.KeyPressCount},{record.MouseMoveDistance},{record.MouseLeftClickCount}," +
                    $"{record.MouseRightClickCount},{record.MouseMiddleClickCount},{record.MouseWheelScrollCount}");
            }

            // 保存到文件
            var fileName = $"usage_statistics_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv";
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);

            _logService.Info($"[UsageStatistics] 数据已导出到: {filePath}");
            return filePath;
        }

        /// <summary>
        /// 保存当前小时记录到数据库
        /// </summary>
        private async Task SaveCurrentHourRecordAsync()
        {
            lock (_lock)
            {
                if (_currentHourRecord.LastUpdateTime == default)
                {
                    return; // 没有数据需要保存
                }

                _currentHourRecord.IsCompleteHour = DateTime.Now >= _currentHour.AddHours(1);
            }

            await _dbContext.SaveHourlyRecordAsync(_currentHourRecord);
            _logService.Debug($"[UsageStatistics] 保存小时记录: {_currentHour:yyyy-MM-dd HH:mm}");
        }

        /// <summary>
        /// 更新日汇总
        /// </summary>
        private async Task UpdateDailySummaryAsync()
        {
            var today = DateTime.Now.Date;
            var hourlyRecords = await _dbContext.GetHourlyRecordsAsync(today, today.AddDays(1).AddSeconds(-1));

            // 添加当前小时数据
            lock (_lock)
            {
                hourlyRecords.Add(_currentHourRecord);
            }

            var summary = new DailyUsageStatistics
            {
                Date = today,
                TotalBootSeconds = hourlyRecords.Sum(r => r.ActiveSeconds + r.IdleSeconds + r.LockScreenSeconds),
                ActiveSeconds = hourlyRecords.Sum(r => r.ActiveSeconds),
                IdleSeconds = hourlyRecords.Sum(r => r.IdleSeconds),
                LockScreenSeconds = hourlyRecords.Sum(r => r.LockScreenSeconds),
                LockScreenCount = hourlyRecords.Sum(r => r.LockScreenCount),
                TotalKeyPressCount = hourlyRecords.Sum(r => r.KeyPressCount),
                TotalMouseMoveDistance = hourlyRecords.Sum(r => r.MouseMoveDistance),
                TotalMouseLeftClickCount = hourlyRecords.Sum(r => r.MouseLeftClickCount),
                TotalMouseRightClickCount = hourlyRecords.Sum(r => r.MouseRightClickCount),
                TotalMouseMiddleClickCount = hourlyRecords.Sum(r => r.MouseMiddleClickCount),
                TotalMouseWheelScrollCount = hourlyRecords.Sum(r => r.MouseWheelScrollCount),
                FirstRecordTime = hourlyRecords.Min(r => r.Hour),
                LastRecordTime = DateTime.Now
            };

            await _dbContext.SaveDailySummaryAsync(summary);
            _logService.Debug($"[UsageStatistics] 更新日汇总: {today:yyyy-MM-dd}");
        }

        /// <summary>
        /// 聚合多日统计数据
        /// </summary>
        private static DailyUsageStatistics AggregateDailyStatistics(List<DailyUsageStatistics> dailyStats, DateTime date)
        {
            if (dailyStats.Count == 0)
            {
                return new DailyUsageStatistics { Date = date };
            }

            return new DailyUsageStatistics
            {
                Date = date,
                TotalBootSeconds = dailyStats.Sum(s => s.TotalBootSeconds),
                ActiveSeconds = dailyStats.Sum(s => s.ActiveSeconds),
                IdleSeconds = dailyStats.Sum(s => s.IdleSeconds),
                LockScreenSeconds = dailyStats.Sum(s => s.LockScreenSeconds),
                LockScreenCount = dailyStats.Sum(s => s.LockScreenCount),
                TotalKeyPressCount = dailyStats.Sum(s => s.TotalKeyPressCount),
                TotalMouseMoveDistance = dailyStats.Sum(s => s.TotalMouseMoveDistance),
                TotalMouseLeftClickCount = dailyStats.Sum(s => s.TotalMouseLeftClickCount),
                TotalMouseRightClickCount = dailyStats.Sum(s => s.TotalMouseRightClickCount),
                TotalMouseMiddleClickCount = dailyStats.Sum(s => s.TotalMouseMiddleClickCount),
                TotalMouseWheelScrollCount = dailyStats.Sum(s => s.TotalMouseWheelScrollCount)
            };
        }

        /// <summary>
        /// 确保服务已初始化
        /// </summary>
        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("使用统计服务尚未初始化，请先调用 InitializeAsync()");
            }
        }

        public void Dispose()
        {
            // 保存当前数据
            if (_isInitialized)
            {
                SaveAsync().Wait();
            }

            _dbContext?.Dispose();
        }
    }
}
