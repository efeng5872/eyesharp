using System;
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
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // 检查单实例
            if (!CheckSingleInstance())
            {
                _logger?.Warn("程序已在运行，退出本次启动");
                Shutdown();
                return;
            }

            // 配置服务
            ConfigureServices();

            // 同步加载配置
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

                // 同步加载配置（使用 Task.Run 避免死锁）
                _currentConfig = Task.Run(() => configService.LoadConfigAsync()).GetAwaiter().GetResult();

                // 延迟初始化日志服务（避免死锁）
                var logService = ServiceProvider.GetService<ILogService>();

                // 记录启动日志
                logService?.Info("应用程序启动");

                // 设置日志级别
                logService?.SetLogLevel(_currentConfig.LogLevel);

                // 清理旧日志
                var logCleanupService = ServiceProvider.GetService<LogCleanupService>();
                logCleanupService?.CleanupOldLogs();

                // 检查是否首次启动（密码未设置）
                var passwordService = ServiceProvider.GetService<IPasswordService>();

                if (passwordService != null && !passwordService.IsPasswordSet(_currentConfig.PasswordHash))
                {
                    // 首次启动，显示密码设置对话框
                    bool? passwordSetResult = ShowPasswordDialog();
                    if (passwordSetResult != true)
                    {
                        // 用户取消密码设置，退出程序
                        logService?.Warn("用户取消密码设置，退出程序");
                        Shutdown();
                        return;
                    }
                }

                // 创建并显示主窗口
                var timerService = ServiceProvider.GetService<ITimerService>();
                var mainViewModel = new MainViewModel(configService, logService!, timerService!, passwordService!, _currentConfig);
                var mainWindow = new MainWindow(mainViewModel);
                mainWindow.Show();
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

                    // Mutex 被其他进程持有，显示提示
                    MessageBox.Show(
                        "程序已在运行中。\n请检查系统托盘图标。",
                        "EyeSharp 护眼助手",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

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
        private bool? ShowPasswordDialog()
        {
            try
            {
                var dialog = new SetPasswordDialog();
                var result = dialog.ShowDialog();

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

                        // 保存配置
                        var configService = ServiceProvider.GetService<IConfigService>();
                        if (configService != null)
                        {
                            configService.SaveConfigAsync(_currentConfig).Wait();

                            var logService = ServiceProvider.GetService<ILogService>();
                            logService?.Info("首次启动密码设置成功");

                            MessageBox.Show("密码设置成功！", "成功", MessageBoxButton.OK, MessageBoxImage.Information);

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
                    return false;
                }
            }
            catch (Exception ex)
            {
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
