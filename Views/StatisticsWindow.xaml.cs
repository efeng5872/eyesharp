using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
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
        private readonly IMouseDistanceConverterService _mouseDistanceConverterService;
        private readonly DispatcherTimer _usageAutoRefreshTimer;
        private readonly bool _usageStatisticsEnabled;
        private bool _isRefreshing;
        private bool _pendingRefresh;
        private DailyUsageStatistics? _lastDisplayedUsageStats;

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
            IThemeService themeService,
            IMouseDistanceConverterService mouseDistanceConverterService)
        {
            _statisticsService = statisticsService;
            _usageStatsService = usageStatsService;
            _logService = logService;
            _themeService = themeService;
            _mouseDistanceConverterService = mouseDistanceConverterService;

            _usageAutoRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(12)
            };
            _usageAutoRefreshTimer.Tick += UsageAutoRefreshTimer_Tick;

            _usageStatisticsEnabled = App.CurrentConfig.EnableUsageStatistics;

            InitializeComponent();

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        /// <summary>
        /// 窗口卸载时取消订阅事件
        /// </summary>
        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            _usageAutoRefreshTimer.Stop();
            _isRefreshing = false;
            _pendingRefresh = false;

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

            _ = TriggerRefreshAsync("窗口加载");
            _usageAutoRefreshTimer.Start();
            _logService.Info("统计窗口自动刷新已启动，间隔12秒");

            UsageDisabledHintText.Visibility = _usageStatisticsEnabled ? Visibility.Collapsed : Visibility.Visible;
            UpdateTodayOverviewCardLayout();
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
        /// 设置本日筛选
        /// </summary>
        private void SetDayFilter()
        {
            var today = DateTime.Now.Date;
            _filterStartDate = today;
            _filterEndDate = today;

            StartDatePicker.SelectedDate = _filterStartDate;
            EndDatePicker.SelectedDate = _filterEndDate;
            _currentPage = 1;

            _logService.Info($"设置本日筛选：{_filterStartDate:yyyy-MM-dd}");
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
        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _logService.Info("用户点击刷新统计");
            await TriggerRefreshAsync("手动刷新");
        }

        /// <summary>
        /// 刷新统计数据
        /// </summary>
        private async Task RefreshStatisticsAsync()
        {
            // 护眼统计
            RefreshEyeProtectionStats();

            // 电脑使用统计
            await RefreshUsageStatsAsync();

            _logService.Info("统计数据刷新完成");
        }

        private async Task TriggerRefreshAsync(string triggerSource)
        {
            if (_isRefreshing)
            {
                if (triggerSource == "手动刷新")
                {
                    _pendingRefresh = true;
                    _logService.Info("统计刷新进行中，已标记手动补刷一次");
                }
                else
                {
                    _logService.Debug($"统计刷新进行中，跳过触发源: {triggerSource}");
                }
                return;
            }

            _isRefreshing = true;
            try
            {
                if (triggerSource == "手动刷新" || triggerSource == "手动补刷")
                {
                    _usageStatsService?.CaptureSnapshotNow();
                    _logService.Debug($"手动刷新前置采样完成，触发源: {triggerSource}");
                }

                await RefreshStatisticsAsync();
            }
            catch (Exception ex)
            {
                _logService.Error(ex, $"刷新统计数据失败，触发源: {triggerSource}");
                MessageBox.Show(
                    "刷新统计数据失败：" + ex.Message,
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
            finally
            {
                _isRefreshing = false;
            }

            if (_pendingRefresh)
            {
                _pendingRefresh = false;
                _logService.Info("执行手动补刷");
                await TriggerRefreshAsync("手动补刷");
            }
        }

        private async void UsageAutoRefreshTimer_Tick(object? sender, EventArgs e)
        {
            _logService.Debug("统计窗口自动刷新触发");
            await TriggerRefreshAsync("自动刷新");
        }

        private void TodayOverviewHost_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateTodayOverviewCardLayout();
        }

        private void DailyUsageDataGrid_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (UsageTabScrollViewer == null)
            {
                return;
            }

            e.Handled = true;
            UsageTabScrollViewer.ScrollToVerticalOffset(UsageTabScrollViewer.VerticalOffset - e.Delta);
        }

        private void RecordsDataGrid_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (EyeTabScrollViewer == null)
            {
                return;
            }

            e.Handled = true;
            EyeTabScrollViewer.ScrollToVerticalOffset(EyeTabScrollViewer.VerticalOffset - e.Delta);
        }

        private void UpdateTodayOverviewCardLayout()
        {
            if (TodayOverviewHost == null || TodayOverviewPanel == null)
            {
                return;
            }

            var hostWidth = TodayOverviewHost.ActualWidth;
            var columns = hostWidth >= 980 ? 5 : 3;
            var cardWidth = columns == 5 ? 172 : 220;

            TodayBootTimeCard.Width = cardWidth;
            TodayActiveTimeCard.Width = cardWidth;
            TodayIdleTimeCard.Width = cardWidth;
            TodayLockScreenCountCard.Width = cardWidth;
            TodayLockScreenTimeCard.Width = cardWidth;
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
        /// 本日筛选按钮点击
        /// </summary>
        private void DayFilterButton_Click(object sender, RoutedEventArgs e)
        {
            SetDayFilter();
            LoadPagedRecords();
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
        private async Task RefreshUsageStatsAsync()
        {
            if (_usageStatsService == null)
            {
                _logService.Warn("使用统计服务未注入，跳过电脑使用统计加载");
                return;
            }

            // 今日统计
            var todayStats = await _usageStatsService.GetTodayStatisticsAsync();
            TodayBootTimeText.Text = FormatDurationHoursMinutes(todayStats.TotalBootSeconds);
            TodayActiveTimeText.Text = FormatDurationHoursMinutes(todayStats.ActiveSeconds);
            TodayIdleTimeText.Text = FormatDurationHoursMinutes(todayStats.IdleSeconds);
            TodayLockScreenCountText.Text = todayStats.LockScreenCount.ToString();
            TodayLockScreenTimeText.Text = FormatDurationHoursMinutes(todayStats.LockScreenSeconds);

            // 输入统计
            TodayKeyPressCountText.Text = todayStats.TotalKeyPressCount.ToString("N0");
            var todayMouseMoveMeters = _mouseDistanceConverterService.ConvertPixelsToMeters(todayStats.TotalMouseMoveDistance);
            TodayMouseMoveText.Text = $"{todayMouseMoveMeters:F1}米";
            var totalClicks = todayStats.TotalMouseLeftClickCount +
                              todayStats.TotalMouseRightClickCount +
                              todayStats.TotalMouseMiddleClickCount;
            TodayMouseClickText.Text = totalClicks.ToString("N0");
            TodayMouseWheelText.Text = todayStats.TotalMouseWheelScrollCount.ToString("N0");

            var realTimeStatus = await _usageStatsService.GetRealTimeStatusAsync();
            UpdateUsagePerceivedInfo(todayStats, realTimeStatus.CurrentState);

            // 24小时活跃柱状图
            await BuildHourlyBarChartAsync();

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

        private void UpdateUsagePerceivedInfo(DailyUsageStatistics todayStats, eyesharp.Models.UserActivityState currentState)
        {
            UsageLastUpdatedText.Text = $"最后更新时间：{DateTime.Now:yyyy-MM-dd HH:mm:ss}";

            UsageSamplingStatusText.Text = currentState switch
            {
                eyesharp.Models.UserActivityState.Active => "采样状态：采样中（输入持续累计）",
                eyesharp.Models.UserActivityState.Idle => "采样状态：当前状态 Idle",
                eyesharp.Models.UserActivityState.Locked => "采样状态：当前状态 Locked",
                _ => "采样状态：当前状态 Unknown"
            };

            if (_lastDisplayedUsageStats == null)
            {
                UsageDeltaKeyText.Text = "按键 +0";
                UsageDeltaClickText.Text = "鼠标点击 +0";
                UsageDeltaWheelText.Text = "滚轮 +0";
                UsageDeltaMoveText.Text = "指针移动 +0.0米";
            }
            else
            {
                var keyDelta = Math.Max(0, todayStats.TotalKeyPressCount - _lastDisplayedUsageStats.TotalKeyPressCount);
                var clickDelta = Math.Max(0,
                    (todayStats.TotalMouseLeftClickCount + todayStats.TotalMouseRightClickCount + todayStats.TotalMouseMiddleClickCount)
                    - (_lastDisplayedUsageStats.TotalMouseLeftClickCount + _lastDisplayedUsageStats.TotalMouseRightClickCount + _lastDisplayedUsageStats.TotalMouseMiddleClickCount));
                var wheelDelta = Math.Max(0, todayStats.TotalMouseWheelScrollCount - _lastDisplayedUsageStats.TotalMouseWheelScrollCount);
                var moveDeltaPixels = Math.Max(0, todayStats.TotalMouseMoveDistance - _lastDisplayedUsageStats.TotalMouseMoveDistance);
                var moveDeltaMeters = _mouseDistanceConverterService.ConvertPixelsToMeters(moveDeltaPixels);

                UsageDeltaKeyText.Text = $"按键 +{keyDelta:N0}";
                UsageDeltaClickText.Text = $"鼠标点击 +{clickDelta:N0}";
                UsageDeltaWheelText.Text = $"滚轮 +{wheelDelta:N0}";
                UsageDeltaMoveText.Text = $"指针移动 +{moveDeltaMeters:F1}米";
            }

            _lastDisplayedUsageStats = new DailyUsageStatistics
            {
                TotalKeyPressCount = todayStats.TotalKeyPressCount,
                TotalMouseLeftClickCount = todayStats.TotalMouseLeftClickCount,
                TotalMouseRightClickCount = todayStats.TotalMouseRightClickCount,
                TotalMouseMiddleClickCount = todayStats.TotalMouseMiddleClickCount,
                TotalMouseWheelScrollCount = todayStats.TotalMouseWheelScrollCount,
                TotalMouseMoveDistance = todayStats.TotalMouseMoveDistance
            };
        }

        /// <summary>
        /// 构建24小时堆叠柱状图（活跃/空闲/锁屏）
        /// </summary>
        private async Task BuildHourlyBarChartAsync()
        {
            try
            {
                HourlyBarChartGrid.Children.Clear();
                HourlyBarChartGrid.ColumnDefinitions.Clear();
                HourlyBarChartGrid.RowDefinitions.Clear();

                var now = DateTime.Now;
                var startOfDay = now.Date;
                var hourlyData = await _usageStatsService.GetHourlyDataAsync(startOfDay, now);

                // 行：0=柱状区域，1=小时标签
                HourlyBarChartGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                HourlyBarChartGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var currentHour = DateTime.Now.Hour;

                for (int hour = 0; hour < 24; hour++)
                {
                    HourlyBarChartGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var record = hourlyData.FirstOrDefault(r => r.Hour.Hour == hour);
                    var activeSeconds = Math.Max(0, record?.ActiveSeconds ?? 0);
                    var idleSeconds = Math.Max(0, record?.IdleSeconds ?? 0);
                    var lockedSeconds = Math.Max(0, record?.LockScreenSeconds ?? 0);
                    var totalSeconds = activeSeconds + idleSeconds + lockedSeconds;
                    if (totalSeconds <= 0)
                    {
                        totalSeconds = 1;
                    }

                    var activeStar = activeSeconds / (double)totalSeconds;
                    var idleStar = idleSeconds / (double)totalSeconds;
                    var lockedStar = lockedSeconds / (double)totalSeconds;

                    var barContainer = new Grid
                    {
                        Height = 120,
                        VerticalAlignment = VerticalAlignment.Bottom,
                        Margin = new Thickness(1, 0, 1, 0),
                        ToolTip = $"{hour:00}:00 - {hour:59}:59\n活跃: {activeSeconds / 60.0:F1}分\n空闲: {idleSeconds / 60.0:F1}分\n锁屏: {lockedSeconds / 60.0:F1}分"
                    };

                    barContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(lockedStar, GridUnitType.Star) });
                    barContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(idleStar, GridUnitType.Star) });
                    barContainer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(activeStar, GridUnitType.Star) });

                    var lockedBar = new Border { Background = new SolidColorBrush(Color.FromRgb(251, 140, 0)) };
                    var idleBar = new Border { Background = new SolidColorBrush(Color.FromRgb(144, 164, 174)) };
                    var activeBar = new Border { Background = new SolidColorBrush(Color.FromRgb(67, 160, 71)) };

                    Grid.SetRow(lockedBar, 0);
                    Grid.SetRow(idleBar, 1);
                    Grid.SetRow(activeBar, 2);
                    barContainer.Children.Add(lockedBar);
                    barContainer.Children.Add(idleBar);
                    barContainer.Children.Add(activeBar);

                    Grid.SetRow(barContainer, 0);
                    Grid.SetColumn(barContainer, hour);
                    HourlyBarChartGrid.Children.Add(barContainer);

                    if (hour % 2 == 0)
                    {
                        var label = new TextBlock
                        {
                            Text = hour.ToString("00"),
                            FontSize = 10,
                            FontWeight = hour == currentHour ? FontWeights.SemiBold : FontWeights.Normal,
                            Foreground = hour == currentHour
                                ? new SolidColorBrush(Color.FromRgb(25, 118, 210))
                                : (Brush)FindResource("TextSecondaryBrush"),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 4, 0, 0)
                        };
                        Grid.SetRow(label, 1);
                        Grid.SetColumn(label, hour);
                        HourlyBarChartGrid.Children.Add(label);
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "构建24小时柱状图失败");
            }
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
                MouseMove = $"{_mouseDistanceConverterService.ConvertPixelsToMeters(d.TotalMouseMoveDistance):F1}米"
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
