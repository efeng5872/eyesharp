using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using eyesharp.ViewModels;
using eyesharp.Services;

namespace eyesharp.Views
{
    /// <summary>
    /// ChangePasswordDialog.xaml 的交互逻辑
    /// </summary>
    public partial class ChangePasswordDialog : Window
    {
        private readonly ChangePasswordDialogViewModel _viewModel;
        private readonly ILogService _logService;

        public string? NewPasswordHash { get; private set; }

        public ChangePasswordDialog(IPasswordService passwordService, string currentPasswordHash, ILogService logService)
        {
            InitializeComponent();

            _viewModel = new ChangePasswordDialogViewModel(passwordService, currentPasswordHash);
            _logService = logService;

            DataContext = _viewModel;

            // 订阅属性变化
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(ChangePasswordDialogViewModel.ErrorMessage))
                {
                    UpdateErrorMessage();
                }
                else if (e.PropertyName == nameof(ChangePasswordDialogViewModel.IsOkButtonEnabled))
                {
                    OkButton.IsEnabled = _viewModel.IsOkButtonEnabled;
                }
                else if (e.PropertyName == nameof(ChangePasswordDialogViewModel.PasswordStrength))
                {
                    UpdatePasswordStrength();
                }
            };
        }

        /// <summary>
        /// 旧密码输入框变化
        /// </summary>
        private void OnOldPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                _viewModel.OldPassword = passwordBox.Password;
            }
        }

        /// <summary>
        /// 新密码输入框变化
        /// </summary>
        private void OnNewPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                _viewModel.NewPassword = passwordBox.Password;
            }
        }

        /// <summary>
        /// 确认密码输入框变化
        /// </summary>
        private void OnConfirmPasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                _viewModel.ConfirmPassword = passwordBox.Password;
            }
        }

        /// <summary>
        /// 更新错误消息显示
        /// </summary>
        private void UpdateErrorMessage()
        {
            if (string.IsNullOrEmpty(_viewModel.ErrorMessage))
            {
                ErrorMessage.Visibility = Visibility.Collapsed;
            }
            else
            {
                ErrorMessage.Text = _viewModel.ErrorMessage;
                ErrorMessage.Visibility = Visibility.Visible;
            }
        }

        /// <summary>
        /// 更新密码强度显示
        /// </summary>
        private void UpdatePasswordStrength()
        {
            if (string.IsNullOrEmpty(_viewModel.PasswordStrength))
            {
                PasswordStrengthText.Text = "";
            }
            else
            {
                PasswordStrengthText.Text = $"强度: {_viewModel.PasswordStrength}";
                PasswordStrengthText.Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString(_viewModel.PasswordStrengthColor)
                );
            }
        }

        /// <summary>
        /// 确定按钮点击
        /// </summary>
        private void OnOkClick(object sender, RoutedEventArgs e)
        {
            _logService.Info("用户提交修改密码");

            var newPasswordHash = _viewModel.ValidateAndGetNewPasswordHash();
            if (newPasswordHash != null)
            {
                NewPasswordHash = newPasswordHash;
                _viewModel.IsCancelled = false;
                DialogResult = true;
                Close();
            }
        }

        /// <summary>
        /// 取消按钮点击
        /// </summary>
        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            _logService.Info("用户取消修改密码");
            _viewModel.IsCancelled = true;
            DialogResult = false;
            Close();
        }
    }
}
