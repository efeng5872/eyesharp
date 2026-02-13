namespace eyesharp.Services
{
    /// <summary>
    /// 密码服务接口
    /// </summary>
    public interface IPasswordService
    {
        /// <summary>
        /// 哈希密码
        /// </summary>
        string HashPassword(string password);

        /// <summary>
        /// 验证密码
        /// </summary>
        bool VerifyPassword(string password, string passwordHash);

        /// <summary>
        /// 检查密码是否已设置
        /// </summary>
        bool IsPasswordSet(string passwordHash);

        /// <summary>
        /// 验证密码强度
        /// </summary>
        bool ValidatePasswordStrength(string password);
    }
}
