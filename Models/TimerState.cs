namespace eyesharp.Models
{
    /// <summary>
    /// 定时器状态枚举
    /// </summary>
    public enum TimerState
    {
        /// <summary>
        /// 运行中 - 正在倒计时，等待休息时间到达
        /// </summary>
        Running,

        /// <summary>
        /// 已暂停 - 用户手动暂停提醒
        /// </summary>
        Paused,

        /// <summary>
        /// 休息中 - 正在显示休息窗口
        /// </summary>
        Resting
    }
}
