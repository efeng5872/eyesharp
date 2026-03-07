using System;

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

        /// <summary>
        /// 是否启用休息前预提醒
        /// </summary>
        public bool IsPreReminderEnabled { get; set; } = true;

        /// <summary>
        /// 预提醒时间间隔（秒），默认为 30,10 表示30秒和10秒各提醒一次
        /// </summary>
        public int[] PreReminderIntervals { get; set; } = { 30, 10 };

        /// <summary>
        /// 主题模式："light" 或 "dark"
        /// </summary>
        public string Theme { get; set; } = "light";

        /// <summary>
        /// 锁屏时的处理方式："pause"=暂停倒计时, "skip"=跳过本次休息, "normal"=正常显示
        /// </summary>
        public string LockScreenBehavior { get; set; } = LockScreenBehaviorConverter.PauseValue;
    }

    /// <summary>
    /// 锁屏处理行为（业务层强类型）
    /// </summary>
    public enum LockScreenBehaviorType
    {
        Normal,
        Pause,
        Skip
    }

    /// <summary>
    /// 锁屏处理行为转换器（配置层 string ↔ 业务层 enum）
    /// </summary>
    public static class LockScreenBehaviorConverter
    {
        public const string NormalValue = "normal";
        public const string PauseValue = "pause";
        public const string SkipValue = "skip";

        public static LockScreenBehaviorType FromConfig(string? value)
        {
            if (string.Equals(value, SkipValue, StringComparison.OrdinalIgnoreCase))
                return LockScreenBehaviorType.Skip;

            if (string.Equals(value, NormalValue, StringComparison.OrdinalIgnoreCase))
                return LockScreenBehaviorType.Normal;

            // 默认：pause（与当前产品默认一致）
            return LockScreenBehaviorType.Pause;
        }

        public static string ToConfig(LockScreenBehaviorType behavior)
        {
            return behavior switch
            {
                LockScreenBehaviorType.Normal => NormalValue,
                LockScreenBehaviorType.Skip => SkipValue,
                _ => PauseValue
            };
        }
    }
}
