using System;
using System.Collections.Generic;

namespace eyesharp.Models
{
    /// <summary>
    /// 单次休息记录
    /// </summary>
    public class RestRecord
    {
        /// <summary>
        /// 记录ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// 休息开始时间
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// 休息结束时间
        /// </summary>
        public DateTime EndTime { get; set; }

        /// <summary>
        /// 休息时长（秒）
        /// </summary>
        public int DurationSeconds { get; set; }

        /// <summary>
        /// 计划休息时长（秒）
        /// </summary>
        public int PlannedDurationSeconds { get; set; }

        /// <summary>
        /// 是否正常完成（false表示提前结束）
        /// </summary>
        public bool IsCompleted { get; set; }

        /// <summary>
        /// 是否强制模式
        /// </summary>
        public bool IsForcedMode { get; set; }
    }

    /// <summary>
    /// 每日统计
    /// </summary>
    public class DailyStatistics
    {
        /// <summary>
        /// 日期
        /// </summary>
        public DateTime Date { get; set; }

        /// <summary>
        /// 休息次数
        /// </summary>
        public int RestCount { get; set; }

        /// <summary>
        /// 总休息时长（秒）
        /// </summary>
        public int TotalDurationSeconds { get; set; }

        /// <summary>
        /// 平均休息时长（秒）
        /// </summary>
        public int AverageDurationSeconds { get; set; }

        /// <summary>
        /// 正常完成次数
        /// </summary>
        public int CompletedCount { get; set; }

        /// <summary>
        /// 提前结束次数
        /// </summary>
        public int SkippedCount { get; set; }
    }

    /// <summary>
    /// 统计数据容器
    /// </summary>
    public class StatisticsData
    {
        /// <summary>
        /// 版本号
        /// </summary>
        public int Version { get; set; } = 1;

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        /// <summary>
        /// 所有休息记录
        /// </summary>
        public List<RestRecord> Records { get; set; } = new List<RestRecord>();

        /// <summary>
        /// 每日统计缓存
        /// </summary>
        public List<DailyStatistics> DailyStats { get; set; } = new List<DailyStatistics>();
    }
}
