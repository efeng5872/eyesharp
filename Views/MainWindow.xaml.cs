using System.Windows;
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
    }
}
