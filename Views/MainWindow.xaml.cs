using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using eyesharp.ViewModels;

namespace eyesharp.Views
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        // 窗口位置记忆（使用可空类型消除冗余标志）
        private double? _savedLeft;
        private double? _savedTop;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // 动态设置窗口图标
            LoadWindowIcon();

            // 窗口加载完成后启动倒计时
            Loaded += OnWindowLoaded;

            // 窗口关闭时最小化到托盘
            Closing += (s, e) =>
            {
                if (viewModel.ShowInTray)
                {
                    e.Cancel = true;
                    // 隐藏前保存窗口位置
                    SaveWindowPosition();
                    Hide();
                }
            };
        }

        /// <summary>
        /// 窗口加载事件处理
        /// </summary>
        private void OnWindowLoaded(object? sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel viewModel)
            {
                viewModel.StartCountdown();
            }
        }

        /// <summary>
        /// 保存当前窗口位置
        /// </summary>
        public void SaveWindowPosition()
        {
            _savedLeft = Left;
            _savedTop = Top;
        }

        /// <summary>
        /// 恢复窗口位置（如果已保存）
        /// </summary>
        public void RestoreWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();

            // 如果之前保存过位置，恢复到保存的位置
            if (_savedLeft.HasValue)
            {
                Left = _savedLeft.Value;
                Top = _savedTop.Value;
            }
        }

        /// <summary>
        /// 动态加载窗口图标
        /// </summary>
        private void LoadWindowIcon()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var iconPath = Path.Combine(baseDir, "Resources", "Icons", "tray_normal_64x64.png");

                // 如果文件不存在，尝试其他尺寸
                if (!File.Exists(iconPath))
                {
                    iconPath = Path.Combine(baseDir, "Resources", "Icons", "tray_normal.png");
                }

                if (File.Exists(iconPath))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(iconPath, UriKind.Absolute);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    Icon = bitmap;
                }
            }
            catch (Exception)
            {
                // 加载失败时忽略，使用默认图标
            }
        }
    }
}
