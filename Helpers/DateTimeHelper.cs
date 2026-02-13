using System;

namespace eyesharp.Helpers
{
    /// <summary>
    /// 日期时间辅助类
    /// </summary>
    public static class DateTimeHelper
    {
        /// <summary>
        /// 北京时区（UTC+8）
        /// </summary>
        private static readonly TimeZoneInfo BeijingTimeZone = TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");

        /// <summary>
        /// 获取当前北京时间
        /// </summary>
        public static DateTime NowBeijingTime => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BeijingTimeZone);

        /// <summary>
        /// 格式化时间为北京时间字符串（yyyy-MM-dd HH:mm:ss）
        /// </summary>
        public static string FormatBeijingTime(DateTime dateTime)
        {
            var beijingTime = TimeZoneInfo.ConvertTimeFromUtc(dateTime.ToUniversalTime(), BeijingTimeZone);
            return beijingTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// 格式化当前时间为北京时间字符串
        /// </summary>
        public static string FormatCurrentBeijingTime()
        {
            return FormatBeijingTime(DateTime.Now);
        }

        /// <summary>
        /// 将秒数转换为可读的时分秒格式
        /// </summary>
        public static string FormatDuration(int totalSeconds)
        {
            if (totalSeconds < 60)
                return $"{totalSeconds}秒";

            if (totalSeconds < 3600)
            {
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                return seconds > 0 ? $"{minutes}分{seconds}秒" : $"{minutes}分钟";
            }

            int hours = totalSeconds / 3600;
            int minutes2 = (totalSeconds % 3600) / 60;
            int seconds2 = totalSeconds % 60;

            if (minutes2 > 0 && seconds2 > 0)
                return $"{hours}小时{minutes2}分{seconds2}秒";
            else if (minutes2 > 0)
                return $"{hours}小时{minutes2}分钟";
            else
                return $"{hours}小时";
        }

        /// <summary>
        /// 将秒数转换为倒计时显示字符串（HH:mm:ss 或 mm:ss）
        /// </summary>
        public static string FormatCountdown(int totalSeconds)
        {
            if (totalSeconds < 0)
                totalSeconds = 0;

            int hours = totalSeconds / 3600;
            int minutes = (totalSeconds % 3600) / 60;
            int seconds = totalSeconds % 60;

            if (hours > 0)
                return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
            else
                return $"{minutes:D2}:{seconds:D2}";
        }
    }
}
