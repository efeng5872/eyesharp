using System;
using System.Collections.Generic;

namespace eyesharp.Models
{
    /// <summary>
    /// 小时级详细记录
    /// </summary>
    public class HourlyActivityRecord
    {
        public DateTime Hour { get; set; }

        // 时间分布
        public int ActiveSeconds { get; set; }
        public int IdleSeconds { get; set; }
        public int LockScreenSeconds { get; set; }
        public int LockScreenCount { get; set; }

        // 输入统计
        public int KeyPressCount { get; set; }
        public long MouseMoveDistance { get; set; }
        public int MouseLeftClickCount { get; set; }
        public int MouseRightClickCount { get; set; }
        public int MouseMiddleClickCount { get; set; }
        public int MouseWheelScrollCount { get; set; }

        // 状态标记
        public bool IsCompleteHour { get; set; } = true;
        public DateTime FirstRecordTime { get; set; }
        public DateTime LastUpdateTime { get; set; }
    }

    /// <summary>
    /// 日汇总统计
    /// </summary>
    public class DailyUsageStatistics
    {
        public DateTime Date { get; set; }

        // 时间统计
        public int TotalBootSeconds { get; set; }
        public int ActiveSeconds { get; set; }
        public int IdleSeconds { get; set; }
        public int LockScreenSeconds { get; set; }
        public int LockScreenCount { get; set; }

        // 输入统计汇总
        public int TotalKeyPressCount { get; set; }
        public long TotalMouseMoveDistance { get; set; }
        public int TotalMouseLeftClickCount { get; set; }
        public int TotalMouseRightClickCount { get; set; }
        public int TotalMouseMiddleClickCount { get; set; }
        public int TotalMouseWheelScrollCount { get; set; }

        // 24小时详细数据（仅保留最近90天）
        public List<HourlyActivityRecord>? HourlyData { get; set; }

        // 元数据
        public DateTime FirstRecordTime { get; set; }
        public DateTime LastRecordTime { get; set; }

        // 计算属性
        public double MouseMoveDistanceMeters => TotalMouseMoveDistance / 3779.5;
        public double AvgKeyPressPerHour => TotalBootSeconds > 0 ? TotalKeyPressCount / (TotalBootSeconds / 3600.0) : 0;
        public double AvgClickPerHour => TotalBootSeconds > 0 ? (TotalMouseLeftClickCount + TotalMouseRightClickCount) / (TotalBootSeconds / 3600.0) : 0;
    }

    /// <summary>
    /// 实时状态（内存中，用于UI显示）
    /// </summary>
    public class RealTimeUsageStatus
    {
        public DateTime CurrentHour { get; set; }
        public UserActivityState CurrentState { get; set; }
        public DateTime StateStartTime { get; set; }

        // 今日累计（从内存计算）
        public TimeSpan TodayActiveTime { get; set; }
        public TimeSpan TodayIdleTime { get; set; }
        public InputEventCounter TodayInputCounter { get; set; } = new();

        // 当前状态持续时间
        public TimeSpan CurrentStateDuration => DateTime.Now - StateStartTime;
    }

    /// <summary>
    /// 用户活动状态
    /// </summary>
    public enum UserActivityState
    {
        Unknown,        // 未知/初始化中
        Active,         // 活跃（有输入）
        Idle,           // 空闲（无输入）
        Locked          // 锁屏
    }
}
