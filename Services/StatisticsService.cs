using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using eyesharp.Models;
using System.Text.Json;

namespace eyesharp.Services
{
    /// <summary>
    /// 统计服务实现
    /// </summary>
    public class StatisticsService : IStatisticsService
    {
        private readonly ILogService _logService;
        private readonly string _dataFilePath;
        private StatisticsData _data = new StatisticsData();
        private RestRecord? _currentRecord;
        private readonly object _lock = new object();

        public StatisticsService(ILogService logService)
        {
            _logService = logService;
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _dataFilePath = Path.Combine(baseDir, "statistics.json");
        }

        /// <summary>
        /// 开始记录休息
        /// </summary>
        public void StartRest(int plannedDurationSeconds, bool isForcedMode)
        {
            lock (_lock)
            {
                _currentRecord = new RestRecord
                {
                    StartTime = DateTime.Now,
                    PlannedDurationSeconds = plannedDurationSeconds,
                    IsForcedMode = isForcedMode
                };
            }
            _logService.Debug($"开始记录休息，计划时长：{plannedDurationSeconds}秒");
        }

        /// <summary>
        /// 结束记录休息
        /// </summary>
        public void EndRest(bool isCompleted)
        {
            lock (_lock)
            {
                if (_currentRecord == null) return;

                _currentRecord.EndTime = DateTime.Now;
                _currentRecord.DurationSeconds = (int)(_currentRecord.EndTime - _currentRecord.StartTime).TotalSeconds;
                _currentRecord.IsCompleted = isCompleted;

                _data.Records.Add(_currentRecord);
                _data.LastUpdated = DateTime.Now;

                // 更新每日统计缓存
                UpdateDailyStatistics(_currentRecord);

                _logService.Info($"休息记录完成：时长{_currentRecord.DurationSeconds}秒，状态：{(isCompleted ? "完成" : "提前结束")}");

                _currentRecord = null;
            }

            // 异步保存
            _ = SaveAsync();
        }

        /// <summary>
        /// 更新每日统计
        /// </summary>
        private void UpdateDailyStatistics(RestRecord record)
        {
            var date = record.StartTime.Date;
            var dailyStat = _data.DailyStats.FirstOrDefault(d => d.Date.Date == date);

            if (dailyStat == null)
            {
                dailyStat = new DailyStatistics { Date = date };
                _data.DailyStats.Add(dailyStat);
            }

            dailyStat.RestCount++;
            dailyStat.TotalDurationSeconds += record.DurationSeconds;
            dailyStat.AverageDurationSeconds = dailyStat.TotalDurationSeconds / dailyStat.RestCount;

            if (record.IsCompleted)
                dailyStat.CompletedCount++;
            else
                dailyStat.SkippedCount++;
        }

        /// <summary>
        /// 获取今日统计
        /// </summary>
        public DailyStatistics GetTodayStatistics()
        {
            lock (_lock)
            {
                var today = DateTime.Now.Date;
                return _data.DailyStats.FirstOrDefault(d => d.Date.Date == today)
                    ?? new DailyStatistics { Date = today };
            }
        }

        /// <summary>
        /// 获取本周统计
        /// </summary>
        public DailyStatistics GetWeekStatistics()
        {
            lock (_lock)
            {
                var today = DateTime.Now.Date;
                var startOfWeek = today.AddDays(-(int)today.DayOfWeek);

                var weekStats = _data.DailyStats
                    .Where(d => d.Date >= startOfWeek && d.Date <= today)
                    .ToList();

                return new DailyStatistics
                {
                    Date = startOfWeek,
                    RestCount = weekStats.Sum(s => s.RestCount),
                    TotalDurationSeconds = weekStats.Sum(s => s.TotalDurationSeconds),
                    AverageDurationSeconds = weekStats.Any() ? weekStats.Sum(s => s.TotalDurationSeconds) / weekStats.Sum(s => s.RestCount) : 0,
                    CompletedCount = weekStats.Sum(s => s.CompletedCount),
                    SkippedCount = weekStats.Sum(s => s.SkippedCount)
                };
            }
        }

        /// <summary>
        /// 获取本月统计
        /// </summary>
        public DailyStatistics GetMonthStatistics()
        {
            lock (_lock)
            {
                var today = DateTime.Now;
                var startOfMonth = new DateTime(today.Year, today.Month, 1);

                var monthStats = _data.DailyStats
                    .Where(d => d.Date >= startOfMonth && d.Date <= today.Date)
                    .ToList();

                return new DailyStatistics
                {
                    Date = startOfMonth,
                    RestCount = monthStats.Sum(s => s.RestCount),
                    TotalDurationSeconds = monthStats.Sum(s => s.TotalDurationSeconds),
                    AverageDurationSeconds = monthStats.Any() ? monthStats.Sum(s => s.TotalDurationSeconds) / monthStats.Sum(s => s.RestCount) : 0,
                    CompletedCount = monthStats.Sum(s => s.CompletedCount),
                    SkippedCount = monthStats.Sum(s => s.SkippedCount)
                };
            }
        }

        /// <summary>
        /// 获取最近N天的每日统计
        /// </summary>
        public List<DailyStatistics> GetRecentDailyStatistics(int days)
        {
            lock (_lock)
            {
                var endDate = DateTime.Now.Date;
                var startDate = endDate.AddDays(-days + 1);

                var result = new List<DailyStatistics>();
                for (var date = startDate; date <= endDate; date = date.AddDays(1))
                {
                    var stat = _data.DailyStats.FirstOrDefault(d => d.Date.Date == date);
                    result.Add(stat ?? new DailyStatistics { Date = date });
                }
                return result;
            }
        }

        /// <summary>
        /// 获取最近的历史记录
        /// </summary>
        public List<RestRecord> GetRecentRecords(int count = 100)
        {
            lock (_lock)
            {
                return _data.Records
                    .OrderByDescending(r => r.StartTime)
                    .Take(count)
                    .ToList();
            }
        }

        /// <summary>
        /// 异步保存统计数据
        /// </summary>
        public async Task SaveAsync()
        {
            try
            {
                var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await File.WriteAllTextAsync(_dataFilePath, json);
                _logService.Debug("统计数据已保存");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "保存统计数据失败");
            }
        }

        /// <summary>
        /// 异步加载统计数据
        /// </summary>
        public async Task LoadAsync()
        {
            try
            {
                if (!File.Exists(_dataFilePath))
                {
                    _logService.Info("统计数据文件不存在，使用默认数据");
                    return;
                }

                var json = await File.ReadAllTextAsync(_dataFilePath);
                var loadedData = JsonSerializer.Deserialize<StatisticsData>(json);
                if (loadedData != null)
                {
                    lock (_lock)
                    {
                        _data = loadedData;
                    }
                    _logService.Info($"统计数据已加载，共{_data.Records.Count}条记录");
                }
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "加载统计数据失败");
            }
        }
    }
}
