using System;
using System.IO;
using System.Linq;
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
    /// RestWindow.xaml 的交互逻辑
    /// </summary>
    public partial class RestWindow : Window
    {
        private readonly IConfigService _configService;
        private readonly IPasswordService _passwordService;
        private readonly ILogService _logService;
        private readonly AppConfig _config;
        private readonly TimerState _previousState;

        private string[]? _imageFiles;
        private int _currentImageIndex = 0;
        private System.Threading.Timer? _imageSwitchTimer;

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

            // 确保窗口在最前面并激活
            Topmost = true;
            Show();
            Activate();
            Focus();

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
                BackgroundImage.Visibility = Visibility.Collapsed;
                BackgroundBrush.Color = Colors.Black;
            }
            else if (_config.RestWindowMode == "image")
            {
                // 图片模式
                BackgroundImage.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 设置控制面板（强制/非强制模式）
        /// </summary>
        private void SetControlPanel()
        {
            if (_config.IsForcedMode)
            {
                // 强制模式：显示密码输入
                ForcedModePanel.Visibility = Visibility.Visible;
                SkipRestButton.Visibility = Visibility.Collapsed;
            }
            else
            {
                // 非强制模式：显示跳过按钮
                ForcedModePanel.Visibility = Visibility.Collapsed;
                SkipRestButton.Visibility = Visibility.Visible;
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
                    _logService.Warn("自定义图片文件夹无效或为空，使用内置图片");
                    // TODO: 加载内置图片（暂时使用黑色背景）
                    _imageFiles = new string[0];
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
        /// 加载指定索引的图片
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

                BackgroundImage.Source = bitmap;
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

            // 每10秒切换一次图片
            _imageSwitchTimer = new System.Threading.Timer(state =>
            {
                Dispatcher.Invoke(() =>
                {
                    _currentImageIndex = (_currentImageIndex + 1) % _imageFiles!.Length;
                    LoadImage(_currentImageIndex);
                });
            }, null, 10000, 10000);
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
        /// 提前结束按钮点击（强制模式）
        /// </summary>
        private void OnEndRestClick(object sender, RoutedEventArgs e)
        {
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
                _logService.Warn("用户输入错误密码");
                ShowErrorMessage("密码错误");
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
            // 简单的错误提示
            var originalText = CountdownText.Text;
            CountdownText.Text = message;
            CountdownText.Foreground = new SolidColorBrush(Colors.Red);

            var timer = new System.Threading.Timer(state =>
            {
                Dispatcher.Invoke(() =>
                {
                    CountdownText.Text = originalText;
                    CountdownText.Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80));
                });
            }, null, 2000, Timeout.Infinite);
        }

        /// <summary>
        /// 关闭休息窗口
        /// </summary>
        private void CloseRestWindow()
        {
            // 停止图片轮播定时器
            _imageSwitchTimer?.Dispose();

            Close();
        }

        /// <summary>
        /// 窗口关闭时清理
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            _imageSwitchTimer?.Dispose();
            _logService.Info("休息窗口已关闭");
            base.OnClosed(e);
        }
    }
}
