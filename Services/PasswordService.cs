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
    }
}
