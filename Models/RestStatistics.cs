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

    /// <summary>
    /// 输入事件计数器（内存中实时累计）
    /// </summary>
    public class InputEventCounter
    {
        /// <summary>
        /// 键盘按键次数
        /// </summary>
        public int KeyPressCount { get; set; }

        /// <summary>
        /// 鼠标移动距离（像素）
        /// </summary>
        public long MouseMoveDistance { get; set; }

        /// <summary>
        /// 鼠标左键点击次数
        /// </summary>
        public int MouseLeftClickCount { get; set; }

        /// <summary>
        /// 鼠标右键点击次数
        /// </summary>
        public int MouseRightClickCount { get; set; }

        /// <summary>
        /// 鼠标中键点击次数
        /// </summary>
        public int MouseMiddleClickCount { get; set; }

        /// <summary>
        /// 鼠标滚轮滚动次数
        /// </summary>
        public int MouseWheelScrollCount { get; set; }

        /// <summary>
        /// 上次鼠标位置（用于计算移动距离）
        /// </summary>
        public System.Drawing.Point LastMousePosition { get; set; }

        /// <summary>
        /// 是否有上次位置记录
        /// </summary>
        public bool HasLastPosition { get; set; }

        /// <summary>
        /// 重置计数器
        /// </summary>
        public void Reset()
        {
            KeyPressCount = 0;
            MouseMoveDistance = 0;
            MouseLeftClickCount = 0;
            MouseRightClickCount = 0;
            MouseMiddleClickCount = 0;
            MouseWheelScrollCount = 0;
            HasLastPosition = false;
        }

        /// <summary>
        /// 累加另一个计数器的数据
        /// </summary>
        public void Add(InputEventCounter other)
        {
            KeyPressCount += other.KeyPressCount;
            MouseMoveDistance += other.MouseMoveDistance;
            MouseLeftClickCount += other.MouseLeftClickCount;
            MouseRightClickCount += other.MouseRightClickCount;
            MouseMiddleClickCount += other.MouseMiddleClickCount;
            MouseWheelScrollCount += other.MouseWheelScrollCount;
        }
    }
}
