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
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            // 动态设置窗口图标
            LoadWindowIcon();

            // 窗口加载完成后启动倒计时
            Loaded += (s, e) => viewModel.StartCountdown();

            // 窗口关闭时最小化到托盘
            Closing += (s, e) =>
            {
                if (viewModel.ShowInTray)
                {
                    e.Cancel = true;
                    Hide();
                }
            };
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
