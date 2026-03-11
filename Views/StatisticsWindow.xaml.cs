using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using eyesharp.Models;
using eyesharp.Services;
using eyesharp.Services.UsageStats;

namespace eyesharp.Views
{
    /// <summary>
    /// StatisticsWindow.xaml 的交互逻辑
    /// </summary>
    public partial class StatisticsWindow : Window
    {
        private readonly IStatisticsService _statisticsService;
        private readonly IUsageStatisticsService _usageStatsService;
        private readonly ILogService _logService;
        private readonly IThemeService _themeService;

        // 分页相关
        private int _currentPage = 1;
        private const int PageSize = 20;
        private int _totalCount = 0;
        private DateTime _filterStartDate;
        private DateTime _filterEndDate;

        public StatisticsWindow(
            IStatisticsService statisticsService,
            IUsageStatisticsService usageStatsService,
            ILogService logService,
            IThemeService themeService)
        {
            _statisticsService = statisticsService;
            _usageStatsService = usageStatsService;
            _logService = logService;
            _themeService = themeService;

            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// 窗口卸载时取消订阅事件
        /// </summary>
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_themeService != null)
            {
                _themeService.ThemeChanged -= OnThemeChanged;
            }
        }

        /// <summary>
        /// 主题变更时刷新DataGrid样式
        /// </summary>
        private void OnThemeChanged(object? sender, ThemeChangedEventArgs e)
        {
            _logService.Info($"统计窗口响应主题变更: {e.NewTheme}");
            // DynamicResource会自动更新，但需要强制刷新DataGrid
            RecordsDataGrid?.Items?.Refresh();
        }

        /// <summary>
        /// 窗口加载时刷新数据
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _logService.Info("统计窗口已加载");

            // 订阅主题变更事件
            if (_themeService != null)
            {
                _themeService.ThemeChanged += OnThemeChanged;
            }

            // 初始化日期筛选器（默认本周）
            InitializeDateFilter();

            RefreshStatistics();
        }

        /// <summary>
        /// 初始化日期筛选器
        /// </summary>
        private void InitializeDateFilter()
        {
            // 默认显示本周数据（周一到周日）
            SetWeekFilter();
        }

        /// <summary>
        /// 设置本周筛选（周一为起始）
        /// </summary>
        private void SetWeekFilter()
        {
            var today = DateTime.Now.Date;
            // 中国习惯：周一为一周开始 (DayOfWeek: Sunday=0, Monday=1, ...)
            var daysSinceMonday = (int)today.DayOfWeek - 1;
            if (daysSinceMonday < 0) daysSinceMonday = 6; // 周日时，回退6天到上周一
            _filterStartDate = today.AddDays(-daysSinceMonday);
            _filterEndDate = today;

            StartDatePicker.SelectedDate = _filterStartDate;
            EndDatePicker.SelectedDate = _filterEndDate;
            _currentPage = 1;

            _logService.Info($"设置本周筛选：{_filterStartDate:yyyy-MM-dd} 至 {_filterEndDate:yyyy-MM-dd}");
        }

        /// <summary>
        /// 设置本月筛选
        /// </summary>
        private void SetMonthFilter()
        {
            var today = DateTime.Now.Date;
            _filterStartDate = new DateTime(today.Year, today.Month, 1);
            _filterEndDate = today;

            StartDatePicker.SelectedDate = _filterStartDate;
            EndDatePicker.SelectedDate = _filterEndDate;
            _currentPage = 1;

            _logService.Info($"设置本月筛选：{_filterStartDate:yyyy-MM-dd} 至 {_filterEndDate:yyyy-MM-dd}");
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
                // 护眼统计
                RefreshEyeProtectionStats();

                // 电脑使用统计
                RefreshUsageStats();

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
        /// 刷新护眼统计数据
        /// </summary>
        private void RefreshEyeProtectionStats()
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

            // 加载分页数据
            LoadPagedRecords();
        }

        /// <summary>
        /// 加载分页记录
        /// </summary>
        private void LoadPagedRecords()
        {
            try
            {
                var records = _statisticsService.GetRecordsByDateRange(
                    _filterStartDate, _filterEndDate, _currentPage, PageSize, out _totalCount);

                RecordsDataGrid.ItemsSource = records;
                UpdatePaginationControls();

                _logService.Info($"加载第 {_currentPage} 页记录，共 {_totalCount} 条");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "加载分页记录失败");
            }
        }

        /// <summary>
        /// 更新分页控件状态
        /// </summary>
        private void UpdatePaginationControls()
        {
            var totalPages = (int)Math.Ceiling((double)_totalCount / PageSize);
            if (totalPages < 1) totalPages = 1;

            PaginationInfoText.Text = $"共 {_totalCount} 条记录";
            PageNumberText.Text = $"第 {_currentPage} / {totalPages} 页";

            PrevPageButton.IsEnabled = _currentPage > 1;
            NextPageButton.IsEnabled = _currentPage < totalPages;
        }

        /// <summary>
        /// 上一页按钮点击
        /// </summary>
        private void PrevPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                LoadPagedRecords();
            }
        }

        /// <summary>
        /// 下一页按钮点击
        /// </summary>
        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            var totalPages = (int)Math.Ceiling((double)_totalCount / PageSize);
            if (_currentPage < totalPages)
            {
                _currentPage++;
                LoadPagedRecords();
            }
        }

        /// <summary>
        /// 日期选择器变更
        /// </summary>
        private void DatePicker_SelectedDateChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (StartDatePicker.SelectedDate.HasValue && EndDatePicker.SelectedDate.HasValue)
            {
                _filterStartDate = StartDatePicker.SelectedDate.Value;
                _filterEndDate = EndDatePicker.SelectedDate.Value;
                _currentPage = 1;
                LoadPagedRecords();
            }
        }

        /// <summary>
        /// 本周筛选按钮点击
        /// </summary>
        private void WeekFilterButton_Click(object sender, RoutedEventArgs e)
        {
            SetWeekFilter();
            LoadPagedRecords();
        }

        /// <summary>
        /// 本月筛选按钮点击
        /// </summary>
        private void MonthFilterButton_Click(object sender, RoutedEventArgs e)
        {
            SetMonthFilter();
            LoadPagedRecords();
        }

        /// <summary>
        /// 刷新电脑使用统计数据
        /// </summary>
        private async void RefreshUsageStats()
        {
            if (_usageStatsService == null)
            {
                _logService.Warn("使用统计服务未注入，跳过电脑使用统计加载");
                return;
            }

            try
            {
                // 今日统计
                var todayStats = await _usageStatsService.GetTodayStatisticsAsync();
                TodayBootTimeText.Text = FormatDurationHoursMinutes(todayStats.TotalBootSeconds);
                TodayActiveTimeText.Text = FormatDurationHoursMinutes(todayStats.ActiveSeconds);
                TodayIdleTimeText.Text = FormatDurationHoursMinutes(todayStats.IdleSeconds);
                TodayLockScreenCountText.Text = todayStats.LockScreenCount.ToString();

                // 输入统计
                TodayKeyPressCountText.Text = todayStats.TotalKeyPressCount.ToString("N0");
                TodayMouseMoveText.Text = $"{todayStats.MouseMoveDistanceMeters:F1}米";
                var totalClicks = todayStats.TotalMouseLeftClickCount +
                                  todayStats.TotalMouseRightClickCount +
                                  todayStats.TotalMouseMiddleClickCount;
                TodayMouseClickText.Text = totalClicks.ToString("N0");
                TodayMouseWheelText.Text = todayStats.TotalMouseWheelScrollCount.ToString("N0");

                // 24小时热力图
                await BuildHourlyHeatmapAsync();

                // 本周概览
                var weekStats = await _usageStatsService.GetWeekStatisticsAsync();
                WeekActiveTimeText.Text = FormatDurationHoursMinutes(weekStats.ActiveSeconds);
                var daysWithData = Math.Max(1, (DateTime.Now - weekStats.Date).Days + 1);
                var avgDailySeconds = weekStats.ActiveSeconds / daysWithData;
                WeekAvgDailyText.Text = FormatDurationHoursMinutes(avgDailySeconds);
                var weekTotalInput = weekStats.TotalKeyPressCount +
                                     weekStats.TotalMouseLeftClickCount +
                                     weekStats.TotalMouseRightClickCount +
                                     weekStats.TotalMouseMiddleClickCount;
                WeekTotalInputText.Text = weekTotalInput.ToString("N0");

                // 最近7天数据
                var recentDailyData = await _usageStatsService.GetRecentDailyStatisticsAsync(7);
                LoadDailyUsageDataGrid(recentDailyData);

                _logService.Info("电脑使用统计数据刷新完成");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "刷新电脑使用统计数据失败");
            }
        }

        /// <summary>
        /// 构建24小时热力图
        /// </summary>
        private async Task BuildHourlyHeatmapAsync()
        {
            try
            {
                HourlyHeatmapGrid.Children.Clear();

                var now = DateTime.Now;
                var startOfDay = now.Date;
                var hourlyData = await _usageStatsService.GetHourlyDataAsync(startOfDay, now);

                // 创建24小时的热力图（4行 x 6列）
                for (int hour = 0; hour < 24; hour++)
                {
                    var row = hour / 6;
                    var col = hour % 6;

                    var record = hourlyData.FirstOrDefault(r => r.Hour.Hour == hour);
                    var activityPercentage = GetActivityPercentage(record);

                    var cell = new Border
                    {
                        Style = (Style)FindResource("HeatmapCellStyle"),
                        Background = GetHeatmapColor(activityPercentage),
                        ToolTip = $"{hour:00}:00 - {hour:59}:59\n活跃度: {activityPercentage}%\n" +
                                  $"活跃: {record?.ActiveSeconds ?? 0}秒\n空闲: {record?.IdleSeconds ?? 0}秒\n锁屏: {record?.LockScreenSeconds ?? 0}秒"
                    };

                    var timeText = new TextBlock
                    {
                        Text = $"{hour:00}",
                        FontSize = 11,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = activityPercentage > 50 ? Brushes.White : Brushes.Black
                    };

                    cell.Child = timeText;
                    Grid.SetRow(cell, row);
                    Grid.SetColumn(cell, col);
                    HourlyHeatmapGrid.Children.Add(cell);
                }
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "构建热力图失败");
            }
        }

        /// <summary>
        /// 获取活跃度百分比
        /// </summary>
        private int GetActivityPercentage(HourlyActivityRecord record)
        {
            if (record == null) return 0;

            var totalSeconds = record.ActiveSeconds + record.IdleSeconds + record.LockScreenSeconds;
            if (totalSeconds == 0) return 0;

            return (int)((double)record.ActiveSeconds / totalSeconds * 100);
        }

        /// <summary>
        /// 获取热力图颜色
        /// </summary>
        private Brush GetHeatmapColor(int activityPercentage)
        {
            // 使用蓝色渐变：从浅蓝到深蓝
            return activityPercentage switch
            {
                0 => new SolidColorBrush(Color.FromRgb(227, 242, 253)),     // #E3F2FD 最浅
                <= 25 => new SolidColorBrush(Color.FromRgb(187, 222, 251)), // #BBDEFB
                <= 50 => new SolidColorBrush(Color.FromRgb(144, 202, 249)), // #90CAF9
                <= 75 => new SolidColorBrush(Color.FromRgb(66, 165, 245)),  // #42A5F5
                _ => new SolidColorBrush(Color.FromRgb(21, 101, 192))      // #1565C0 最深
            };
        }

        /// <summary>
        /// 加载每日使用数据表格
        /// </summary>
        private void LoadDailyUsageDataGrid(List<DailyUsageStatistics> dailyData)
        {
            var displayData = dailyData.Select(d => new
            {
                d.Date,
                BootTime = FormatDurationHoursMinutes(d.TotalBootSeconds),
                ActiveTime = FormatDurationHoursMinutes(d.ActiveSeconds),
                LockScreenCount = d.LockScreenCount,
                KeyPressCount = d.TotalKeyPressCount.ToString("N0"),
                MouseMove = $"{d.MouseMoveDistanceMeters:F1}米"
            }).ToList();

            DailyUsageDataGrid.ItemsSource = displayData;
        }

        /// <summary>
        /// 格式化为小时分钟格式
        /// </summary>
        private string FormatDurationHoursMinutes(int totalSeconds)
        {
            var hours = totalSeconds / 3600;
            var minutes = (totalSeconds % 3600) / 60;

            if (hours > 0)
            {
                return $"{hours}小时{minutes}分";
            }
            return $"{minutes}分";
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
