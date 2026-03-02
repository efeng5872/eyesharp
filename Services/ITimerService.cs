using System;
using eyesharp.Models;

namespace eyesharp.Services
{
    /// <summary>
    /// 定时器服务接口
    /// </summary>
    public interface ITimerService
    {
        /// <summary>
        /// 当前状态
        /// </summary>
        TimerState State { get; }

        /// <summary>
        /// 主倒计时剩余秒数
        /// </summary>
        int MainCountdownRemaining { get; }

        /// <summary>
        /// 休息倒计时剩余秒数
        /// </summary>
        int RestCountdownRemaining { get; }

        /// <summary>
        /// 状态变化事件
        /// </summary>
        event EventHandler<TimerStateChangedEventArgs>? StateChanged;

        /// <summary>
        /// 主倒计时更新事件（每秒触发）
        /// </summary>
        event EventHandler<TimerTickEventArgs>? MainCountdownTick;

        /// <summary>
        /// 休息倒计时更新事件（每秒触发）
        /// </summary>
        event EventHandler<TimerTickEventArgs>? RestCountdownTick;

        /// <summary>
        /// 主倒计时结束事件（触发休息窗口）
        /// </summary>
        event EventHandler? MainCountdownElapsed;

        /// <summary>
        /// 休息倒计时结束事件（关闭休息窗口）
        /// </summary>
        event EventHandler? RestCountdownElapsed;

        /// <summary>
        /// 休息前预提醒事件
        /// </summary>
        event EventHandler<PreReminderEventArgs>? PreReminder;

        /// <summary>
        /// 开始主倒计时
        /// </summary>
        void StartMainCountdown(TimeSpan duration);

        /// <summary>
        /// 暂停倒计时
        /// </summary>
        void Pause();

        /// <summary>
        /// 恢复倒计时
        /// </summary>
        void Resume();

        /// <summary>
        /// 停止倒计时
        /// </summary>
        void Stop();

        /// <summary>
        /// 开始休息倒计时
        /// </summary>
        void StartRestCountdown(TimeSpan duration);

        /// <summary>
        /// 重置定时器
        /// </summary>
        void Reset();
    }

    /// <summary>
    /// 定时器状态变化事件参数
    /// </summary>
    public class TimerStateChangedEventArgs : EventArgs
    {
        public TimerState OldState { get; set; }
        public TimerState NewState { get; set; }

        public TimerStateChangedEventArgs(TimerState oldState, TimerState newState)
        {
            OldState = oldState;
            NewState = newState;
        }
    }

    /// <summary>
    /// 定时器Tick事件参数
    /// </summary>
    public class TimerTickEventArgs : EventArgs
    {
        public int RemainingSeconds { get; set; }
        public string FormattedTime { get; set; }

        public TimerTickEventArgs(int remainingSeconds, string formattedTime)
        {
            RemainingSeconds = remainingSeconds;
            FormattedTime = formattedTime;
        }
    }

    /// <summary>
    /// 休息前预提醒事件参数
    /// </summary>
    public class PreReminderEventArgs : EventArgs
    {
        /// <summary>
        /// 距离休息开始的秒数
        /// </summary>
        public int SecondsUntilRest { get; set; }

        /// <summary>
        /// 提醒消息
        /// </summary>
        public string Message { get; set; }

        public PreReminderEventArgs(int secondsUntilRest, string message)
        {
            SecondsUntilRest = secondsUntilRest;
            Message = message;
        }
    }
}
