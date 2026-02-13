using System.Windows;
using System.Windows.Controls;
using eyesharp.ViewModels;

namespace eyesharp.Views
{
    /// <summary>
    /// 设置密码对话框
    /// </summary>
    public partial class SetPasswordDialog : Window
    {
        private readonly SetPasswordDialogViewModel _viewModel;

        public SetPasswordDialog()
        {
            InitializeComponent();
            _viewModel = new SetPasswordDialogViewModel();
            DataContext = _viewModel;

            // 禁用关闭按钮，强制用户设置密码
            this.Closing += (s, e) =>
            {
                if (!_viewModel.IsPasswordSet && !_viewModel.IsCancelled)
                {
                    e.Cancel = true;
                }
            };
        }

        /// <summary>
        /// 获取设置的密码
        /// </summary>
        public string? Password => _viewModel.Password;

        private void OnPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                _viewModel.Password = passwordBox.Password;
            }
        }

        private void OnConfirmPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                _viewModel.ConfirmPassword = passwordBox.Password;
            }
        }

        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            if (_viewModel.ValidateAndSetPassword())
            {
                _viewModel.IsPasswordSet = true;
                DialogResult = true;
                Close();
            }
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            _viewModel.IsCancelled = true;
            DialogResult = false;
            Close();
        }
    }
}
