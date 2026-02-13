namespace eyesharp.Models
{
    /// <summary>
    /// 密码哈希数据模型
    /// </summary>
    public class PasswordData
    {
        /// <summary>
        /// 迭代次数
        /// </summary>
        public int Iterations { get; set; }

        /// <summary>
        /// 盐值（Base64编码）
        /// </summary>
        public string Salt { get; set; } = string.Empty;

        /// <summary>
        /// 密码哈希值（Base64编码）
        /// </summary>
        public string Hash { get; set; } = string.Empty;

        /// <summary>
        /// 转换为配置文件存储格式
        /// </summary>
        public string ToConfigString()
        {
            return $"{Iterations}:{Salt}:{Hash}";
        }

        /// <summary>
        /// 从配置文件格式解析
        /// </summary>
        public static PasswordData? FromConfigString(string configString)
        {
            if (string.IsNullOrWhiteSpace(configString))
                return null;

            var parts = configString.Split(':');
            if (parts.Length != 3)
                return null;

            if (!int.TryParse(parts[0], out int iterations))
                return null;

            return new PasswordData
            {
                Iterations = iterations,
                Salt = parts[1],
                Hash = parts[2]
            };
        }
    }
}
