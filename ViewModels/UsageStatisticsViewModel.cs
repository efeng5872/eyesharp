using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using eyesharp.Models;
using eyesharp.Services;
using eyesharp.Services.UsageStats;

namespace eyesharp.ViewModels
{
    /// <summary>
    /// 使用统计视图模型
    /// </summary>
    public partial class UsageStatisticsViewModel : ObservableObject
    {
        private readonly IUsageStatisticsService _usageStatsService;
        private readonly ILogService _logService;

        // 今日统计数据
        [ObservableProperty]
        private string _todayBootTime = "0小时0分";

        [ObservableProperty]
        private string _todayActiveTime = "0小时0分";

        [ObservableProperty]
        private string _todayIdleTime = "0小时0分";

        [ObservableProperty]
        private int _todayLockScreenCount;

        [ObservableProperty]
        private string _todayLockScreenTime = "0小时0分";

        [ObservableProperty]
        private int _todayKeyPressCount;

        [ObservableProperty]
        private string _todayMouseMoveDistance = "0米";

        [ObservableProperty]
        private int _todayMouseClickCount;

        [ObservableProperty]
        private int _todayMouseWheelCount;

        // 本周/本月统计
        [ObservableProperty]
        private string _weekActiveTime = "0小时0分";

        [ObservableProperty]
        private string _monthActiveTime = "0小时0分";

        // 24小时数据
        [ObservableProperty]
        private List<HourlyActivityRecord> _hourlyData = new();

        [ObservableProperty]
        private List<DailyUsageStatistics> _recentDailyData = new();

        // 当前视图
        [ObservableProperty]
        private int _selectedViewIndex = 0; // 0=今日, 1=本周, 2=本月

        // 刷新命令
        public ICommand RefreshCommand { get; }
        public ICommand ExportCommand { get; }

        public UsageStatisticsViewModel(IUsageStatisticsService usageStatsService, ILogService logService)
        {
            _usageStatsService = usageStatsService ?? throw new ArgumentNullException(nameof(usageStatsService));
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));

            RefreshCommand = new RelayCommand(async () => await RefreshAsync());
            ExportCommand = new RelayCommand(async () => await ExportAsync());

            // 初始加载
            _ = RefreshAsync();
        }

        /// <summary>
        /// 刷新数据
        /// </summary>
        public async Task RefreshAsync()
        {
            try
            {
                _logService.Debug("[UsageStatsVM] 刷新统计数据...");

                // 获取今日统计
                var todayStats = await _usageStatsService.GetTodayStatisticsAsync();
                UpdateTodayStats(todayStats);

                // 获取本周统计
                var weekStats = await _usageStatsService.GetWeekStatisticsAsync();
                WeekActiveTime = FormatDuration(weekStats.ActiveSeconds);

                // 获取本月统计
                var monthStats = await _usageStatsService.GetMonthStatisticsAsync();
                MonthActiveTime = FormatDuration(monthStats.ActiveSeconds);

                // 获取24小时数据
                var endDate = DateTime.Now;
                var startDate = endDate.Date;
                HourlyData = await _usageStatsService.GetHourlyDataAsync(startDate, endDate);

                // 获取最近7天数据
                RecentDailyData = await _usageStatsService.GetRecentDailyStatisticsAsync(7);

                _logService.Debug("[UsageStatsVM] 统计数据刷新完成");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "[UsageStatsVM] 刷新统计数据失败");
            }
        }

        /// <summary>
        /// 导出数据
        /// </summary>
        private async Task ExportAsync()
        {
            try
            {
                var endDate = DateTime.Now;
                var startDate = endDate.AddDays(-7);
                var filePath = await _usageStatsService.ExportToCsvAsync(startDate, endDate);

                _logService.Info($"[UsageStatsVM] 数据已导出: {filePath}");

                // 打开文件所在目录
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "[UsageStatsVM] 导出数据失败");
            }
        }

        /// <summary>
        /// 更新今日统计显示
        /// </summary>
        private void UpdateTodayStats(DailyUsageStatistics stats)
        {
            TodayBootTime = FormatDuration(stats.TotalBootSeconds);
            TodayActiveTime = FormatDuration(stats.ActiveSeconds);
            TodayIdleTime = FormatDuration(stats.IdleSeconds);
            TodayLockScreenCount = stats.LockScreenCount;
            TodayLockScreenTime = FormatDuration(stats.LockScreenSeconds);

            TodayKeyPressCount = stats.TotalKeyPressCount;
            TodayMouseMoveDistance = $"{stats.MouseMoveDistanceMeters:F2}米";
            TodayMouseClickCount = stats.TotalMouseLeftClickCount +
                                   stats.TotalMouseRightClickCount +
                                   stats.TotalMouseMiddleClickCount;
            TodayMouseWheelCount = stats.TotalMouseWheelScrollCount;
        }

        /// <summary>
        /// 格式化时长（秒 -> 小时分钟）
        /// </summary>
        private static string FormatDuration(int totalSeconds)
        {
            var hours = totalSeconds / 3600;
            var minutes = (totalSeconds % 3600) / 60;

            if (hours > 0)
            {
                return $"{hours}小时{minutes}分";
            }
            else
            {
                return $"{minutes}分";
            }
        }

        /// <summary>
        /// 获取指定小时的数据（用于绑定）
        /// </summary>
        public HourlyActivityRecord? GetHourlyRecord(int hour)
        {
            return HourlyData.FirstOrDefault(h => h.Hour.Hour == hour);
        }

        /// <summary>
        /// 获取活跃度百分比（用于热力图颜色）
        /// </summary>
        public int GetActivityPercentage(int hour)
        {
            var record = GetHourlyRecord(hour);
            if (record == null) return 0;

            var totalSeconds = record.ActiveSeconds + record.IdleSeconds + record.LockScreenSeconds;
            if (totalSeconds == 0) return 0;

            return (int)((double)record.ActiveSeconds / totalSeconds * 100);
        }
    }
}
