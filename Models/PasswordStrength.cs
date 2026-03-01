namespace eyesharp.Models
{
    /// <summary>
    /// 密码强度信息
    /// </summary>
    public class PasswordStrength
    {
        /// <summary>
        /// 强度等级（1=弱, 2=中, 3=强）
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// 强度描述
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// 强度提示信息
        /// </summary>
        public string Message { get; set; } = "";
    }
}
