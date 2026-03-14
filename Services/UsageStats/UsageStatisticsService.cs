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
        private readonly IInputMonitorService _inputMonitorService;
        private readonly object _lock = new();

        // 当前小时累计数据（已落盘事实 + 当前小时内事件增量）
        private HourlyActivityRecord _currentHourRecord = new();
        private DateTime _currentHour;
        private bool _isInitialized;

        // 输入计数快照（用于增量计算，避免重复累计）
        private InputEventCounter _lastCounterSnapshot = new();

        public UsageStatisticsService(ILogService logService, IInputMonitorService inputMonitorService, string? dbPath = null)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _inputMonitorService = inputMonitorService ?? throw new ArgumentNullException(nameof(inputMonitorService));
            _dbContext = new UsageStatisticsDbContext(logService, dbPath);
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

            var currentHourStart = DateTime.Now.Date.AddHours(DateTime.Now.Hour);
            var existingRecord = await _dbContext.GetHourlyRecordsAsync(currentHourStart, currentHourStart.AddHours(1).AddSeconds(-1));

            lock (_lock)
            {
                _currentHour = currentHourStart;
                if (existingRecord.Count > 0)
                {
                    _currentHourRecord = CloneHourlyRecord(existingRecord[0]);
                    _logService.Debug($"[UsageStatistics] 加载当前小时已有数据: 按键{_currentHourRecord.KeyPressCount}次");
                }

                _lastCounterSnapshot = CloneCounter(_inputMonitorService.GetCurrentCounter());
                _isInitialized = true;
            }

            _logService.Info("[UsageStatistics] 使用统计服务初始化完成");
        }

        /// <inheritdoc />
        public async Task<DailyUsageStatistics> GetTodayStatisticsAsync()
        {
            EnsureInitialized();
            var today = DateTime.Now.Date;
            return await AggregateOneDayAsync(today);
        }

        /// <inheritdoc />
        public async Task<DailyUsageStatistics> GetWeekStatisticsAsync()
        {
            EnsureInitialized();

            var today = DateTime.Now.Date;
            var daysSinceMonday = (int)today.DayOfWeek - 1;
            if (daysSinceMonday < 0) daysSinceMonday = 6;
            var startOfWeek = today.AddDays(-daysSinceMonday);

            return await AggregateRangeToOneStatisticsAsync(startOfWeek, today, startOfWeek);
        }

        /// <inheritdoc />
        public async Task<DailyUsageStatistics> GetMonthStatisticsAsync()
        {
            EnsureInitialized();

            var today = DateTime.Now.Date;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);

            return await AggregateRangeToOneStatisticsAsync(startOfMonth, today, startOfMonth);
        }

        /// <inheritdoc />
        public async Task<List<DailyUsageStatistics>> GetRecentDailyStatisticsAsync(int days)
        {
            EnsureInitialized();
            if (days <= 0)
            {
                return new List<DailyUsageStatistics>();
            }

            var endDate = DateTime.Now.Date;
            var startDate = endDate.AddDays(-days + 1);

            var allFacts = await GetHourlyFactsAsync(startDate, endDate);
            var byDay = allFacts.GroupBy(r => r.Hour.Date)
                .ToDictionary(g => g.Key, g => g.ToList());

            var result = new List<DailyUsageStatistics>(days);
            for (var day = startDate; day <= endDate; day = day.AddDays(1))
            {
                byDay.TryGetValue(day, out var records);
                result.Add(AggregateDailyStatistics(records ?? new List<HourlyActivityRecord>(), day));
            }

            return result;
        }

        /// <inheritdoc />
        public async Task<List<HourlyActivityRecord>> GetHourlyDataAsync(DateTime startDate, DateTime endDate)
        {
            EnsureInitialized();

            if (endDate < startDate)
            {
                return new List<HourlyActivityRecord>();
            }

            var records = await _dbContext.GetHourlyRecordsAsync(startDate, endDate);
            var now = DateTime.Now;

            DateTime currentHour;
            lock (_lock)
            {
                currentHour = _currentHour;
            }

            if (startDate <= currentHour && endDate >= currentHour)
            {
                var currentRecord = BuildCurrentHourRecordWithRealtime(now);
                var existingIndex = records.FindIndex(r => r.Hour == currentHour);
                if (existingIndex >= 0)
                {
                    records[existingIndex] = currentRecord;
                }
                else
                {
                    records.Add(currentRecord);
                }
            }

            return records.OrderBy(r => r.Hour).ToList();
        }

        /// <inheritdoc />
        public async Task<RealTimeUsageStatus> GetRealTimeStatusAsync()
        {
            EnsureInitialized();

            var todayStats = await GetTodayStatisticsAsync();

            return new RealTimeUsageStatus
            {
                CurrentHour = DateTime.Now.Date.AddHours(DateTime.Now.Hour),
                CurrentState = Enum.Parse<eyesharp.Models.UserActivityState>(_inputMonitorService.CurrentState.ToString()),
                StateStartTime = _inputMonitorService.StateStartTime,
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

        /// <inheritdoc />
        public void HandleActivityStateChanged(ActivityStateChangedEventArgs args)
        {
            if (!_isInitialized)
            {
                return;
            }

            var now = DateTime.Now;
            HourlyActivityRecord? recordToPersist;

            lock (_lock)
            {
                RotateCurrentHourIfNeeded(now, out recordToPersist);

                var durationSeconds = Math.Max(0, (int)args.PreviousStateDuration.TotalSeconds);
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

                ApplyInputCounterSnapshot(args.CounterSnapshot, now);
            }

            if (recordToPersist != null)
            {
                _ = PersistHourlyRecordSafeAsync(recordToPersist);
            }
        }

        /// <inheritdoc />
        public void HandleCounterUpdated(InputCounterEventArgs args)
        {
            if (!_isInitialized)
            {
                return;
            }

            var now = DateTime.Now;
            HourlyActivityRecord? recordToPersist;

            lock (_lock)
            {
                RotateCurrentHourIfNeeded(now, out recordToPersist);
                ApplyInputCounterSnapshot(args.Counter, now);
            }

            if (recordToPersist != null)
            {
                _ = PersistHourlyRecordSafeAsync(recordToPersist);
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
                _currentHourRecord.LastUpdateTime = DateTime.Now;
                _currentHourRecord.IsCompleteHour = false;
            }

            _logService.Debug("[UsageStatistics] 记录锁屏事件");
        }

        /// <inheritdoc />
        public void HandleUnlockScreen()
        {
            _logService.Debug("[UsageStatistics] 记录解锁事件");
        }

        /// <inheritdoc />
        public void CaptureSnapshotNow()
        {
            if (!_isInitialized)
            {
                return;
            }

            var snapshot = _inputMonitorService.GetCurrentCounter();
            HandleCounterUpdated(new InputCounterEventArgs
            {
                RecordTime = DateTime.Now,
                Counter = snapshot,
                CurrentState = _inputMonitorService.CurrentState
            });
            _logService.Debug("[UsageStatistics] 手动触发即时快照入账完成");
        }

        /// <inheritdoc />
        public async Task SaveAsync()
        {
            EnsureInitialized();
            _logService.Debug("[UsageStatistics] 保存统计数据...");

            await SaveCurrentHourRecordAsync();
            await UpdateDailySummaryAsync();

            _logService.Debug("[UsageStatistics] 统计数据保存完成");
        }

        /// <inheritdoc />
        public async Task<int> RebuildDailySummariesAsync(DateTime startDate, DateTime endDate)
        {
            EnsureInitialized();

            var rangeStart = startDate.Date;
            var rangeEnd = endDate.Date;
            if (rangeEnd < rangeStart)
            {
                return 0;
            }

            _logService.Info($"[UsageStatistics] 开始重算日汇总: {rangeStart:yyyy-MM-dd} ~ {rangeEnd:yyyy-MM-dd}");

            var allFacts = await GetHourlyFactsAsync(rangeStart, rangeEnd);
            var byDay = allFacts.GroupBy(r => r.Hour.Date)
                .ToDictionary(g => g.Key, g => g.ToList());

            var rebuiltCount = 0;
            var now = DateTime.Now;

            for (var day = rangeStart; day <= rangeEnd; day = day.AddDays(1))
            {
                byDay.TryGetValue(day, out var dayRecords);
                dayRecords ??= new List<HourlyActivityRecord>();

                var summary = AggregateDailyStatistics(dayRecords, day);
                summary.FirstRecordTime = dayRecords.Count > 0 ? dayRecords.Min(r => r.Hour) : day;
                summary.LastRecordTime = day == now.Date ? now : day.AddDays(1).AddSeconds(-1);

                await _dbContext.SaveDailySummaryAsync(summary);
                rebuiltCount++;
            }

            _logService.Info($"[UsageStatistics] 日汇总重算完成: {rangeStart:yyyy-MM-dd} ~ {rangeEnd:yyyy-MM-dd}, 共{rebuiltCount}天");
            return rebuiltCount;
        }

        /// <inheritdoc />
        public async Task<string> ExportToCsvAsync(DateTime startDate, DateTime endDate)
        {
            EnsureInitialized();

            var records = await GetHourlyDataAsync(startDate, endDate);
            var sb = new StringBuilder();

            sb.AppendLine("Hour,ActiveSeconds,IdleSeconds,LockScreenSeconds,LockScreenCount," +
                "KeyPressCount,MouseMoveDistance,MouseLeftClickCount,MouseRightClickCount," +
                "MouseMiddleClickCount,MouseWheelScrollCount");

            foreach (var record in records)
            {
                sb.AppendLine($"{record.Hour:yyyy-MM-dd HH:mm:ss}," +
                    $"{record.ActiveSeconds},{record.IdleSeconds},{record.LockScreenSeconds},{record.LockScreenCount}," +
                    $"{record.KeyPressCount},{record.MouseMoveDistance},{record.MouseLeftClickCount}," +
                    $"{record.MouseRightClickCount},{record.MouseMiddleClickCount},{record.MouseWheelScrollCount}");
            }

            var fileName = $"usage_statistics_{startDate:yyyyMMdd}_{endDate:yyyyMMdd}.csv";
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);

            _logService.Info($"[UsageStatistics] 数据已导出到: {filePath}");
            return filePath;
        }

        private async Task<DailyUsageStatistics> AggregateOneDayAsync(DateTime day)
        {
            var facts = await GetHourlyFactsAsync(day, day);
            return AggregateDailyStatistics(facts.Where(r => r.Hour.Date == day).ToList(), day);
        }

        private async Task<DailyUsageStatistics> AggregateRangeToOneStatisticsAsync(DateTime startDate, DateTime endDate, DateTime outputDate)
        {
            var facts = await GetHourlyFactsAsync(startDate, endDate);
            return AggregateDailyStatistics(facts, outputDate);
        }

        private async Task<List<HourlyActivityRecord>> GetHourlyFactsAsync(DateTime startDate, DateTime endDate)
        {
            var now = DateTime.Now;
            var queryStart = startDate.Date;
            var queryEnd = endDate.Date.AddDays(1).AddSeconds(-1);

            var records = await _dbContext.GetHourlyRecordsAsync(queryStart, queryEnd);

            if (endDate.Date >= now.Date)
            {
                DateTime currentHour;
                lock (_lock)
                {
                    currentHour = _currentHour;
                }

                records.RemoveAll(r => r.Hour == currentHour);
                records.Add(BuildCurrentHourRecordWithRealtime(now));
            }

            return records
                .Where(r => r.Hour.Date >= startDate.Date && r.Hour.Date <= endDate.Date)
                .GroupBy(r => r.Hour)
                .Select(g => g.OrderByDescending(x => x.LastUpdateTime).First())
                .OrderBy(r => r.Hour)
                .ToList();
        }

        private HourlyActivityRecord BuildCurrentHourRecordWithRealtime(DateTime now)
        {
            HourlyActivityRecord baseRecord;
            DateTime hourStart;

            lock (_lock)
            {
                hourStart = _currentHour;
                baseRecord = CloneHourlyRecord(_currentHourRecord);
            }

            var realtimeRecord = CloneHourlyRecord(baseRecord);
            realtimeRecord.Hour = hourStart;

            var state = _inputMonitorService.CurrentState;
            var stateStart = _inputMonitorService.StateStartTime;
            var overlapStart = stateStart > hourStart ? stateStart : hourStart;
            var additionalSeconds = Math.Max(0, (int)(now - overlapStart).TotalSeconds);

            if (additionalSeconds > 0)
            {
                switch (state)
                {
                    case UserActivityState.Active:
                        realtimeRecord.ActiveSeconds += additionalSeconds;
                        break;
                    case UserActivityState.Idle:
                        realtimeRecord.IdleSeconds += additionalSeconds;
                        break;
                    case UserActivityState.Locked:
                        realtimeRecord.LockScreenSeconds += additionalSeconds;
                        break;
                }
            }

            realtimeRecord.LastUpdateTime = now;
            realtimeRecord.IsCompleteHour = false;

            _logService.Debug($"[UsageStatistics] 当前小时补算: 小时{hourStart:yyyy-MM-dd HH}:00, 状态={state}, 补算{additionalSeconds}秒");

            return realtimeRecord;
        }

        private async Task SaveCurrentHourRecordAsync()
        {
            HourlyActivityRecord? recordToSave = null;

            lock (_lock)
            {
                if (_currentHourRecord.LastUpdateTime == default &&
                    _currentHourRecord.KeyPressCount == 0 &&
                    _currentHourRecord.MouseMoveDistance == 0 &&
                    _currentHourRecord.MouseLeftClickCount == 0 &&
                    _currentHourRecord.MouseRightClickCount == 0 &&
                    _currentHourRecord.MouseMiddleClickCount == 0 &&
                    _currentHourRecord.MouseWheelScrollCount == 0 &&
                    _currentHourRecord.ActiveSeconds == 0 &&
                    _currentHourRecord.IdleSeconds == 0 &&
                    _currentHourRecord.LockScreenSeconds == 0 &&
                    _currentHourRecord.LockScreenCount == 0)
                {
                    return;
                }

                recordToSave = CloneHourlyRecord(_currentHourRecord);
                recordToSave.Hour = _currentHour;
                recordToSave.IsCompleteHour = DateTime.Now >= _currentHour.AddHours(1);
                recordToSave.LastUpdateTime = DateTime.Now;
            }

            await _dbContext.SaveHourlyRecordAsync(recordToSave);
            _logService.Debug($"[UsageStatistics] 保存小时记录: {_currentHour:yyyy-MM-dd HH:mm}");
        }

        private async Task UpdateDailySummaryAsync()
        {
            var today = DateTime.Now.Date;
            var todayFacts = await GetHourlyFactsAsync(today, today);

            var summary = AggregateDailyStatistics(todayFacts, today);
            summary.FirstRecordTime = todayFacts.Any() ? todayFacts.Min(r => r.Hour) : today;
            summary.LastRecordTime = DateTime.Now;

            await _dbContext.SaveDailySummaryAsync(summary);
            _logService.Debug($"[UsageStatistics] 更新日汇总: {today:yyyy-MM-dd}");
        }

        private static DailyUsageStatistics AggregateDailyStatistics(List<HourlyActivityRecord> records, DateTime date)
        {
            if (records.Count == 0)
            {
                return new DailyUsageStatistics { Date = date };
            }

            return new DailyUsageStatistics
            {
                Date = date,
                TotalBootSeconds = records.Sum(r => r.ActiveSeconds + r.IdleSeconds + r.LockScreenSeconds),
                ActiveSeconds = records.Sum(r => r.ActiveSeconds),
                IdleSeconds = records.Sum(r => r.IdleSeconds),
                LockScreenSeconds = records.Sum(r => r.LockScreenSeconds),
                LockScreenCount = records.Sum(r => r.LockScreenCount),
                TotalKeyPressCount = records.Sum(r => r.KeyPressCount),
                TotalMouseMoveDistance = records.Sum(r => r.MouseMoveDistance),
                TotalMouseLeftClickCount = records.Sum(r => r.MouseLeftClickCount),
                TotalMouseRightClickCount = records.Sum(r => r.MouseRightClickCount),
                TotalMouseMiddleClickCount = records.Sum(r => r.MouseMiddleClickCount),
                TotalMouseWheelScrollCount = records.Sum(r => r.MouseWheelScrollCount)
            };
        }

        private static HourlyActivityRecord CloneHourlyRecord(HourlyActivityRecord source)
        {
            return new HourlyActivityRecord
            {
                Hour = source.Hour,
                ActiveSeconds = source.ActiveSeconds,
                IdleSeconds = source.IdleSeconds,
                LockScreenSeconds = source.LockScreenSeconds,
                LockScreenCount = source.LockScreenCount,
                KeyPressCount = source.KeyPressCount,
                MouseMoveDistance = source.MouseMoveDistance,
                MouseLeftClickCount = source.MouseLeftClickCount,
                MouseRightClickCount = source.MouseRightClickCount,
                MouseMiddleClickCount = source.MouseMiddleClickCount,
                MouseWheelScrollCount = source.MouseWheelScrollCount,
                IsCompleteHour = source.IsCompleteHour,
                FirstRecordTime = source.FirstRecordTime,
                LastUpdateTime = source.LastUpdateTime
            };
        }

        private static InputEventCounter CloneCounter(InputEventCounter source)
        {
            return new InputEventCounter
            {
                KeyPressCount = source.KeyPressCount,
                MouseMoveDistance = source.MouseMoveDistance,
                MouseLeftClickCount = source.MouseLeftClickCount,
                MouseRightClickCount = source.MouseRightClickCount,
                MouseMiddleClickCount = source.MouseMiddleClickCount,
                MouseWheelScrollCount = source.MouseWheelScrollCount,
                LastMousePosition = source.LastMousePosition,
                HasLastPosition = source.HasLastPosition
            };
        }

        private static InputEventCounter CalculateCounterDelta(InputEventCounter current, InputEventCounter previous)
        {
            return new InputEventCounter
            {
                KeyPressCount = Math.Max(0, current.KeyPressCount - previous.KeyPressCount),
                MouseMoveDistance = Math.Max(0, current.MouseMoveDistance - previous.MouseMoveDistance),
                MouseLeftClickCount = Math.Max(0, current.MouseLeftClickCount - previous.MouseLeftClickCount),
                MouseRightClickCount = Math.Max(0, current.MouseRightClickCount - previous.MouseRightClickCount),
                MouseMiddleClickCount = Math.Max(0, current.MouseMiddleClickCount - previous.MouseMiddleClickCount),
                MouseWheelScrollCount = Math.Max(0, current.MouseWheelScrollCount - previous.MouseWheelScrollCount)
            };
        }

        private void RotateCurrentHourIfNeeded(DateTime now, out HourlyActivityRecord? recordToPersist)
        {
            recordToPersist = null;
            var newHour = now.Date.AddHours(now.Hour);
            if (newHour <= _currentHour)
            {
                return;
            }

            recordToPersist = CloneHourlyRecord(_currentHourRecord);
            recordToPersist.IsCompleteHour = true;
            _currentHour = newHour;
            _currentHourRecord = new HourlyActivityRecord
            {
                Hour = _currentHour,
                FirstRecordTime = now,
                LastUpdateTime = now,
                IsCompleteHour = false
            };

            _logService.Debug($"[UsageStatistics] 切换到新小时: {_currentHour:yyyy-MM-dd HH:mm}");
        }

        private void ApplyInputCounterSnapshot(InputEventCounter currentSnapshot, DateTime now)
        {
            var delta = CalculateCounterDelta(currentSnapshot, _lastCounterSnapshot);
            _currentHourRecord.KeyPressCount += delta.KeyPressCount;
            _currentHourRecord.MouseMoveDistance += delta.MouseMoveDistance;
            _currentHourRecord.MouseLeftClickCount += delta.MouseLeftClickCount;
            _currentHourRecord.MouseRightClickCount += delta.MouseRightClickCount;
            _currentHourRecord.MouseMiddleClickCount += delta.MouseMiddleClickCount;
            _currentHourRecord.MouseWheelScrollCount += delta.MouseWheelScrollCount;

            _lastCounterSnapshot = CloneCounter(currentSnapshot);
            _currentHourRecord.LastUpdateTime = now;
            _currentHourRecord.IsCompleteHour = false;

            _logService.Debug($"[UsageStatistics] 输入增量入账: 按键+{delta.KeyPressCount}, 左键+{delta.MouseLeftClickCount}, 右键+{delta.MouseRightClickCount}, 中键+{delta.MouseMiddleClickCount}, 滚轮+{delta.MouseWheelScrollCount}, 移动+{delta.MouseMoveDistance}");
        }

        private async Task PersistHourlyRecordSafeAsync(HourlyActivityRecord record)
        {
            try
            {
                await _dbContext.SaveHourlyRecordAsync(record);
                _logService.Debug($"[UsageStatistics] 异步保存跨小时记录: {record.Hour:yyyy-MM-dd HH:mm}");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "[UsageStatistics] 异步保存跨小时记录失败");
            }
        }

        private void EnsureInitialized()
        {
            if (!_isInitialized)
            {
                throw new InvalidOperationException("使用统计服务尚未初始化，请先调用 InitializeAsync()");
            }
        }

        public void Dispose()
        {
            if (_isInitialized)
            {
                try
                {
                    SaveAsync().GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logService.Error(ex, "[UsageStatistics] Dispose 时保存数据失败");
                }
            }

            _dbContext.Dispose();
        }
    }
}
