using System;
using System.Threading.Tasks;
using eyesharp.Models;

namespace eyesharp.Services.UsageStats
{
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

    /// <summary>
    /// 活动状态变化事件参数
    /// </summary>
    public class ActivityStateChangedEventArgs : EventArgs
    {
        public UserActivityState OldState { get; set; }
        public UserActivityState NewState { get; set; }
        public DateTime ChangeTime { get; set; }
        public TimeSpan PreviousStateDuration { get; set; }
        public InputEventCounter CounterSnapshot { get; set; } = new();
    }

    /// <summary>
    /// 输入计数事件参数
    /// </summary>
    public class InputCounterEventArgs : EventArgs
    {
        public DateTime RecordTime { get; set; }
        public InputEventCounter Counter { get; set; } = new();
        public UserActivityState CurrentState { get; set; }
    }

    /// <summary>
    /// 输入监控服务接口
    /// </summary>
    public interface IInputMonitorService : IDisposable
    {
        /// <summary>
        /// 是否正在运行
        /// </summary>
        bool IsRunning { get; }

        /// <summary>
        /// 当前用户活动状态
        /// </summary>
        UserActivityState CurrentState { get; }

        /// <summary>
        /// 当前状态开始时间
        /// </summary>
        DateTime StateStartTime { get; }

        /// <summary>
        /// 当前状态持续时间
        /// </summary>
        TimeSpan CurrentStateDuration { get; }

        /// <summary>
        /// 空闲阈值（毫秒，默认5分钟）
        /// </summary>
        int IdleThresholdMilliseconds { get; set; }

        /// <summary>
        /// 用户活动状态变化事件
        /// </summary>
        event EventHandler<ActivityStateChangedEventArgs>? ActivityStateChanged;

        /// <summary>
        /// 输入计数更新事件（每60秒触发一次）
        /// </summary>
        event EventHandler<InputCounterEventArgs>? CounterUpdated;

        /// <summary>
        /// 启动监控
        /// </summary>
        void Start();

        /// <summary>
        /// 停止监控
        /// </summary>
        void Stop();

        /// <summary>
        /// 暂停监控（锁屏时使用）
        /// </summary>
        void Pause();

        /// <summary>
        /// 恢复监控
        /// </summary>
        void Resume();

        /// <summary>
        /// 获取当前计数器快照
        /// </summary>
        InputEventCounter GetCurrentCounter();

        /// <summary>
        /// 重置当前计数器
        /// </summary>
        void ResetCounter();

        /// <summary>
        /// 强制设置为锁屏状态
        /// </summary>
        void SetLockedState();

        /// <summary>
        /// 从锁屏状态恢复
        /// </summary>
        void SetUnlockedState();
    }
}
