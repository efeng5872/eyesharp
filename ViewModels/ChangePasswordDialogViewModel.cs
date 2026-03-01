using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using eyesharp.Services;

namespace eyesharp.ViewModels
{
    /// <summary>
    /// 修改密码对话框 ViewModel
    /// </summary>
    public partial class ChangePasswordDialogViewModel : ObservableObject
    {
        private readonly IPasswordService _passwordService;
        private readonly string _currentPasswordHash;

        [ObservableProperty]
        private string _oldPassword = "";

        [ObservableProperty]
        private string _newPassword = "";

        [ObservableProperty]
        private string _confirmPassword = "";

        [ObservableProperty]
        private string _errorMessage = "";

        [ObservableProperty]
        private string _passwordStrength = "";

        [ObservableProperty]
        private string _passwordStrengthColor = "#999999";

        [ObservableProperty]
        private bool _isOkButtonEnabled = false;

        [ObservableProperty]
        private bool _isCancelled = false;

        public ChangePasswordDialogViewModel(IPasswordService passwordService, string currentPasswordHash)
        {
            _passwordService = passwordService;
            _currentPasswordHash = currentPasswordHash;
        }

        partial void OnOldPasswordChanged(string value)
        {
            ValidatePasswords();
        }

        partial void OnNewPasswordChanged(string value)
        {
            UpdatePasswordStrength(value);
            ValidatePasswords();
        }

        partial void OnConfirmPasswordChanged(string value)
        {
            ValidatePasswords();
        }

        /// <summary>
        /// 更新密码强度显示
        /// </summary>
        private void UpdatePasswordStrength(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                PasswordStrength = "";
                PasswordStrengthColor = "#999999";
                return;
            }

            var strength = _passwordService.GetPasswordStrength(password);

            switch (strength.Level)
            {
                case 1: // 弱
                    PasswordStrength = "弱";
                    PasswordStrengthColor = "#F44336"; // 红色
                    break;
                case 2: // 中
                    PasswordStrength = "中";
                    PasswordStrengthColor = "#FF9800"; // 橙色
                    break;
                case 3: // 强
                    PasswordStrength = "强";
                    PasswordStrengthColor = "#4CAF50"; // 绿色
                    break;
                default:
                    PasswordStrength = "";
                    PasswordStrengthColor = "#999999";
                    break;
            }
        }

        /// <summary>
        /// 验证密码
        /// </summary>
        private void ValidatePasswords()
        {
            // 检查旧密码
            if (string.IsNullOrEmpty(OldPassword))
            {
                ErrorMessage = "";
                IsOkButtonEnabled = false;
                return;
            }

            // 验证旧密码是否正确
            if (!_passwordService.VerifyPassword(OldPassword, _currentPasswordHash))
            {
                ErrorMessage = "旧密码不正确";
                IsOkButtonEnabled = false;
                return;
            }

            // 检查新密码长度
            if (NewPassword.Length > 0 && (NewPassword.Length < 4 || NewPassword.Length > 20))
            {
                ErrorMessage = "新密码长度必须在4-20字符之间";
                IsOkButtonEnabled = false;
                return;
            }

            // 检查新密码是否与旧密码相同
            if (!string.IsNullOrEmpty(NewPassword) && NewPassword == OldPassword)
            {
                ErrorMessage = "新密码不能与旧密码相同";
                IsOkButtonEnabled = false;
                return;
            }

            // 检查确认密码
            if (!string.IsNullOrEmpty(NewPassword) && ConfirmPassword.Length > 0 && NewPassword != ConfirmPassword)
            {
                ErrorMessage = "两次输入的新密码不一致";
                IsOkButtonEnabled = false;
                return;
            }

            // 检查是否所有字段都已填写
            if (string.IsNullOrEmpty(OldPassword) || string.IsNullOrEmpty(NewPassword) || string.IsNullOrEmpty(ConfirmPassword))
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
        /// 验证并返回新密码哈希
        /// </summary>
        public string? ValidateAndGetNewPasswordHash()
        {
            // 验证旧密码
            if (!_passwordService.VerifyPassword(OldPassword, _currentPasswordHash))
            {
                ErrorMessage = "旧密码不正确";
                return null;
            }

            // 验证新密码强度
            if (!_passwordService.ValidatePasswordStrength(NewPassword))
            {
                ErrorMessage = "密码长度必须在4-20字符之间";
                return null;
            }

            // 验证两次密码是否一致
            if (NewPassword != ConfirmPassword)
            {
                ErrorMessage = "两次输入的新密码不一致";
                return null;
            }

            // 验证新密码不能与旧密码相同
            if (NewPassword == OldPassword)
            {
                ErrorMessage = "新密码不能与旧密码相同";
                return null;
            }

            // 生成新密码哈希
            return _passwordService.HashPassword(NewPassword);
        }
    }
}
