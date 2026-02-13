using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using eyesharp.Services;

namespace eyesharp.ViewModels
{
    /// <summary>
    /// 设置密码对话框 ViewModel
    /// </summary>
    public partial class SetPasswordDialogViewModel : ObservableObject
    {
        private readonly IPasswordService _passwordService;

        [ObservableProperty]
        private string _password = "";

        [ObservableProperty]
        private string _confirmPassword = "";

        [ObservableProperty]
        private string _errorMessage = "";

        [ObservableProperty]
        private bool _isOkButtonEnabled = false;

        [ObservableProperty]
        private bool _isPasswordSet = false;

        [ObservableProperty]
        private bool _isCancelled = false;

        public SetPasswordDialogViewModel()
        {
            // 注意：这里暂时直接创建服务实例，实际应从依赖注入获取
            _passwordService = new PasswordService();
        }

        partial void OnPasswordChanged(string value)
        {
            ValidatePasswords();
        }

        partial void OnConfirmPasswordChanged(string value)
        {
            ValidatePasswords();
        }

        /// <summary>
        /// 验证密码
        /// </summary>
        private void ValidatePasswords()
        {
            // 检查密码长度
            if (Password.Length < 4 || Password.Length > 20)
            {
                if (Password.Length > 0)
                {
                    ErrorMessage = "密码长度必须在4-20字符之间";
                }
                else
                {
                    ErrorMessage = "";
                }
                IsOkButtonEnabled = false;
                return;
            }

            // 检查确认密码
            if (ConfirmPassword.Length > 0 && Password != ConfirmPassword)
            {
                ErrorMessage = "两次输入的密码不一致";
                IsOkButtonEnabled = false;
                return;
            }

            // 检查确认密码是否已输入
            if (ConfirmPassword.Length == 0)
            {
                ErrorMessage = "";
                IsOkButtonEnabled = false;
                return;
            }

            // 验证通过
            ErrorMessage = "";
            IsOkButtonEnabled = true;
        }

        /// <summary>
        /// 验证并设置密码
        /// </summary>
        public bool ValidateAndSetPassword()
        {
            // 使用密码服务验证密码强度
            if (!_passwordService.ValidatePasswordStrength(Password))
            {
                ErrorMessage = "密码长度必须在4-20字符之间";
                return false;
            }

            // 检查两次密码是否一致
            if (Password != ConfirmPassword)
            {
                ErrorMessage = "两次输入的密码不一致";
                return false;
            }

            return true;
        }
    }
}
