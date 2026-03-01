using System;
using eyesharp.Helpers;
using eyesharp.Models;

namespace eyesharp.Services
{
    /// <summary>
    /// 密码服务实现
    /// </summary>
    public class PasswordService : IPasswordService
    {
        /// <summary>
        /// 哈希密码
        /// </summary>
        public string HashPassword(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("密码不能为空", nameof(password));

            // 生成盐值
            var salt = CryptoHelper.GenerateSalt();

            // 哈希密码
            var hash = CryptoHelper.HashPassword(password, salt, CryptoHelper.DefaultIterations);

            // 转换为存储格式
            var passwordData = new PasswordData
            {
                Iterations = CryptoHelper.DefaultIterations,
                Salt = CryptoHelper.ToBase64String(salt),
                Hash = CryptoHelper.ToBase64String(hash)
            };

            return passwordData.ToConfigString();
        }

        /// <summary>
        /// 验证密码
        /// </summary>
        public bool VerifyPassword(string password, string passwordHash)
        {
            if (string.IsNullOrEmpty(password))
                return false;

            if (string.IsNullOrEmpty(passwordHash))
                return false;

            var passwordData = PasswordData.FromConfigString(passwordHash);
            if (passwordData == null)
                return false;

            var salt = CryptoHelper.FromBase64String(passwordData.Salt);
            var hash = CryptoHelper.FromBase64String(passwordData.Hash);

            return CryptoHelper.VerifyPassword(password, salt, hash, passwordData.Iterations);
        }

        /// <summary>
        /// 检查密码是否已设置
        /// </summary>
        public bool IsPasswordSet(string passwordHash)
        {
            return !string.IsNullOrEmpty(passwordHash);
        }

        /// <summary>
        /// 验证密码强度
        /// </summary>
        public bool ValidatePasswordStrength(string password)
        {
            // 密码长度必须在4-20字符之间
            if (string.IsNullOrEmpty(password))
                return false;

            if (password.Length < 4 || password.Length > 20)
                return false;

            return true;
        }

        /// <summary>
        /// 获取密码强度信息
        /// </summary>
        public PasswordStrength GetPasswordStrength(string password)
        {
            if (password == null)
                throw new ArgumentNullException(nameof(password));

            var strength = new PasswordStrength();

            // 检查长度
            if (password.Length < 4)
            {
                strength.Level = 0;
                strength.Description = "太短";
                strength.Message = "密码长度至少4个字符";
                return strength;
            }

            if (password.Length > 20)
            {
                strength.Level = 0;
                strength.Description = "太长";
                strength.Message = "密码长度不能超过20个字符";
                return strength;
            }

            // 计算强度
            int score = 0;

            // 长度得分
            if (password.Length >= 6) score++;
            if (password.Length >= 8) score++;
            if (password.Length >= 12) score++;

            // 字符类型得分
            bool hasLower = false;
            bool hasUpper = false;
            bool hasDigit = false;
            bool hasSpecial = false;

            foreach (char c in password)
            {
                if (char.IsLower(c)) hasLower = true;
                else if (char.IsUpper(c)) hasUpper = true;
                else if (char.IsDigit(c)) hasDigit = true;
                else hasSpecial = true;
            }

            int charTypes = (hasLower ? 1 : 0) +
                           (hasUpper ? 1 : 0) +
                           (hasDigit ? 1 : 0) +
                           (hasSpecial ? 1 : 0);

            score += charTypes;

            // 判定强度等级
            if (score <= 3)
            {
                strength.Level = 1;
                strength.Description = "弱";
                strength.Message = "建议使用更复杂的密码";
            }
            else if (score <= 5)
            {
                strength.Level = 2;
                strength.Description = "中";
                strength.Message = "密码强度中等";
            }
            else
            {
                strength.Level = 3;
                strength.Description = "强";
                strength.Message = "密码强度很好";
            }

            return strength;
        }
    }
}
