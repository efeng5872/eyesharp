using System;
using System.Security.Cryptography;
using System.Text;

namespace eyesharp.Helpers
{
    /// <summary>
    /// 加密辅助类
    /// </summary>
    public static class CryptoHelper
    {
        /// <summary>
        /// 默认PBKDF2迭代次数
        /// </summary>
        public const int DefaultIterations = 10000;

        /// <summary>
        /// 盐值字节长度
        /// </summary>
        public const int SaltSize = 16;

        /// <summary>
        /// 哈希值字节长度
        /// </summary>
        public const int HashSize = 32;

        /// <summary>
        /// 使用PBKDF2-HMAC-SHA256算法哈希密码
        /// </summary>
        /// <param name="password">明文密码</param>
        /// <param name="salt">盐值字节数组</param>
        /// <param name="iterations">迭代次数</param>
        /// <returns>密码哈希值</returns>
        public static byte[] HashPassword(string password, byte[] salt, int iterations = DefaultIterations)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("密码不能为空", nameof(password));

            if (salt == null || salt.Length != SaltSize)
                throw new ArgumentException($"盐值必须为{SaltSize}字节", nameof(salt));

            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256);

            return pbkdf2.GetBytes(HashSize);
        }

        /// <summary>
        /// 生成随机盐值
        /// </summary>
        /// <returns>随机盐值字节数组</returns>
        public static byte[] GenerateSalt()
        {
            var salt = new byte[SaltSize];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            return salt;
        }

        /// <summary>
        /// 验证密码
        /// </summary>
        /// <param name="password">明文密码</param>
        /// <param name="salt">盐值字节数组</param>
        /// <param name="hash">密码哈希值</param>
        /// <param name="iterations">迭代次数</param>
        /// <returns>验证是否通过</returns>
        public static bool VerifyPassword(string password, byte[] salt, byte[] hash, int iterations)
        {
            var computedHash = HashPassword(password, salt, iterations);
            return CryptographicOperations.FixedTimeEquals(computedHash, hash);
        }

        /// <summary>
        /// 将字节数组转换为Base64字符串
        /// </summary>
        public static string ToBase64String(byte[] bytes)
        {
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// 将Base64字符串转换为字节数组
        /// </summary>
        public static byte[] FromBase64String(string base64)
        {
            return Convert.FromBase64String(base64);
        }
    }
}
