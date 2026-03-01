using System.Windows;

namespace eyesharp.Views
{
    /// <summary>
    /// ExitConfirmDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ExitConfirmDialog : Window
    {
        public ExitConfirmDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 取消按钮点击
        /// </summary>
        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 确定退出按钮点击
        /// </summary>
        private void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
