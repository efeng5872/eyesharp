using System;

namespace eyesharp.Services
{
    /// <summary>
    /// 锁屏状态服务接口（单一锁屏状态来源）
    /// </summary>
    public interface ILockStateService : IDisposable
    {
        /// <summary>
        /// 当前是否锁屏
        /// </summary>
        bool IsLocked { get; }

        /// <summary>
        /// 锁屏状态变化事件
        /// </summary>
        event EventHandler<LockStateChangedEventArgs>? LockStateChanged;
    }

    /// <summary>
    /// 锁屏状态变化事件参数
    /// </summary>
    public class LockStateChangedEventArgs : EventArgs
    {
        public bool IsLocked { get; }

        public LockStateChangedEventArgs(bool isLocked)
        {
            IsLocked = isLocked;
        }
    }
}
