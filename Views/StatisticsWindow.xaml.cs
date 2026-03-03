using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using eyesharp.Services;

namespace eyesharp.Views
{
    /// <summary>
    /// StatisticsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class StatisticsWindow : Window
    {
        private readonly IStatisticsService _statisticsService;
        private readonly ILogService _logService;

        public StatisticsWindow(IStatisticsService statisticsService, ILogService logService)
        {
            _statisticsService = statisticsService;
            _logService = logService;

            InitializeComponent();

            Loaded += OnLoaded;
        }

        /// <summary>
        /// 窗口加载时刷新数据
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _logService.Info("统计窗口已加载");
            RefreshStatistics();
        }

        /// <summary>
        /// 刷新按钮点击
        /// </summary>
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _logService.Info("用户点击刷新统计");
            RefreshStatistics();
        }

        /// <summary>
        /// 刷新统计数据
        /// </summary>
        private void RefreshStatistics()
        {
            try
            {
                // 今日统计
                var todayStats = _statisticsService.GetTodayStatistics();
                TodayCountText.Text = todayStats.RestCount.ToString();
                TodayDurationText.Text = FormatDuration(todayStats.TotalDurationSeconds);
                TodayCompletedText.Text = todayStats.CompletedCount.ToString();
                TodaySkippedText.Text = todayStats.SkippedCount.ToString();

                // 本周统计
                var weekStats = _statisticsService.GetWeekStatistics();
                WeekCountText.Text = weekStats.RestCount.ToString();
                WeekDurationText.Text = FormatDuration(weekStats.TotalDurationSeconds);
                WeekCompletedText.Text = weekStats.CompletedCount.ToString();
                WeekSkippedText.Text = weekStats.SkippedCount.ToString();

                // 本月统计
                var monthStats = _statisticsService.GetMonthStatistics();
                MonthCountText.Text = monthStats.RestCount.ToString();
                MonthDurationText.Text = FormatDuration(monthStats.TotalDurationSeconds);
                MonthCompletedText.Text = monthStats.CompletedCount.ToString();
                MonthSkippedText.Text = monthStats.SkippedCount.ToString();

                // 最近记录
                var recentRecords = _statisticsService.GetRecentRecords(50);
                RecordsDataGrid.ItemsSource = recentRecords;

                _logService.Info("统计数据刷新完成");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "刷新统计数据失败");
                MessageBox.Show(
                    "刷新统计数据失败：" + ex.Message,
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// 格式化时长（秒转分钟/小时）
        /// </summary>
        private string FormatDuration(int seconds)
        {
            if (seconds < 60)
            {
                return $"{seconds}秒";
            }
            else if (seconds < 3600)
            {
                var minutes = seconds / 60;
                var remainingSeconds = seconds % 60;
                if (remainingSeconds > 0)
                {
                    return $"{minutes}分{remainingSeconds}秒";
                }
                return $"{minutes}分";
            }
            else
            {
                var hours = seconds / 3600;
                var remainingMinutes = (seconds % 3600) / 60;
                if (remainingMinutes > 0)
                {
                    return $"{hours}时{remainingMinutes}分";
                }
                return $"{hours}时";
            }
        }
    }

    /// <summary>
    /// 完成状态转换器
    /// </summary>
    public class IsCompletedConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isCompleted)
            {
                return isCompleted ? "✅ 正常完成" : "⏹️ 提前结束";
            }
            return "未知";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// 模式转换器
    /// </summary>
    public class IsForcedModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isForcedMode)
            {
                return isForcedMode ? "🔒 强制" : "🟢 普通";
            }
            return "未知";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
