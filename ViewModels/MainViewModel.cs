using System;
using System.Windows;
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
    public partial class MainViewModel : ObservableObject
    {
        private readonly IConfigService _configService;
        private readonly ILogService _logService;
        private readonly ITimerService _timerService;
        private readonly IPasswordService _passwordService;
        private AppConfig _config;
        private RestWindow? _currentRestWindow;

        [ObservableProperty]
        private string _statusText = "初始化中...";

        [ObservableProperty]
        private string _countdownText = "00:00";

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

        public MainViewModel(IConfigService configService, ILogService logService, ITimerService timerService, IPasswordService passwordService, AppConfig config)
        {
            _configService = configService;
            _logService = logService;
            _timerService = timerService;
            _passwordService = passwordService;
            _config = config;

            // 更新UI属性
            RestIntervalMinutes = _config.RestIntervalMinutes;
            RestDurationSeconds = _config.RestDurationSeconds;
            IsForcedMode = _config.IsForcedMode;
            IsBlackMode = _config.RestWindowMode == "black";
            IsImageMode = _config.RestWindowMode == "image";
            CustomImagePath = _config.CustomImagePath;
            AutoStart = _config.AutoStart;

            // 订阅定时器事件
            _timerService.StateChanged += OnTimerStateChanged;
            _timerService.MainCountdownTick += OnMainCountdownTick;
            _timerService.MainCountdownElapsed += OnMainCountdownElapsed;
            _timerService.RestCountdownTick += OnRestCountdownTick;
            _timerService.RestCountdownElapsed += OnRestCountdownElapsed;

            StatusText = "运行中";
            _logService.Info("配置加载成功");

            // 启动倒计时
            StartCountdown();
        }

        /// <summary>
        /// 启动倒计时
        /// </summary>
        private void StartCountdown()
        {
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
            Application.Current.Dispatcher.Invoke(() =>
            {
                switch (e.NewState)
                {
                    case TimerState.Running:
                        StatusText = "运行中";
                        PauseResumeButtonText = "暂停提醒";
                        break;

                    case TimerState.Paused:
                        StatusText = "已暂停";
                        PauseResumeButtonText = "恢复提醒";
                        break;

                    case TimerState.Resting:
                        StatusText = "休息中";
                        PauseResumeButtonText = "休息中";
                        break;
                }
            });
        }

        /// <summary>
        /// 主倒计时Tick处理
        /// </summary>
        private void OnMainCountdownTick(object? sender, TimerTickEventArgs e)
        {
            // 在UI线程中更新
            Application.Current.Dispatcher.Invoke(() =>
            {
                CountdownText = e.FormattedTime;
            });
        }

        /// <summary>
        /// 主倒计时结束处理（触发休息窗口）
        /// </summary>
        private void OnMainCountdownElapsed(object? sender, EventArgs e)
        {
            // 在UI线程中处理
            Application.Current.Dispatcher.Invoke(() =>
            {
                _logService.Info("主倒计时结束，显示休息窗口");
                ShowRestWindow();
            });
        }

        /// <summary>
        /// 休息倒计时Tick处理
        /// </summary>
        private void OnRestCountdownTick(object? sender, TimerTickEventArgs e)
        {
            // 在UI线程中更新休息窗口倒计时
            Application.Current.Dispatcher.Invoke(() =>
            {
                _currentRestWindow?.UpdateCountdown(e.RemainingSeconds);
            });
        }

        /// <summary>
        /// 休息倒计时结束处理（关闭休息窗口，继续主倒计时）
        /// </summary>
        private void OnRestCountdownElapsed(object? sender, EventArgs e)
        {
            _logService.Info("休息倒计时结束，准备关闭休息窗口");

            // 先启动主倒计时（不阻塞）
            StartCountdown();

            // 异步关闭休息窗口，避免死锁
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

        /// <summary>
        /// 显示休息窗口
        /// </summary>
        private void ShowRestWindow()
        {
            try
            {
                var previousState = _timerService.State;
                _logService.Info($"开始创建休息窗口，之前的状态: {previousState}");

                _currentRestWindow = new RestWindow(_configService, _passwordService, _logService, _config, previousState);

                _logService.Info("休息窗口对象已创建");

                // 当休息窗口关闭时，检查是否是正常结束还是提前结束
                _currentRestWindow.Closed += (s, e) =>
                {
                    _logService.Info("休息窗口 Closed 事件触发");
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
            }
            else if (_timerService.State == TimerState.Paused)
            {
                _timerService.Resume();
                _logService.Info("用户恢复倒计时");
            }
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

                // 保存配置
                await _configService.SaveConfigAsync(_config);

                // 重启倒计时
                _timerService.Stop();
                StartCountdown();

                _logService.Info("设置已应用并重启倒计时");
                MessageBox.Show("设置已保存", "成功", MessageBoxButton.OK, MessageBoxImage.Information);
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
            // TODO: 实现文件夹浏览对话框
            _logService.Info("用户点击浏览图片文件夹");
        }

        /// <summary>
        /// 修改密码命令
        /// </summary>
        [RelayCommand]
        private void ChangePassword()
        {
            // TODO: 打开修改密码对话框
            _logService.Info("用户点击修改密码");
        }

        /// <summary>
        /// 退出命令
        /// </summary>
        [RelayCommand]
        private void Exit()
        {
            // TODO: 保存配置并退出
            _logService.Info("用户点击退出程序");
            Application.Current.Shutdown();
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
    }
}
