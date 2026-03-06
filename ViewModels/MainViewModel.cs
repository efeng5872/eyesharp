using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using WinForms = System.Windows.Forms;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using eyesharp.Models;
using eyesharp.Services;
using eyesharp.Views;
using eyesharp.Helpers;

namespace eyesharp.ViewModels
{
    /// <summary>
    /// 主窗口 ViewModel
    /// </summary>
    public partial class MainViewModel : ObservableObject, IDisposable
    {
        private readonly IConfigService _configService;
        private readonly ILogService _logService;
        private readonly ITimerService _timerService;
        private readonly IPasswordService _passwordService;
        private readonly IStatisticsService _statisticsService;
        private readonly IThemeService _themeService;
        private AppConfig _config;
        private RestWindow? _currentRestWindow;
        private WinForms.NotifyIcon? _trayIcon;
        private WinForms.ToolStripMenuItem? _trayPauseResumeItem;
        private bool _isRestCompleted = false;

        // 托盘图标资源
        private System.Drawing.Icon? _trayIconNormal;
        private System.Drawing.Icon? _trayIconPaused;

        [ObservableProperty]
        private string _statusText = "初始化中...";

        [ObservableProperty]
        private string _countdownText = "00:00";

        [ObservableProperty]
        private double _countdownProgress = 0;

        [ObservableProperty]
        private string _pauseResumeButtonText = "暂停提醒";

        [ObservableProperty]
        private bool _isSettingsEnabled = true;

        [ObservableProperty]
        private int _restIntervalMinutes = 30;

        [ObservableProperty]
        private int _restDurationSeconds = 20;

        [ObservableProperty]
        private bool _isForcedMode = true;

        [ObservableProperty]
        private bool _isBlackMode = true;

        [ObservableProperty]
        private bool _isImageMode = false;

        [ObservableProperty]
        private string _customImagePath = "";

        [ObservableProperty]
        private bool _autoStart = false;

        [ObservableProperty]
        private bool _showInTray = true;

        [ObservableProperty]
        private string _versionInfo = "EyeSharp v1.0.0";

        // 日志级别选项
        public string[] LogLevelOptions { get; } = { "DEBUG", "INFO", "WARN", "ERROR" };

        // 锁屏相关状态
        private bool _wasPausedDueToLockScreen = false;

        [ObservableProperty]
        private string _selectedLogLevel = "INFO";

        [ObservableProperty]
        private bool _isPreReminderEnabled = true;

        [ObservableProperty]
        private bool _isDarkTheme = false;

        [ObservableProperty]
        private string _themeButtonText = "🌙 深色";

        [ObservableProperty]
        private string _themeButtonIcon = "🌙";

        [ObservableProperty]
        private string _themeButtonShortText = "深色";

        // 锁屏处理行为
        [ObservableProperty]
        private bool _isLockScreenBehaviorNormal = true;

        [ObservableProperty]
        private bool _isLockScreenBehaviorPause = false;

        [ObservableProperty]
        private bool _isLockScreenBehaviorSkip = false;

        // Toast通知属性
        [ObservableProperty]
        private System.Windows.Visibility _toastVisibility = System.Windows.Visibility.Collapsed;

        [ObservableProperty]
        private double _toastOpacity = 0;

        [ObservableProperty]
        private string _toastMessage = "";

        private System.Windows.Threading.DispatcherTimer? _toastTimer;

        public MainViewModel(IConfigService configService, ILogService logService, ITimerService timerService, IPasswordService passwordService, IStatisticsService statisticsService, IThemeService themeService, AppConfig config)
        {
            _configService = configService;
            _logService = logService;
            _timerService = timerService;
            _passwordService = passwordService;
            _statisticsService = statisticsService;
            _themeService = themeService;
            _config = config;

            // 更新UI属性
            RestIntervalMinutes = _config.RestIntervalMinutes;
            RestDurationSeconds = _config.RestDurationSeconds;
            IsForcedMode = _config.IsForcedMode;
            IsBlackMode = _config.RestWindowMode == "black";
            IsImageMode = _config.RestWindowMode == "image";
            CustomImagePath = _config.CustomImagePath;

            // 初始化日志级别（从配置读取，默认INFO）
            SelectedLogLevel = string.IsNullOrEmpty(_config.LogLevel) ? "INFO" : _config.LogLevel.ToUpper();

            // 初始化预提醒设置
            IsPreReminderEnabled = _config.IsPreReminderEnabled;

            // 初始化主题设置
            IsDarkTheme = _config.Theme == "dark";
            ThemeButtonText = IsDarkTheme ? "☀️ 浅色" : "🌙 深色";
            ThemeButtonIcon = IsDarkTheme ? "☀️" : "🌙";
            ThemeButtonShortText = IsDarkTheme ? "浅色" : "深色";

            // 初始化锁屏处理行为
            InitializeLockScreenBehavior();

            // 同步开机自启动状态（以注册表实际状态为准）
            AutoStart = AutoStartHelper.IsAutoStartEnabled();
            if (AutoStart != _config.AutoStart)
            {
                // 如果注册表状态与配置不一致，更新配置
                _config.AutoStart = AutoStart;
                _ = _configService.SaveConfigAsync(_config);
                _logService.Info("开机自启动状态已同步到配置");
            }

            // 订阅定时器事件
            _timerService.StateChanged += OnTimerStateChanged;
            _timerService.MainCountdownTick += OnMainCountdownTick;
            _timerService.MainCountdownElapsed += OnMainCountdownElapsed;
            _timerService.RestCountdownTick += OnRestCountdownTick;
            _timerService.RestCountdownElapsed += OnRestCountdownElapsed;
            _timerService.PreReminder += OnPreReminder;

            // 订阅系统会话切换事件（用于检测锁屏/解锁）
            SystemEvents.SessionSwitch += OnSessionSwitch;

            // 配置预提醒
            if (_timerService is TimerService concreteTimerService)
            {
                concreteTimerService.IsPreReminderEnabled = _config.IsPreReminderEnabled;
                concreteTimerService.PreReminderIntervals = _config.PreReminderIntervals;
            }

            StatusText = "运行中";
            _logService.Info("配置加载成功");

            // 注意：不在构造函数中启动倒计时，而是在 MainWindow.Loaded 事件中启动
            // 这样可以确保 Application.Current.Dispatcher 已经准备好

            // 初始化系统托盘图标
            InitializeTrayIcon();

            // 加载统计数据
            _ = _statisticsService.LoadAsync();
        }

        /// <summary>
        /// 启动倒计时（公共方法，供外部调用）
        /// </summary>
        public void StartCountdown()
        {
            _logService.Info("StartCountdown() 方法被调用");
            var duration = TimeSpan.FromMinutes(_config.RestIntervalMinutes);
            _timerService.StartMainCountdown(duration);
            _logService.Info($"倒计时已启动：{_config.RestIntervalMinutes}分钟");
        }

        /// <summary>
        /// 定时器状态变化处理
        /// </summary>
        private void OnTimerStateChanged(object? sender, TimerStateChangedEventArgs e)
        {
            // 在UI线程中更新
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    switch (e.NewState)
                    {
                        case TimerState.Running:
                            StatusText = "运行中";
                            PauseResumeButtonText = "暂停提醒";
                            // 同步托盘图标
                            UpdateTrayIconState(false);
                            break;

                        case TimerState.Paused:
                            StatusText = "已暂停";
                            PauseResumeButtonText = "恢复提醒";
                            // 同步托盘图标
                            UpdateTrayIconState(true);
                            break;

                        case TimerState.Resting:
                            StatusText = "休息中";
                            PauseResumeButtonText = "休息中";
                            break;
                    }
                });
            }
        }

        /// <summary>
        /// 主倒计时Tick处理
        /// </summary>
        private void OnMainCountdownTick(object? sender, TimerTickEventArgs e)
        {
            // 在UI线程中更新
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    CountdownText = e.FormattedTime;

                    // 计算进度百分比
                    var totalSeconds = _config.RestIntervalMinutes * 60;
                    if (totalSeconds > 0)
                    {
                        var progress = (totalSeconds - e.RemainingSeconds) / (double)totalSeconds * 100;
                        CountdownProgress = Math.Min(100, Math.Max(0, progress));
                    }
                });
            }
        }

        /// <summary>
        /// 主倒计时结束处理（触发休息窗口）
        /// </summary>
        private void OnMainCountdownElapsed(object? sender, EventArgs e)
        {
            // 在UI线程中处理
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _logService.Info("主倒计时结束，显示休息窗口");
                    ShowRestWindow();
                });
            }
        }

        /// <summary>
        /// 休息倒计时Tick处理
        /// </summary>
        private void OnRestCountdownTick(object? sender, TimerTickEventArgs e)
        {
            // 在UI线程中更新休息窗口倒计时
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _currentRestWindow?.UpdateCountdown(e.RemainingSeconds);
                });
            }
        }

        /// <summary>
        /// 休息倒计时结束处理（关闭休息窗口，继续主倒计时）
        /// </summary>
        private void OnRestCountdownElapsed(object? sender, EventArgs e)
        {
            _logService.Info("休息倒计时结束，准备关闭休息窗口");

            // 标记休息已完成
            _isRestCompleted = true;

            // 先启动主倒计时（不阻塞）
            StartCountdown();

            // 异步关闭休息窗口，避免死锁
            if (Application.Current?.Dispatcher != null)
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                {
                    _logService.Info("正在关闭休息窗口...");
                    CloseRestWindow();

                    // 强制刷新UI状态
                    StatusText = "运行中";
                    PauseResumeButtonText = "暂停提醒";

                    var initialTime = DateTimeHelper.FormatCountdown(_timerService.MainCountdownRemaining);
                    CountdownText = initialTime;

                    _logService.Info($"主倒计时已重启，当前显示: {initialTime}, 状态: {StatusText}");
                }), System.Windows.Threading.DispatcherPriority.Normal);
            }
        }

        /// <summary>
        /// 休息前预提醒处理
        /// </summary>
        private void OnPreReminder(object? sender, PreReminderEventArgs e)
        {
            _logService.Info($"预提醒触发：{e.SecondsUntilRest}秒后开始休息");

            // 显示托盘气泡提示
            if (_trayIcon != null)
            {
                _trayIcon.ShowBalloonTip(
                    5000,  // 显示5秒
                    "EyeSharp 护眼提醒",
                    e.Message,
                    WinForms.ToolTipIcon.Info
                );
            }
        }

        /// <summary>
        /// 显示休息窗口
        /// </summary>
        private void ShowRestWindow()
        {
            try
            {
                var previousState = _timerService.State;
                _logService.Info($"开始创建休息窗口，之前的状态: {previousState}");

                // 开始记录休息统计
                _isRestCompleted = false;
                _statisticsService.StartRest(_config.RestDurationSeconds, _config.IsForcedMode);
                _logService.Info("开始记录休息统计");

                _currentRestWindow = new RestWindow(_configService, _passwordService, _logService, _config, previousState);

                _logService.Info("休息窗口对象已创建");

                // 当休息窗口关闭时，检查是否是正常结束还是提前结束
                _currentRestWindow.Closed += (s, e) =>
                {
                    _logService.Info("休息窗口 Closed 事件触发");

                    // 结束休息统计
                    _statisticsService.EndRest(_isRestCompleted);
                    _logService.Info($"休息统计已记录，完成状态: {_isRestCompleted}");

                    if (_timerService.State == TimerState.Resting)
                    {
                        // 如果定时器还在 Resting 状态，说明是提前关闭的
                        _logService.Info("休息窗口被提前关闭");
                        CloseRestWindow();
                        StartCountdown();
                    }
                };

                _logService.Info("准备显示休息窗口，调用 Show() 方法");
                _currentRestWindow.Show();
                _logService.Info("Show() 方法已调用");

                // 强制激活窗口
                _currentRestWindow.Activate();
                _logService.Info("窗口已激活");

                _logService.Info("休息窗口已显示");

                // 开始休息倒计时
                var restDuration = TimeSpan.FromSeconds(_config.RestDurationSeconds);
                _timerService.StartRestCountdown(restDuration);
                _logService.Info($"休息倒计时已启动: {_config.RestDurationSeconds}秒");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "显示休息窗口失败");
                // 如果休息窗口显示失败，直接继续主倒计时
                StartCountdown();
            }
        }

        /// <summary>
        /// 关闭休息窗口
        /// </summary>
        private void CloseRestWindow()
        {
            if (_currentRestWindow != null)
            {
                _currentRestWindow.Close();
                _currentRestWindow = null;
            }
        }

        /// <summary>
        /// 暂停/恢复命令
        /// </summary>
        [RelayCommand]
        private void PauseResume()
        {
            if (_timerService.State == TimerState.Running)
            {
                _timerService.Pause();
                _logService.Info("用户暂停倒计时");
                // 更新托盘图标为暂停状态
                UpdateTrayIconState(true);
            }
            else if (_timerService.State == TimerState.Paused)
            {
                _timerService.Resume();
                _logService.Info("用户恢复倒计时");
                // 更新托盘图标为正常运行状态
                UpdateTrayIconState(false);
            }
        }

        /// <summary>
        /// 托盘菜单暂停/恢复切换
        /// </summary>
        private void TogglePauseResume()
        {
            _logService.Info("用户通过托盘菜单点击暂停/恢复");
            PauseResume();
        }

        /// <summary>
        /// 应用设置命令
        /// </summary>
        [RelayCommand]
        private async System.Threading.Tasks.Task ApplySettingsAsync()
        {
            try
            {
                // 验证输入
                if (RestIntervalMinutes < 1 || RestIntervalMinutes > 120)
                {
                    MessageBox.Show("休息间隔必须在 1-120 分钟之间", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (RestDurationSeconds < 5 || RestDurationSeconds > 300)
                {
                    MessageBox.Show("休息时长必须在 5-300 秒之间", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // 更新配置
                _config.RestIntervalMinutes = RestIntervalMinutes;
                _config.RestDurationSeconds = RestDurationSeconds;
                _config.IsForcedMode = IsForcedMode;
                _config.RestWindowMode = IsBlackMode ? "black" : "image";
                _config.CustomImagePath = CustomImagePath;
                _config.AutoStart = AutoStart;
                _config.LogLevel = SelectedLogLevel;
                _config.IsPreReminderEnabled = IsPreReminderEnabled;
                _config.Theme = IsDarkTheme ? "dark" : "light";
                _config.LockScreenBehavior = GetLockScreenBehavior();

                // 应用日志级别变更
                _logService.SetLogLevel(SelectedLogLevel);

                // 应用预提醒设置
                if (_timerService is TimerService concreteTimerService)
                {
                    concreteTimerService.IsPreReminderEnabled = IsPreReminderEnabled;
                }

                // 保存配置
                await _configService.SaveConfigAsync(_config);

                // 设置开机自启动
                var autoStartResult = AutoStartHelper.SetAutoStart(AutoStart);
                if (autoStartResult)
                {
                    _logService.Info($"开机自启动已{(AutoStart ? "启用" : "禁用")}");
                }
                else
                {
                    _logService.Warn("开机自启动设置失败");
                }

                // 重启倒计时
                _timerService.Stop();
                StartCountdown();

                _logService.Info("设置已应用并重启倒计时");
                ShowToast("✅ 设置已保存");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "保存设置失败");
                MessageBox.Show("保存设置失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 浏览图片文件夹命令
        /// </summary>
        [RelayCommand]
        private void BrowseImage()
        {
            _logService.Info("用户点击浏览图片文件夹");

            try
            {
                // 使用 Windows Forms 的 FolderBrowserDialog
                using var dialog = new System.Windows.Forms.FolderBrowserDialog()
                {
                    Description = "选择包含图片的文件夹",
                    ShowNewFolderButton = false,
                    UseDescriptionForTitle = true
                };

                var result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    CustomImagePath = dialog.SelectedPath;
                    _logService.Info($"用户选择图片文件夹: {CustomImagePath}");
                }
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "选择图片文件夹失败");
                MessageBox.Show($"选择图片文件夹失败：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 修改密码命令
        /// </summary>
        [RelayCommand]
        private async System.Threading.Tasks.Task ChangePasswordAsync()
        {
            _logService.Info("用户点击修改密码");

            try
            {
                // 创建修改密码对话框
                var dialog = new ChangePasswordDialog(_passwordService, _config.PasswordHash, _logService);

                // 显示对话框
                var result = dialog.ShowDialog();

                if (result == true && dialog.NewPasswordHash != null)
                {
                    // 更新密码哈希
                    _config.PasswordHash = dialog.NewPasswordHash;

                    // 保存配置
                    await _configService.SaveConfigAsync(_config);

                    _logService.Info("密码修改成功");

                    // 显示成功提示
                    MessageBox.Show(
                        "密码修改成功！",
                        "成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );
                }
                else
                {
                    _logService.Info("用户取消修改密码");
                }
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "修改密码失败");
                MessageBox.Show(
                    "修改密码失败，请稍后重试。",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// 忘记密码命令 - 清空所有配置并退出程序
        /// </summary>
        [RelayCommand]
        private async System.Threading.Tasks.Task ForgotPasswordAsync()
        {
            _logService.Info("用户点击忘记密码");

            try
            {
                // 显示确认对话框
                var dialog = new ForgotPasswordDialog();
                var result = dialog.ShowDialog();

                if (result == true && dialog.IsConfirmed)
                {
                    // 获取配置文件路径
                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    var configPath = System.IO.Path.Combine(baseDir, "config.json");
                    var backupPath = System.IO.Path.Combine(baseDir, "config.backup.json");

                    // 删除配置文件
                    if (System.IO.File.Exists(configPath))
                    {
                        System.IO.File.Delete(configPath);
                        _logService.Info($"已删除配置文件: {configPath}");
                    }

                    // 删除备份文件（如果存在）
                    if (System.IO.File.Exists(backupPath))
                    {
                        System.IO.File.Delete(backupPath);
                        _logService.Info($"已删除备份文件: {backupPath}");
                    }

                    _logService.Info("所有配置已清空，程序将退出");

                    // 显示提示
                    MessageBox.Show(
                        "所有配置已清空，程序将退出。\n下次启动时需要重新设置密码。",
                        "重置成功",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information
                    );

                    // 退出程序
                    Application.Current.Shutdown();
                }
                else
                {
                    _logService.Info("用户取消重置操作");
                }
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "忘记密码操作失败");
                MessageBox.Show(
                    "操作失败，请稍后重试。",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// 切换主题命令
        /// </summary>
        [RelayCommand]
        private void ToggleTheme()
        {
            try
            {
                _logService.Info("用户点击切换主题");

                // 切换主题状态
                IsDarkTheme = !IsDarkTheme;

                // 更新按钮文本
                ThemeButtonText = IsDarkTheme ? "☀️ 浅色" : "🌙 深色";
                ThemeButtonIcon = IsDarkTheme ? "☀️" : "🌙";
                ThemeButtonShortText = IsDarkTheme ? "浅色" : "深色";

                // 应用主题
                var theme = IsDarkTheme ? ThemeType.Dark : ThemeType.Light;
                _themeService.ApplyTheme(theme);

                // 保存配置
                _config.Theme = IsDarkTheme ? "dark" : "light";
                _ = _configService.SaveConfigAsync(_config);

                _logService.Info($"主题已切换为: {_config.Theme}");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "切换主题失败");
            }
        }

        /// <summary>
        /// 隐藏到托盘命令
        /// </summary>
        [RelayCommand]
        private void HideToTray()
        {
            _logService.Info("用户点击隐藏到托盘");

            // 隐藏主窗口（与点击关闭按钮行为一致）
            if (Application.Current?.Windows.Count > 0)
            {
                var mainWindow = Application.Current.Windows[0] as MainWindow;
                if (mainWindow != null)
                {
                    // 保存窗口位置（与关闭时行为一致）
                    mainWindow.SaveWindowPosition();
                    mainWindow.Hide();
                    _logService.Info("主窗口已隐藏到托盘");
                }
            }
        }

        /// <summary>
        /// 查看统计命令
        /// </summary>
        [RelayCommand]
        private void ViewStatistics()
        {
            _logService.Info("用户点击查看统计");

            try
            {
                // 保存当前统计数据
                _ = _statisticsService.SaveAsync();

                // 创建并显示统计窗口
                var statisticsWindow = new StatisticsWindow(_statisticsService, _logService, _themeService);
                statisticsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "打开统计窗口失败");
                MessageBox.Show(
                    "打开统计窗口失败，请稍后重试。",
                    "错误",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// 显示Toast通知
        /// </summary>
        /// <param name="message">消息内容</param>
        /// <param name="durationMs">显示时长（毫秒）</param>
        public void ShowToast(string message, int durationMs = 2000)
        {
            _logService.Info($"显示Toast: {message}");

            // 取消之前的定时器
            _toastTimer?.Stop();

            // 设置消息并显示
            ToastMessage = message;
            ToastVisibility = System.Windows.Visibility.Visible;

            // 使用动画效果显示
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(async () =>
            {
                // 淡入
                for (double i = 0; i <= 1; i += 0.1)
                {
                    ToastOpacity = i;
                    await Task.Delay(20);
                }
                ToastOpacity = 1;

                // 等待指定时间
                await Task.Delay(durationMs);

                // 淡出
                for (double i = 1; i >= 0; i -= 0.1)
                {
                    ToastOpacity = i;
                    await Task.Delay(20);
                }

                ToastVisibility = System.Windows.Visibility.Collapsed;
                ToastOpacity = 0;
            });
        }

        /// <summary>
        /// 初始化锁屏处理行为
        /// </summary>
        private void InitializeLockScreenBehavior()
        {
            var behavior = string.IsNullOrEmpty(_config.LockScreenBehavior) ? "normal" : _config.LockScreenBehavior;
            IsLockScreenBehaviorNormal = behavior == "normal";
            IsLockScreenBehaviorPause = behavior == "pause";
            IsLockScreenBehaviorSkip = behavior == "skip";

            _logService.Info($"锁屏处理行为初始化: {behavior}");
        }

        /// <summary>
        /// 获取当前锁屏处理行为字符串
        /// </summary>
        private string GetLockScreenBehavior()
        {
            if (IsLockScreenBehaviorPause) return "pause";
            if (IsLockScreenBehaviorSkip) return "skip";
            return "normal";
        }

        /// <summary>
        /// 退出命令
        /// </summary>
        [RelayCommand]
        private void Exit()
        {
            _logService.Info("用户点击退出程序，显示确认对话框");

            // 显示退出确认对话框
            var dialog = new ExitConfirmDialog();
            var result = dialog.ShowDialog();

            if (result == true)
            {
                // 用户确认退出
                _logService.Info("用户确认退出程序");
                _trayIcon?.Dispose();
                Application.Current.Shutdown();
            }
            else
            {
                // 用户取消退出
                _logService.Info("用户取消退出程序");
            }
        }

        /// <summary>
        /// 初始化系统托盘图标
        /// </summary>
        private void InitializeTrayIcon()
        {
            // 加载自定义图标
            LoadTrayIcons();

            _trayIcon = new WinForms.NotifyIcon
            {
                Icon = _trayIconNormal ?? System.Drawing.SystemIcons.Application,
                Text = "护眼助手 - 休息倒计时中",
                Visible = true
            };

            // 创建右键菜单
            var contextMenu = new WinForms.ContextMenuStrip();
            var showItem = new WinForms.ToolStripMenuItem("显示主窗口");
            showItem.Click += (s, e) => ShowMainWindow();
            contextMenu.Items.Add(showItem);

            // 添加暂停/恢复菜单项
            var pauseResumeItem = new WinForms.ToolStripMenuItem("暂停提醒");
            pauseResumeItem.Click += (s, e) => TogglePauseResume();
            contextMenu.Items.Add(pauseResumeItem);

            contextMenu.Items.Add(new WinForms.ToolStripSeparator());

            var exitItem = new WinForms.ToolStripMenuItem("退出程序");
            exitItem.Click += (s, e) => Exit();
            contextMenu.Items.Add(exitItem);

            _trayIcon.ContextMenuStrip = contextMenu;

            // 保存引用以便后续更新
            _trayPauseResumeItem = pauseResumeItem;

            // 双击托盘图标显示主窗口
            _trayIcon.DoubleClick += (s, e) => ShowMainWindow();

            _logService.Info("系统托盘图标已初始化");
        }

        /// <summary>
        /// 加载托盘图标资源
        /// </summary>
        private void LoadTrayIcons()
        {
            try
            {
                // 获取图标文件路径
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var normalIconPath = Path.Combine(baseDir, "Resources", "Icons", "tray_normal_16x16.png");
                var pausedIconPath = Path.Combine(baseDir, "Resources", "Icons", "tray_paused_16x16.png");

                // 如果16x16不存在，尝试使用主文件
                if (!File.Exists(normalIconPath))
                {
                    normalIconPath = Path.Combine(baseDir, "Resources", "Icons", "tray_normal.png");
                }
                if (!File.Exists(pausedIconPath))
                {
                    pausedIconPath = Path.Combine(baseDir, "Resources", "Icons", "tray_paused.png");
                }

                // 加载图标
                if (File.Exists(normalIconPath))
                {
                    using var normalStream = new FileStream(normalIconPath, FileMode.Open, FileAccess.Read);
                    using var normalBitmap = new System.Drawing.Bitmap(normalStream);
                    _trayIconNormal = System.Drawing.Icon.FromHandle(normalBitmap.GetHicon());
                    _logService.Info($"已加载正常运行图标: {normalIconPath}");
                }
                else
                {
                    _logService.Warn($"未找到正常运行图标: {normalIconPath}");
                }

                if (File.Exists(pausedIconPath))
                {
                    using var pausedStream = new FileStream(pausedIconPath, FileMode.Open, FileAccess.Read);
                    using var pausedBitmap = new System.Drawing.Bitmap(pausedStream);
                    _trayIconPaused = System.Drawing.Icon.FromHandle(pausedBitmap.GetHicon());
                    _logService.Info($"已加载暂停状态图标: {pausedIconPath}");
                }
                else
                {
                    _logService.Warn($"未找到暂停状态图标: {pausedIconPath}");
                }
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "加载托盘图标失败，将使用系统默认图标");
            }
        }

        /// <summary>
        /// 更新托盘图标状态
        /// </summary>
        private void UpdateTrayIconState(bool isPaused)
        {
            if (_trayIcon == null) return;

            if (isPaused)
            {
                // 暂停状态
                _trayIcon.Icon = _trayIconPaused ?? _trayIconNormal ?? System.Drawing.SystemIcons.Application;
                _trayIcon.Text = "护眼助手 - 休息倒计时已暂停";
                if (_trayPauseResumeItem != null)
                    _trayPauseResumeItem.Text = "恢复提醒";
                _logService.Info("托盘图标已切换为暂停状态");
            }
            else
            {
                // 正常运行状态
                _trayIcon.Icon = _trayIconNormal ?? System.Drawing.SystemIcons.Application;
                _trayIcon.Text = "护眼助手 - 休息倒计时中";
                if (_trayPauseResumeItem != null)
                    _trayPauseResumeItem.Text = "暂停提醒";
                _logService.Info("托盘图标已切换为正常运行状态");
            }
        }

        /// <summary>
        /// 显示主窗口
        /// </summary>
        [RelayCommand]
        private void ShowMainWindow()
        {
            if (Application.Current?.Windows.Count > 0)
            {
                var mainWindow = Application.Current.Windows[0] as MainWindow;
                if (mainWindow != null)
                {
                    mainWindow.RestoreWindow();
                }
            }
        }

        /// <summary>
        /// 部分属性变更时更新UI状态
        /// </summary>
        partial void OnIsBlackModeChanged(bool value)
        {
            if (value)
            {
                IsImageMode = false;
            }
        }

        partial void OnIsImageModeChanged(bool value)
        {
            if (value)
            {
                IsBlackMode = false;
            }
        }

        /// <summary>
        /// 释放资源，取消事件订阅
        /// </summary>
        public void Dispose()
        {
            // 取消定时器事件订阅
            _timerService.StateChanged -= OnTimerStateChanged;
            _timerService.MainCountdownTick -= OnMainCountdownTick;
            _timerService.MainCountdownElapsed -= OnMainCountdownElapsed;
            _timerService.RestCountdownTick -= OnRestCountdownTick;
            _timerService.RestCountdownElapsed -= OnRestCountdownElapsed;
            _timerService.PreReminder -= OnPreReminder;

            // 取消系统事件订阅
            SystemEvents.SessionSwitch -= OnSessionSwitch;

            // 释放托盘图标资源
            _trayIcon?.Dispose();
            _trayIcon = null;

            // 释放图标资源（修复内存泄漏）
            _trayIconNormal?.Dispose();
            _trayIconNormal = null;
            _trayIconPaused?.Dispose();
            _trayIconPaused = null;

            _logService.Info("MainViewModel 已释放");
        }

        /// <summary>
        /// 系统会话切换事件处理（锁屏/解锁检测）
        /// </summary>
        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            try
            {
                switch (e.Reason)
                {
                    case SessionSwitchReason.SessionLock:
                        _logService.Info("检测到系统锁屏");
                        HandleSessionLock();
                        break;

                    case SessionSwitchReason.SessionUnlock:
                        _logService.Info("检测到系统解锁");
                        HandleSessionUnlock();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "处理会话切换事件时发生错误");
            }
        }

        /// <summary>
        /// 处理系统锁屏
        /// </summary>
        private void HandleSessionLock()
        {
            var behavior = _config.LockScreenBehavior ?? "normal";
            _logService.Info($"锁屏处理方式: {behavior}");

            switch (behavior)
            {
                case "pause":
                    // 方案2: 暂停倒计时
                    if (_timerService.State == TimerState.Running)
                    {
                        _timerService.Pause();
                        _wasPausedDueToLockScreen = true;
                        _logService.Info("锁屏导致倒计时暂停");
                        ShowToast("⏸️ 已暂停倒计时（锁屏）");
                    }
                    break;

                case "skip":
                    // 方案3: 如果正在休息，则提前结束
                    if (_timerService.State == TimerState.Resting && _currentRestWindow != null)
                    {
                        _logService.Info("锁屏导致跳过本次休息");
                        Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            _currentRestWindow?.Close();
                        });
                    }
                    break;

                case "normal":
                default:
                    // 方案1: 正常显示，不做特殊处理
                    _logService.Info("锁屏时不做特殊处理");
                    break;
            }
        }

        /// <summary>
        /// 处理系统解锁
        /// </summary>
        private void HandleSessionUnlock()
        {
            var behavior = _config.LockScreenBehavior ?? "normal";

            if (behavior == "pause" && _wasPausedDueToLockScreen)
            {
                // 方案2: 恢复倒计时
                _timerService.Resume();
                _wasPausedDueToLockScreen = false;
                _logService.Info("解锁后恢复倒计时");
                ShowToast("▶️ 已恢复倒计时");
            }
        }
    }
}
