namespace eyesharp.Models
{
    /// <summary>
    /// 应用程序配置数据模型
    /// </summary>
    public class AppConfig
    {
        /// <summary>
        /// 休息间隔时间（分钟）
        /// </summary>
        public int RestIntervalMinutes { get; set; } = 30;

        /// <summary>
        /// 休息时长（秒）
        /// </summary>
        public int RestDurationSeconds { get; set; } = 20;

        /// <summary>
        /// 是否强制模式
        /// </summary>
        public bool IsForcedMode { get; set; } = true;

        /// <summary>
        /// 休息窗口模式："image" 或 "black"
        /// </summary>
        public string RestWindowMode { get; set; } = "black";

        /// <summary>
        /// 自定义图片文件夹路径
        /// </summary>
        public string CustomImagePath { get; set; } = "";

        /// <summary>
        /// 密码哈希值（格式：iterations:salt:hash）
        /// </summary>
        public string PasswordHash { get; set; } = "";

        /// <summary>
        /// 开机自启动
        /// </summary>
        public bool AutoStart { get; set; } = false;

        /// <summary>
        /// 日志级别：DEBUG/INFO/WARN/ERROR
        /// </summary>
        public string LogLevel { get; set; } = "INFO";
    }
}
