using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using eyesharp.Helpers;
using eyesharp.Models;
using eyesharp.Services;

namespace eyesharp.Views
{
    /// <summary>
    /// Windows API 用于检测锁屏状态
    /// </summary>
    internal static class Win32Helper
    {
        [DllImport("user32.dll")]
        public static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool CloseDesktop(IntPtr hDesktop);
    }
    /// <summary>
    /// RestWindow.xaml 的交互逻辑
    /// </summary>
    public partial class RestWindow : Window
    {
        private readonly IConfigService _configService;
        private readonly IPasswordService _passwordService;
        private readonly ILogService _logService;
        private readonly AppConfig _config;
        private readonly TimerState _previousState;

        // 定时器常量
        private const int ImageSwitchIntervalSeconds = 10;
        private const int TopmostCheckIntervalSeconds = 1;

        private string[]? _imageFiles;
        private int _currentImageIndex = 0;
        private System.Windows.Threading.DispatcherTimer? _imageSwitchTimer;

        // 密码错误计数器
        private int _passwordErrorCount = 0;
        private const int MaxPasswordErrorCount = 3;

        // 图片淡入淡出控制
        private bool _isImage1Visible = true;

        // 置顶恢复定时器
        private System.Windows.Threading.DispatcherTimer? _topmostRestoreTimer;

        // 错误消息恢复定时器
        private System.Windows.Threading.DispatcherTimer? _errorResetTimer;

        // Windows API 锁屏函数
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool LockWorkStation();

        public RestWindow(IConfigService configService, IPasswordService passwordService, ILogService logService, AppConfig config, TimerState previousState)
        {
            InitializeComponent();

            _configService = configService;
            _passwordService = passwordService;
            _logService = logService;
            _config = config;
            _previousState = previousState;

            Loaded += OnLoaded;
            KeyDown += OnKeyDown;
        }

        /// <summary>
        /// 窗口加载时初始化
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _logService.Info("休息窗口已显示");

                // 设置全屏到所有显示器
                SetFullScreenToAllMonitors();

                // 设置显示模式
                SetDisplayMode();

                // 设置控制区（强制模式 vs 非强制模式）
                SetControlPanel();

                // 设置初始倒计时
                UpdateCountdown(_config.RestDurationSeconds);

                // 如果是图片模式，加载图片并开始轮播
                if (_config.RestWindowMode == "image")
                {
                    LoadImages();
                    StartImageSlideshow();
                }

                // 启动置顶恢复定时器
                StartTopmostRestoreTimer();
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "休息窗口初始化失败");
                Close();
            }
        }

        /// <summary>
        /// 设置全屏到所有显示器
        /// </summary>
        private void SetFullScreenToAllMonitors()
        {
            // 获取主显示器信息
            var primaryScreen = System.Windows.Forms.Screen.PrimaryScreen;

            // 手动设置窗口覆盖整个屏幕
            Left = 0;
            Top = 0;
            Width = primaryScreen.Bounds.Width;
            Height = primaryScreen.Bounds.Height;

            // 确保窗口在最前面
            // 注意：不在这里调用 Show()/Activate()，因为在 ShowRestWindow 中已经调用
            // 锁屏状态下重复调用会导致死锁
            Topmost = true;

            _logService.Info($"休息窗口已设置全屏: {Width}x{Height} at ({Left},{Top})");
        }

        /// <summary>
        /// 设置显示模式（黑屏或图片）
        /// </summary>
        private void SetDisplayMode()
        {
            if (_config.RestWindowMode == "black")
            {
                // 黑屏模式
                BackgroundImage1.Visibility = Visibility.Collapsed;
                BackgroundImage2.Visibility = Visibility.Collapsed;
                BackgroundBrush.Color = Colors.Black;
            }
            else if (_config.RestWindowMode == "image")
            {
                // 图片模式 - 初始化第一张图片可见
                BackgroundImage1.Visibility = Visibility.Visible;
                BackgroundImage1.Opacity = 1;
                BackgroundImage2.Visibility = Visibility.Collapsed;
                BackgroundImage2.Opacity = 0;
            }
        }

        /// <summary>
        /// 设置控制面板（强制/非强制模式）
        /// </summary>
        private void SetControlPanel()
        {
            if (_config.IsForcedMode)
            {
                // 强制模式：显示密码输入和锁屏选项
                ForcedModePanel.Visibility = Visibility.Visible;
                NonForcedModePanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                // 非强制模式：显示跳过按钮和锁屏选项
                ForcedModePanel.Visibility = Visibility.Collapsed;
                NonForcedModePanel.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 加载图片
        /// </summary>
        private void LoadImages()
        {
            try
            {
                // 获取图片文件夹
                var imageFolder = _config.CustomImagePath;

                // 如果用户自定义路径有效且包含图片
                if (!string.IsNullOrEmpty(imageFolder) && Directory.Exists(imageFolder))
                {
                    var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
                    _imageFiles = Directory.GetFiles(imageFolder)
                        .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLower()))
                        .ToArray();
                }

                // 如果自定义图片为空或无效，使用内置图片
                if (_imageFiles == null || _imageFiles.Length == 0)
                {
                    _logService.Info("自定义图片文件夹无效或为空，加载内置护眼图片");
                    _imageFiles = LoadBuiltInImages();
                }

                // 使用 Fisher-Yates 洗牌算法随机排序
                if (_imageFiles.Length > 0)
                {
                    var random = new Random();
                    for (int i = _imageFiles.Length - 1; i > 0; i--)
                    {
                        int j = random.Next(i + 1);
                        var temp = _imageFiles[i];
                        _imageFiles[i] = _imageFiles[j];
                        _imageFiles[j] = temp;
                    }

                    // 加载第一张图片
                    LoadImage(0);
                }
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "加载图片失败");
                _imageFiles = new string[0];
            }
        }

        /// <summary>
        /// 加载内置护眼图片
        /// </summary>
        private string[] LoadBuiltInImages()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var imagesDir = System.IO.Path.Combine(baseDir, "Resources", "Images");

                // 内置图片文件名
                var builtInImageNames = new[]
                {
                    "eye1.jpg",
                    "eye2.jpg",
                    "eye3.jpg",
                    "eye4.jpg",
                    "eye5.jpg"
                };

                var imagePaths = new List<string>();

                foreach (var imageName in builtInImageNames)
                {
                    var imagePath = System.IO.Path.Combine(imagesDir, imageName);
                    if (System.IO.File.Exists(imagePath))
                    {
                        imagePaths.Add(imagePath);
                        _logService.Debug($"加载内置图片: {imageName}");
                    }
                    else
                    {
                        _logService.Warn($"内置图片不存在: {imagePath}");
                    }
                }

                if (imagePaths.Count == 0)
                {
                    _logService.Error("没有可用的内置图片");
                }
                else
                {
                    _logService.Info($"成功加载 {imagePaths.Count} 张内置护眼图片");
                }

                return imagePaths.ToArray();
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "加载内置图片失败");
                return new string[0];
            }
        }

        /// <summary>
        /// 加载指定索引的图片（带淡入淡出效果）
        /// </summary>
        private void LoadImage(int index)
        {
            try
            {
                if (_imageFiles == null || index >= _imageFiles.Length)
                    return;

                var imagePath = _imageFiles[index];
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.UriSource = new Uri(imagePath);
                bitmap.EndInit();

                // 使用双图切换实现淡入淡出
                var fadeInImage = _isImage1Visible ? BackgroundImage2 : BackgroundImage1;
                var fadeOutImage = _isImage1Visible ? BackgroundImage1 : BackgroundImage2;

                // 设置新图片
                fadeInImage.Source = bitmap;
                fadeInImage.Visibility = Visibility.Visible;
                fadeInImage.Opacity = 0;

                // 创建淡入淡出动画
                var fadeInAnimation = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromSeconds(1)
                };

                var fadeOutAnimation = new DoubleAnimation
                {
                    From = 1,
                    To = 0,
                    Duration = TimeSpan.FromSeconds(1)
                };

                // 淡出完成后隐藏旧图片
                fadeOutAnimation.Completed += (s, e) =>
                {
                    fadeOutImage.Visibility = Visibility.Collapsed;
                };

                // 执行动画
                fadeInImage.BeginAnimation(OpacityProperty, fadeInAnimation);
                fadeOutImage.BeginAnimation(OpacityProperty, fadeOutAnimation);

                // 切换当前显示状态
                _isImage1Visible = !_isImage1Visible;

                _logService.Debug($"切换到图片: {System.IO.Path.GetFileName(imagePath)}");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, $"加载图片失败: {_imageFiles?[index]}");
            }
        }

        /// <summary>
        /// 开始图片轮播
        /// </summary>
        private void StartImageSlideshow()
        {
            if (_imageFiles == null || _imageFiles.Length <= 1)
                return;

            // 使用 DispatcherTimer 避免线程切换开销
            _imageSwitchTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(ImageSwitchIntervalSeconds)
            };
            _imageSwitchTimer.Tick += (s, e) =>
            {
                _currentImageIndex = (_currentImageIndex + 1) % _imageFiles!.Length;
                LoadImage(_currentImageIndex);
            };
            _imageSwitchTimer.Start();
            _logService.Debug($"图片轮播定时器已启动，间隔: {ImageSwitchIntervalSeconds}秒");
        }

        /// <summary>
        /// 更新倒计时显示
        /// </summary>
        public void UpdateCountdown(int remainingSeconds)
        {
            var formatted = DateTimeHelper.FormatCountdown(remainingSeconds);
            CountdownText.Text = formatted;
        }

        /// <summary>
        /// 密码输入框回车键处理
        /// </summary>
        private void OnPasswordKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OnEndRestClick(sender, e);
            }
        }

        /// <summary>
        /// 结束锁屏复选框勾选事件（强制模式）- 勾选后立即禁用，防止他人取消
        /// </summary>
        private void OnLockAfterRestChecked(object sender, RoutedEventArgs e)
        {
            if (LockAfterRestCheckBox.IsChecked == true)
            {
                LockAfterRestCheckBox.IsEnabled = false;
                _logService.Info("用户勾选了'结束锁屏'（强制模式），复选框已禁用以防止他人取消");
            }
        }

        /// <summary>
        /// 结束锁屏复选框勾选事件（非强制模式）- 勾选后立即禁用，防止他人取消
        /// </summary>
        private void OnLockAfterRestCheckedNonForced(object sender, RoutedEventArgs e)
        {
            if (LockAfterRestCheckBoxNonForced.IsChecked == true)
            {
                LockAfterRestCheckBoxNonForced.IsEnabled = false;
                _logService.Info("用户勾选了'结束锁屏'（非强制模式），复选框已禁用以防止他人取消");
            }
        }

        /// <summary>
        /// 提前结束按钮点击（强制模式）
        /// </summary>
        private void OnEndRestClick(object sender, RoutedEventArgs e)
        {
            // 如果已经超过最大错误次数，不允许再尝试
            if (_passwordErrorCount >= MaxPasswordErrorCount)
            {
                ShowErrorMessage("请点击重置按钮");
                return;
            }

            var password = PasswordBox.Password;

            if (string.IsNullOrEmpty(password))
            {
                ShowErrorMessage("请输入密码");
                return;
            }

            // 验证密码
            if (_passwordService.VerifyPassword(password, _config.PasswordHash))
            {
                _logService.Info("用户输入正确密码，提前结束休息");
                CloseRestWindow();
            }
            else
            {
                _passwordErrorCount++;
                _logService.Warn($"用户输入错误密码，错误次数: {_passwordErrorCount}");

                if (_passwordErrorCount >= MaxPasswordErrorCount)
                {
                    // 显示重置按钮和提示
                    ResetButton.Visibility = Visibility.Visible;
                    ShowErrorMessage("密码错误超过3次，请点击重置");
                    _logService.Warn("密码错误超过3次，显示重置按钮");
                }
                else
                {
                    ShowErrorMessage($"密码错误（{_passwordErrorCount}/3）");
                }

                PasswordBox.Clear();
            }
        }

        /// <summary>
        /// 跳过休息按钮点击（非强制模式）
        /// </summary>
        private void OnSkipRestClick(object sender, RoutedEventArgs e)
        {
            _logService.Info("用户跳过休息（非强制模式）");
            CloseRestWindow();
        }

        /// <summary>
        /// 重置按钮点击（密码错误3次后显示）
        /// </summary>
        private void OnResetClick(object sender, RoutedEventArgs e)
        {
            _logService.Info("用户点击重置按钮，显示重置确认对话框");

            // 显示重置确认对话框
            var dialog = new ForgotPasswordDialog();
            var result = dialog.ShowDialog();

            if (result == true && dialog.IsConfirmed)
            {
                // 用户确认重置，执行重置操作
                try
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

                    // 关闭休息窗口后退出程序
                    CloseRestWindow();

                    // 退出程序
                    Dispatcher.Invoke(() =>
                    {
                        Application.Current.Shutdown();
                    });
                }
                catch (Exception ex)
                {
                    _logService.Error(ex, "重置操作失败");
                    ShowErrorMessage("重置失败");
                }
            }
            else
            {
                _logService.Info("用户取消重置操作");
            }
        }

        /// <summary>
        /// 键盘事件处理（非强制模式允许 Alt+F4 关闭）
        /// </summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (!_config.IsForcedMode && e.Key == Key.F4 && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                _logService.Info("用户通过 Alt+F4 关闭休息窗口（非强制模式）");
                CloseRestWindow();
            }
        }

        /// <summary>
        /// 显示错误消息
        /// </summary>
        private void ShowErrorMessage(string message)
        {
            // 取消之前的错误恢复定时器
            _errorResetTimer?.Stop();

            // 简单的错误提示
            var originalText = CountdownText.Text;
            CountdownText.Text = message;
            CountdownText.Foreground = new SolidColorBrush(Colors.Red);

            // 使用 DispatcherTimer 避免线程切换
            _errorResetTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _errorResetTimer.Tick += (s, e) =>
            {
                CountdownText.Text = originalText;
                CountdownText.Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
                _errorResetTimer?.Stop();
                _errorResetTimer = null;
            };
            _errorResetTimer.Start();
        }

        /// <summary>
        /// 关闭休息窗口
        /// </summary>
        private void CloseRestWindow()
        {
            // 停止图片轮播定时器
            _imageSwitchTimer?.Stop();

            // 停止置顶恢复定时器
            _topmostRestoreTimer?.Stop();

            Close();
        }

        /// <summary>
        /// 检测系统是否处于锁屏状态
        /// </summary>
        private bool IsWorkstationLocked()
        {
            try
            {
                const uint DESKTOP_READOBJECTS = 0x0001;
                IntPtr hDesktop = Win32Helper.OpenInputDesktop(0, false, DESKTOP_READOBJECTS);
                if (hDesktop == IntPtr.Zero)
                {
                    return true; // 锁屏状态
                }
                Win32Helper.CloseDesktop(hDesktop);
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 启动置顶恢复定时器
        /// </summary>
        private void StartTopmostRestoreTimer()
        {
            // 使用 DispatcherTimer 避免线程切换
            _topmostRestoreTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(TopmostCheckIntervalSeconds)
            };
            _topmostRestoreTimer.Tick += (s, e) =>
            {
                try
                {
                    // 检测是否锁屏，锁屏时不执行置顶操作
                    if (IsWorkstationLocked())
                    {
                        return;
                    }

                    // 检测并恢复置顶状态
                    if (!Topmost)
                    {
                        Topmost = true;
                        _logService.Debug("休息窗口置顶状态被重置，已恢复置顶");
                    }

                    // 确保窗口始终在最前面
                    if (!IsActive)
                    {
                        Activate();
                        _logService.Debug("休息窗口失去焦点，已强制激活");
                    }
                }
                catch (Exception ex)
                {
                    _logService.Error(ex, "置顶恢复定时器执行异常");
                }
            };
            _topmostRestoreTimer.Start();

            _logService.Info($"置顶恢复定时器已启动，检测间隔: {TopmostCheckIntervalSeconds}秒");
        }

        /// <summary>
        /// 窗口关闭时清理
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            // 检查是否需要锁屏
            CheckAndLockWorkStation();

            // 取消事件订阅
            Loaded -= OnLoaded;
            KeyDown -= OnKeyDown;

            // 停止定时器
            _imageSwitchTimer?.Stop();
            _topmostRestoreTimer?.Stop();

            _logService.Info("休息窗口已关闭");
            base.OnClosed(e);
        }

        /// <summary>
        /// 检查并执行锁屏
        /// </summary>
        private void CheckAndLockWorkStation()
        {
            try
            {
                // 获取当前激活的锁屏复选框状态
                bool shouldLock = false;

                if (_config.IsForcedMode && LockAfterRestCheckBox.IsChecked == true)
                {
                    shouldLock = true;
                }
                else if (!_config.IsForcedMode && LockAfterRestCheckBoxNonForced.IsChecked == true)
                {
                    shouldLock = true;
                }

                if (shouldLock)
                {
                    _logService.Info("用户选择了休息结束后锁屏，正在执行锁屏操作");
                    bool result = LockWorkStation();
                    if (result)
                    {
                        _logService.Info("锁屏操作执行成功");
                    }
                    else
                    {
                        _logService.Warn("锁屏操作执行失败");
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "执行锁屏操作时发生错误");
            }
        }
    }
}
