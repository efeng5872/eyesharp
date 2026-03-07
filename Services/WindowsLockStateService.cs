using System;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace eyesharp.Services
{
    /// <summary>
    /// Windows 锁屏状态服务实现（WTS 优先 + OpenInputDesktop 兜底）
    /// </summary>
    public class WindowsLockStateService : ILockStateService
    {
        private readonly object _lock = new object();
        private readonly ILogService? _logService;
        private bool _isLocked;
        private bool _disposed;

        public event EventHandler<LockStateChangedEventArgs>? LockStateChanged;

        public bool IsLocked
        {
            get
            {
                lock (_lock)
                {
                    return _isLocked;
                }
            }
            private set
            {
                bool changed;
                lock (_lock)
                {
                    if (_isLocked == value)
                    {
                        return;
                    }

                    _isLocked = value;
                    changed = true;
                }

                if (changed)
                {
                    _logService?.Info($"[LockStateService] 锁屏状态变化: IsLocked={value}");
                    LockStateChanged?.Invoke(this, new LockStateChangedEventArgs(value));
                }
            }
        }

        public WindowsLockStateService(ILogService? logService = null)
        {
            _logService = logService;
            _isLocked = ProbeCurrentLockState();
            _logService?.Info($"[LockStateService] 初始化完成，初始锁屏状态: IsLocked={_isLocked}");
            SystemEvents.SessionSwitch += OnSessionSwitch;
        }

        private void OnSessionSwitch(object? sender, SessionSwitchEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                switch (e.Reason)
                {
                    case SessionSwitchReason.SessionLock:
                        IsLocked = true;
                        break;

                    case SessionSwitchReason.SessionUnlock:
                        IsLocked = false;
                        break;
                }
            }
            catch (Exception ex)
            {
                _logService?.Error(ex, "[LockStateService] 处理 SessionSwitch 事件失败");
            }
        }

        /// <summary>
        /// 主动探测当前锁屏状态（WTS 优先 + OpenInputDesktop 兜底）
        /// </summary>
        private bool ProbeCurrentLockState()
        {
            try
            {
                int sessionId = System.Diagnostics.Process.GetCurrentProcess().SessionId;
                if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, WTS_INFO_CLASS.WTSConnectState, out IntPtr pBuffer, out uint _))
                {
                    try
                    {
                        int state = Marshal.ReadInt32(pBuffer);
                        return state != (int)WTS_CONNECTSTATE_CLASS.WTSActive;
                    }
                    finally
                    {
                        WTSFreeMemory(pBuffer);
                    }
                }
            }
            catch (Exception ex)
            {
                _logService?.Warn($"[LockStateService] WTS 探测失败，尝试兜底探测: {ex.Message}");
            }

            try
            {
                IntPtr hDesktop = OpenInputDesktop(0, false, DESKTOP_READOBJECTS);
                if (hDesktop == IntPtr.Zero)
                {
                    return true;
                }

                CloseDesktop(hDesktop);
                return false;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            _logService?.Info("[LockStateService] 资源已释放");
        }

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQuerySessionInformation(IntPtr hServer, int sessionId, WTS_INFO_CLASS wtsInfoClass, out IntPtr ppBuffer, out uint pBytesReturned);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        [DllImport("user32.dll")]
        private static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseDesktop(IntPtr hDesktop);

        private const uint DESKTOP_READOBJECTS = 0x0001;

        private enum WTS_INFO_CLASS
        {
            WTSConnectState = 8
        }

        private enum WTS_CONNECTSTATE_CLASS
        {
            WTSActive,
            WTSConnected,
            WTSConnectQuery,
            WTSShadow,
            WTSDisconnected,
            WTSIdle,
            WTSListen,
            WTSReset,
            WTSDown,
            WTSInit
        }
    }
}
