using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using eyesharp.Models;
using eyesharp.Services;
using eyesharp.Views;
using eyesharp.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace eyesharp
{
    /// <summary>
    /// App.xaml 的交互逻辑
    /// </summary>
    public partial class App : Application
    {
        private static Logger? _logger = LogManager.GetCurrentClassLogger();
        private static IServiceProvider? _serviceProvider;
        private static Mutex? _singleInstanceMutex;
        private static AppConfig? _currentConfig;
        private static MainViewModel? _mainViewModel;

        // Windows API 用于跨进程窗口激活
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        private const int SW_RESTORE = 9;

        /// <summary>
        /// 获取服务提供者
        /// </summary>
        public static IServiceProvider ServiceProvider => _serviceProvider ?? throw new InvalidOperationException("服务未初始化");

        /// <summary>
        /// 获取当前配置
        /// </summary>
        public static AppConfig CurrentConfig => _currentConfig ?? throw new InvalidOperationException("配置未加载");

        /// <summary>
        /// 应用程序启动
        /// </summary>
        private async void Application_Startup(object sender, StartupEventArgs e)
        {
            // 检查单实例
            if (!CheckSingleInstance())
            {
                _logger?.Warn("程序已在运行，退出本次启动");
                Shutdown();
                return;
            }

            // 设置为显式关闭模式，防止对话框关闭时自动退出
            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            // 配置服务
            ConfigureServices();

            // 异步加载配置
            try
            {
                var configService = GetConfigService();

                if (configService == null)
                {
                    MessageBox.Show("配置服务初始化失败", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                    return;
                }

                // 订阅配置损坏事件
                configService.ConfigCorrupted += OnConfigCorrupted;

                // 异步加载配置
                _currentConfig = await configService.LoadConfigAsync();

                // 延迟初始化日志服务
                var logService = ServiceProvider.GetService<ILogService>();

                // 记录启动日志
                logService?.Info("应用程序启动");

                // 设置日志级别
                logService?.Info("准备设置日志级别");
                logService?.SetLogLevel(_currentConfig.LogLevel);
                logService?.Info("日志级别设置完成");

                // 初始化主题
                logService?.Info("准备初始化主题");
                var themeService = ServiceProvider.GetService<IThemeService>();
                themeService?.LoadTheme(_currentConfig.Theme);
                logService?.Info("主题初始化完成");

                // 清理旧日志
                logService?.Info("准备清理旧日志");
                var logCleanupService = ServiceProvider.GetService<LogCleanupService>();
                logCleanupService?.CleanupOldLogs();
                logService?.Info("旧日志清理完成");

                // 检查是否首次启动（密码未设置）
                logService?.Info("准备检查密码服务");
                var passwordService = ServiceProvider.GetService<IPasswordService>();
                logService?.Info($"密码服务获取成功，密码哈希长度: {_currentConfig.PasswordHash?.Length ?? 0}");

                logService?.Info("检查密码是否已设置");
                if (passwordService != null && !passwordService.IsPasswordSet(_currentConfig.PasswordHash))
                {
                    logService?.Info("密码未设置，准备显示密码设置对话框");
                    // 首次启动，显示密码设置对话框
                    logService?.Info("即将调用 ShowPasswordDialogAsync()");
                    bool? passwordSetResult = await ShowPasswordDialogAsync();
                    logService?.Info($"密码设置对话框关闭，结果: {passwordSetResult.HasValue}, 值: {passwordSetResult.GetValueOrDefault()}");
                    if (passwordSetResult != true)
                    {
                        // 用户取消密码设置，退出程序
                        logService?.Warn("用户取消密码设置，退出程序");
                        Shutdown();
                        return;
                    }

                    // 重新加载配置以获取保存的密码哈希
                    logService?.Info("重新加载配置");
                    _currentConfig = await configService.LoadConfigAsync();
                    logService?.Info($"配置重新加载成功，密码哈希长度: {_currentConfig.PasswordHash?.Length ?? 0}");
                }

                // 创建并显示主窗口
                var timerService = ServiceProvider.GetService<ITimerService>();
                var statisticsService = ServiceProvider.GetService<IStatisticsService>();
                logService?.Info("开始创建 MainViewModel");
                _mainViewModel = new MainViewModel(configService, logService!, timerService!, passwordService!, statisticsService!, themeService!, _currentConfig);
                logService?.Info("MainViewModel 创建成功");

                var mainWindow = new MainWindow(_mainViewModel);
                logService?.Info("MainWindow 创建成功，准备显示");

                logService?.Info("即将调用 mainWindow.Show()");
                mainWindow.Show();
                logService?.Info("mainWindow.Show() 已调用");

                // 主窗口已显示，设置为主窗口关闭模式
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                logService?.Info("ShutdownMode 已设置为 OnMainWindowClose");

                logService?.Info("Application_Startup 方法即将完成");
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "应用程序启动失败");
                MessageBox.Show($"应用程序启动失败：{ex.Message}\n\n{ex.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        /// <summary>
        /// 应用程序退出
        /// </summary>
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            var logService = _serviceProvider?.GetService<ILogService>();
            logService?.Info("应用程序退出");

            // 释放 MainViewModel（包含托盘图标清理）
            _mainViewModel?.Dispose();

            // 释放互斥体
            _singleInstanceMutex?.ReleaseMutex();
            _singleInstanceMutex?.Dispose();

            // 刷新日志
            LogManager.Shutdown();
        }

        /// <summary>
        /// 检查单实例
        /// </summary>
        private bool CheckSingleInstance()
        {
            const string mutexName = "Global\\eyesharp_single_instance_mutex";

            try
            {
                _singleInstanceMutex = new Mutex(true, mutexName, out bool createdNew);

                if (!createdNew)
                {
                    // 尝试获取 Mutex，如果原持有者进程已终止，我们可以获取它
                    if (_singleInstanceMutex.WaitOne(0))
                    {
                        // 成功获取 Mutex，说明原进程已终止
                        return true;
                    }

                    // Mutex 被其他进程持有，激活已运行实例的窗口
                    _logger?.Info("检测到已有实例运行，尝试激活已运行实例窗口");
                    ActivateExistingInstance();

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "检查单实例失败");
                return false;
            }
        }

        /// <summary>
        /// 激活已运行实例的窗口
        /// </summary>
        private void ActivateExistingInstance()
        {
            try
            {
                // 查找已运行实例的主窗口（通过窗口标题）
                IntPtr hWnd = FindWindow(null, "EyeSharp 护眼助手");

                if (hWnd != IntPtr.Zero)
                {
                    // 如果窗口最小化，则恢复它
                    if (IsIconic(hWnd))
                    {
                        ShowWindow(hWnd, SW_RESTORE);
                    }

                    // 将窗口置于前台
                    SetForegroundWindow(hWnd);
                    _logger?.Info("成功激活已运行实例的窗口");
                }
                else
                {
                    // 未找到窗口，显示提示
                    MessageBox.Show(
                        "程序已在运行中。\n请检查系统托盘图标。",
                        "EyeSharp 护眼助手",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                _logger?.Error(ex, "激活已运行实例窗口失败");
                // 出错时仍然显示提示
                MessageBox.Show(
                    "程序已在运行中。\n请检查系统托盘图标。",
                    "EyeSharp 护眼助手",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }

        /// <summary>
        /// 配置依赖注入服务
        /// </summary>
        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            // 注册服务
            services.AddSingleton<ILogService, LogService>();
            services.AddSingleton<IConfigService, ConfigService>();
            services.AddSingleton<IPasswordService, PasswordService>();
            services.AddSingleton<ITimerService, TimerService>();
            services.AddSingleton<IStatisticsService, StatisticsService>();
            services.AddSingleton<IThemeService, ThemeService>();
            services.AddSingleton<LogCleanupService>();

            _serviceProvider = services.BuildServiceProvider();
        }

        /// <summary>
        /// 获取配置服务
        /// </summary>
        private IConfigService GetConfigService()
        {
            return ServiceProvider.GetService<IConfigService>()
                ?? throw new InvalidOperationException("配置服务未注册");
        }

        /// <summary>
        /// 显示密码设置对话框
        /// </summary>
        private async Task<bool?> ShowPasswordDialogAsync()
        {
            try
            {
                var showPwdDialogLogService = ServiceProvider.GetService<ILogService>();
                showPwdDialogLogService?.Info("ShowPasswordDialog: 开始创建对话框");
                var dialog = new SetPasswordDialog();
                showPwdDialogLogService?.Info("ShowPasswordDialog: 对话框已创建，准备显示");
                var result = dialog.ShowDialog();
                showPwdDialogLogService?.Info($"ShowPasswordDialog: ShowDialog() 返回，结果: {result}");

                if (result == true)
                {
                    if (dialog.Password == null || string.IsNullOrEmpty(dialog.Password))
                    {
                        MessageBox.Show("密码为空，无法保存", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }

                    // 保存密码到配置
                    var passwordService = ServiceProvider.GetService<IPasswordService>();
                    if (passwordService != null)
                    {
                        var passwordHash = passwordService.HashPassword(dialog.Password);
                        _currentConfig!.PasswordHash = passwordHash;
                        showPwdDialogLogService?.Info($"密码哈希生成成功，长度: {passwordHash?.Length ?? 0}");

                        // 异步保存配置
                        var configService = ServiceProvider.GetService<IConfigService>();
                        if (configService != null)
                        {
                            showPwdDialogLogService?.Info("开始保存配置");
                            await configService.SaveConfigAsync(_currentConfig);
                            showPwdDialogLogService?.Info("首次启动密码设置成功");
                            return true;
                        }
                        else
                        {
                            MessageBox.Show("配置服务不可用", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                            return false;
                        }
                    }
                    else
                    {
                        MessageBox.Show("密码服务不可用", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }
                else
                {
                    // 用户取消
                    showPwdDialogLogService?.Info("用户取消密码设置");
                    return false;
                }
            }
            catch (Exception ex)
            {
                var logService = ServiceProvider.GetService<ILogService>();
                logService?.Error($"ShowPasswordDialog 异常: {ex.Message}");
                MessageBox.Show($"密码设置失败：{ex.Message}\n\n{ex.StackTrace}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// 配置损坏事件处理
        /// </summary>
        private void OnConfigCorrupted(object? sender, ConfigCorruptedEventArgs e)
        {
            // 在 UI 线程中显示对话框
            Dispatcher.Invoke(() =>
            {
                var dialog = new ConfigRecoveryDialog();
                dialog.ShowDialog();

                var logService = ServiceProvider.GetService<ILogService>();
                logService?.Info("配置已恢复为默认值");
            });
        }
    }
}
