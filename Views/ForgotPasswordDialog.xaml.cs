using System.Windows;

namespace eyesharp.Views
{
    /// <summary>
    /// 忘记密码/重置对话框
    /// </summary>
    public partial class ForgotPasswordDialog : Window
    {
        public bool IsConfirmed { get; private set; } = false;

        public ForgotPasswordDialog()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 取消按钮点击
        /// </summary>
        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// 确认重置按钮点击
        /// </summary>
        private void OnConfirmClick(object sender, RoutedEventArgs e)
        {
            IsConfirmed = true;
            DialogResult = true;
            Close();
        }
    }
}
