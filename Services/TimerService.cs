using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using eyesharp.Models;
using eyesharp.Helpers;

namespace eyesharp.Services
{
    /// <summary>
    /// 定时器服务实现
    /// </summary>
    public class TimerService : ITimerService, IDisposable
    {
        private readonly object _lock = new object();
        private System.Threading.Timer? _timer;
        private TimerState _state = TimerState.Running;
        private int _mainCountdownRemaining = 0;
        private int _restCountdownRemaining = 0;
        private bool _disposed = false;
        private readonly ILogService? _logService;

        // 预提醒配置
        private bool _isPreReminderEnabled = true;
        private int[] _preReminderIntervals = { 30, 10 };
        private HashSet<int> _reminderTriggered = new HashSet<int>();

        // 锁屏处理行为
        private string _lockScreenBehavior = "normal";

        // 记录倒计时期间是否发生过锁屏
        private bool _wasLockedDuringCountdown = false;

        // 记录是否有待处理的跳过休息（策略3：等解锁后再开始新倒计时）
        private bool _pendingSkipRest = false;

        // Windows API 检测锁屏状态
        [DllImport("user32.dll")]
        private static extern IntPtr OpenInputDesktop(uint dwFlags, bool fInherit, uint dwDesiredAccess);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseDesktop(IntPtr hDesktop);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetUserObjectInformation(IntPtr hObj, int nIndex, IntPtr pvInfo, uint nLength, out uint lpnLengthNeeded);

        private const int UOI_NAME = 2;

        // WTS API 用于检测会话锁定状态
        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern bool WTSQuerySessionInformation(IntPtr hServer, int sessionId, WTS_INFO_CLASS wtsInfoClass, out IntPtr ppBuffer, out uint pBytesReturned);

        [DllImport("wtsapi32.dll", SetLastError = true)]
        private static extern void WTSFreeMemory(IntPtr pMemory);

        [DllImport("kernel32.dll")]
        private static extern int WTSGetActiveConsoleSessionId();

        private enum WTS_INFO_CLASS
        {
            WTSInitialProgram = 0,
            WTSApplicationName = 1,
            WTSWorkingDirectory = 2,
            WTSOEMId = 3,
            WTSSessionId = 4,
            WTSUserName = 5,
            WTSWinStationName = 6,
            WTSDomainName = 7,
            WTSConnectState = 8,
            WTSClientBuildNumber = 9,
            WTSClientName = 10,
            WTSClientDirectory = 11,
            WTSClientProductId = 12,
            WTSClientHardwareId = 13,
            WTSClientAddress = 14,
            WTSClientDisplay = 15,
            WTSClientProtocolType = 16,
            WTSIdleTime = 17,
            WTSLogonTime = 18,
            WTSIncomingBytes = 19,
            WTSOutgoingBytes = 20,
            WTSIncomingFrames = 21,
            WTSOutgoingFrames = 22,
            WTSClientInfo = 23,
            WTSSessionInfo = 24,
            WTSSessionInfoEx = 25,
            WTSConfigInfo = 26,
            WTSValidationInfo = 27,
            WTSSessionAddressV4 = 28,
            WTSIsRemoteSession = 29
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

        /// <summary>
        /// 是否启用预提醒
        /// </summary>
        public bool IsPreReminderEnabled
        {
            get
            {
                lock (_lock)
                {
                    return _isPreReminderEnabled;
                }
            }
            set
            {
                lock (_lock)
                {
                    _isPreReminderEnabled = value;
                }
            }
        }

        /// <summary>
        /// 预提醒时间间隔（秒）
        /// </summary>
        public int[] PreReminderIntervals
        {
            get
            {
                lock (_lock)
                {
                    return _preReminderIntervals;
                }
            }
            set
            {
                lock (_lock)
                {
                    _preReminderIntervals = value ?? new[] { 30, 10 };
                    _reminderTriggered.Clear();
                }
            }
        }

        /// <summary>
        /// 设置锁屏处理行为
        /// </summary>
        public void SetLockScreenBehavior(string behavior)
        {
            lock (_lock)
            {
                _lockScreenBehavior = behavior ?? "normal";
            }
        }

        /// <summary>
        /// 通知 TimerService 系统已锁屏
        /// 由 MainViewModel 在 SessionSwitch 事件中调用
        /// </summary>
        public void NotifyWorkstationLocked()
        {
            lock (_lock)
            {
                _wasLockedDuringCountdown = true;
                _logService?.Info("[TimerService] 记录到锁屏事件，倒计时期间曾锁屏");
            }
        }

        /// <summary>
        /// 通知 TimerService 系统已解锁
        /// </summary>
        public void NotifyWorkstationUnlocked()
        {
            // 解锁时不重置标志，保留到倒计时结束
            _logService?.Debug("[TimerService] 记录到解锁事件");
        }

        /// <summary>
        /// 检查是否有待处理的跳过休息（策略3）
        /// </summary>
        public bool HasPendingSkipRest()
        {
            lock (_lock)
            {
                return _pendingSkipRest;
            }
        }

        /// <summary>
        /// 清除待处理的跳过休息标志
        /// </summary>
        public void ClearPendingSkipRest()
        {
            lock (_lock)
            {
                _pendingSkipRest = false;
            }
        }

        /// <summary>
        /// 获取当前锁屏处理行为
        /// </summary>
        public string GetLockScreenBehavior()
        {
            lock (_lock)
            {
                return _lockScreenBehavior;
            }
        }

        // Windows API 常量
        private const uint DESKTOP_READOBJECTS = 0x0001;

        /// <summary>
        /// 检测倒计时期间是否发生过锁屏（用于策略2和3）
        /// 由 SessionSwitch 事件驱动，而非实时检测
        /// </summary>
        private bool WasLockedDuringCountdown()
        {
            lock (_lock)
            {
                _logService?.Info($"[TimerService] 检测锁屏状态: _wasLockedDuringCountdown={_wasLockedDuringCountdown}");
                return _wasLockedDuringCountdown;
            }
        }

        /// <summary>
        /// 检测当前是否正在锁屏（用于策略1的延迟显示）
        /// 使用 Windows API 实时检测
        /// </summary>
        private bool IsCurrentlyLocked()
        {
            try
            {
                // 简单检测：尝试获取输入桌面
                IntPtr hDesktop = OpenInputDesktop(0, false, 0);
                if (hDesktop == IntPtr.Zero)
                {
                    return true; // 锁屏状态
                }
                CloseDesktop(hDesktop);
                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 重置锁屏状态标志
        /// 在主倒计时开始时调用
        /// </summary>
        private void ResetLockScreenFlag()
        {
            lock (_lock)
            {
                _wasLockedDuringCountdown = false;
                _pendingSkipRest = false;
                _logService?.Debug("[TimerService] 重置锁屏标志和待处理跳过标志");
            }
        }

        public TimerState State
        {
            get
            {
                lock (_lock)
                {
                    return _state;
                }
            }
            private set
            {
                TimerStateChangedEventArgs? eventArgs = null;
                lock (_lock)
                {
                    if (_state != value)
                    {
                        var oldState = _state;
                        _state = value;
                        eventArgs = new TimerStateChangedEventArgs(oldState, value);
                    }
                }
                // 在锁外触发事件，避免死锁
                if (eventArgs != null)
                {
                    StateChanged?.Invoke(this, eventArgs);
                }
            }
        }

        public int MainCountdownRemaining
        {
            get
            {
                lock (_lock)
                {
                    return _mainCountdownRemaining;
                }
            }
            private set
            {
                lock (_lock)
                {
                    _mainCountdownRemaining = value;
                }
            }
        }

        public int RestCountdownRemaining
        {
            get
            {
                lock (_lock)
                {
                    return _restCountdownRemaining;
                }
            }
            private set
            {
                lock (_lock)
                {
                    _restCountdownRemaining = value;
                }
            }
        }

        public event EventHandler<TimerStateChangedEventArgs>? StateChanged;
        public event EventHandler<TimerTickEventArgs>? MainCountdownTick;
        public event EventHandler<TimerTickEventArgs>? RestCountdownTick;
        public event EventHandler? MainCountdownElapsed;
        public event EventHandler? RestCountdownElapsed;
        public event EventHandler<PreReminderEventArgs>? PreReminder;

        public TimerService(ILogService? logService = null)
        {
            _logService = logService;
            // 初始化定时器，每秒触发一次
            _timer = new System.Threading.Timer(OnTimerTick, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// 开始主倒计时
        /// </summary>
        public void StartMainCountdown(TimeSpan duration)
        {
            lock (_lock)
            {
                _mainCountdownRemaining = (int)duration.TotalSeconds;
                State = TimerState.Running;
                _reminderTriggered.Clear(); // 重置预提醒状态
                ResetLockScreenFlag(); // 重置锁屏标志
            }

            // 在锁外启动定时器，避免死锁
            _timer?.Change(0, 1000);

            // 使用 Task.Run 延迟触发 Tick，避免死锁
            Task.Run(() =>
            {
                Task.Delay(50).Wait();  // 等待 50ms
                OnMainCountdownTick();
            });
        }

        /// <summary>
        /// 暂停倒计时
        /// </summary>
        public void Pause()
        {
            lock (_lock)
            {
                if (_state == TimerState.Running)
                {
                    State = TimerState.Paused;
                    // 定时器继续运行，但不会更新倒计时
                }
            }
        }

        /// <summary>
        /// 恢复倒计时
        /// </summary>
        public void Resume()
        {
            lock (_lock)
            {
                if (_state == TimerState.Paused)
                {
                    State = TimerState.Running;
                }
            }
        }

        /// <summary>
        /// 停止倒计时
        /// </summary>
        public void Stop()
        {
            lock (_lock)
            {
                _timer?.Change(Timeout.Infinite, Timeout.Infinite);
                _mainCountdownRemaining = 0;
                _restCountdownRemaining = 0;
                State = TimerState.Running;
            }
        }

        /// <summary>
        /// 开始休息倒计时
        /// </summary>
        public void StartRestCountdown(TimeSpan duration)
        {
            lock (_lock)
            {
                _restCountdownRemaining = (int)duration.TotalSeconds;
                State = TimerState.Resting;
            }

            // 在锁外启动定时器，避免死锁
            _timer?.Change(0, 1000);

            // 使用 Task.Run 延迟触发 Tick，避免死锁
            Task.Run(() =>
            {
                Task.Delay(50).Wait();  // 等待 50ms
                OnRestCountdownTick();
            });
        }

        /// <summary>
        /// 重置定时器
        /// </summary>
        public void Reset()
        {
            Stop();
            _mainCountdownRemaining = 0;
            _restCountdownRemaining = 0;
        }

        /// <summary>
        /// 定时器Tick回调
        /// </summary>
        private void OnTimerTick(object? state)
        {
            bool mainElapsed = false;
            bool restElapsed = false;

            lock (_lock)
            {
                if (_disposed)
                    return;

                switch (_state)
                {
                    case TimerState.Running:
                        if (_mainCountdownRemaining > 0)
                        {
                            _mainCountdownRemaining--;
                            OnMainCountdownTick();

                            if (_mainCountdownRemaining == 0)
                            {
                                // 标记主倒计时结束，稍后触发事件
                                mainElapsed = true;
                                // 停止定时器，等待开始休息倒计时
                                _timer?.Change(Timeout.Infinite, Timeout.Infinite);
                            }
                        }
                        break;

                    case TimerState.Resting:
                        if (_restCountdownRemaining > 0)
                        {
                            _restCountdownRemaining--;
                            OnRestCountdownTick();

                            if (_restCountdownRemaining == 0)
                            {
                                // 标记休息倒计时结束，稍后触发事件
                                restElapsed = true;
                                _timer?.Change(Timeout.Infinite, Timeout.Infinite);
                            }
                        }
                        break;

                    case TimerState.Paused:
                        // 暂停状态，不处理倒计时
                        break;
                }
            }

            // 在 lock 外部触发事件，避免死锁
            if (mainElapsed)
            {
                // 检测锁屏状态并根据策略处理
                HandleMainCountdownElapsedWithLockScreenCheck();
            }

            if (restElapsed)
            {
                RestCountdownElapsed?.Invoke(this, EventArgs.Empty);
            }

            // 触发预提醒事件
            CheckAndTriggerPreReminder();
        }

        /// <summary>
        /// 处理主倒计时结束，根据锁屏状态和策略决定是否触发事件
        /// </summary>
        private void HandleMainCountdownElapsedWithLockScreenCheck()
        {
            _logService?.Info("[TimerService] HandleMainCountdownElapsedWithLockScreenCheck 开始执行");

            string behavior = GetLockScreenBehavior();
            bool wasLocked = WasLockedDuringCountdown();
            bool isCurrentlyLocked = IsCurrentlyLocked();

            _logService?.Info($"[TimerService] 检测结果: wasLocked={wasLocked}, isCurrentlyLocked={isCurrentlyLocked}, behavior={behavior}");

            switch (behavior)
            {
                case "skip":
                    // 策略3：如果倒计时期间锁屏过，设置标志等待解锁后再开始新倒计时
                    if (wasLocked)
                    {
                        _logService?.Info("[TimerService] 策略3(skip)：倒计时期间曾锁屏，设置标志等待解锁后开始新倒计时");
                        lock (_lock)
                        {
                            _pendingSkipRest = true;
                        }
                        // 停止定时器，不触发任何事件，等待解锁
                        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
                    }
                    else
                    {
                        _logService?.Info("[TimerService] 策略3(skip)：倒计时期间未锁屏，正常显示休息窗口");
                        MainCountdownElapsed?.Invoke(this, EventArgs.Empty);
                    }
                    break;

                case "pause":
                    // 策略2：如果当前正在锁屏，不触发事件（倒计时已在锁屏时暂停）
                    // 如果未锁屏，正常显示休息窗口
                    if (isCurrentlyLocked)
                    {
                        _logService?.Info("[TimerService] 策略2(pause)：当前处于锁屏状态，不触发事件");
                    }
                    else
                    {
                        _logService?.Info("[TimerService] 策略2(pause)：未锁屏，正常显示休息窗口");
                        MainCountdownElapsed?.Invoke(this, EventArgs.Empty);
                    }
                    break;

                case "normal":
                default:
                    // 策略1：无论是否锁屏都触发事件，主程序处理延迟显示
                    _logService?.Info("[TimerService] 策略1(normal)：触发 MainCountdownElapsed");
                    MainCountdownElapsed?.Invoke(this, EventArgs.Empty);
                    break;
            }

            _logService?.Info("[TimerService] HandleMainCountdownElapsedWithLockScreenCheck 执行完成");
        }

        /// <summary>
        /// 检查并触发预提醒
        /// </summary>
        private void CheckAndTriggerPreReminder()
        {
            bool isEnabled;
            int[] intervals;
            int remaining;

            lock (_lock)
            {
                isEnabled = _isPreReminderEnabled;
                intervals = _preReminderIntervals;
                remaining = _mainCountdownRemaining;
            }

            if (!isEnabled || _state != TimerState.Running || remaining <= 0)
                return;

            foreach (var interval in intervals)
            {
                if (remaining == interval && !_reminderTriggered.Contains(interval))
                {
                    _reminderTriggered.Add(interval);
                    string message = interval <= 10
                        ? $"{interval}秒后开始休息，请保存工作"
                        : $"{interval}秒后开始休息";
                    PreReminder?.Invoke(this, new PreReminderEventArgs(interval, message));
                    break;
                }
            }
        }

        /// <summary>
        /// 触发主倒计时Tick事件
        /// </summary>
        private void OnMainCountdownTick()
        {
            var remaining = MainCountdownRemaining;
            var formatted = DateTimeHelper.FormatCountdown(remaining);
            MainCountdownTick?.Invoke(this, new TimerTickEventArgs(remaining, formatted));
        }

        /// <summary>
        /// 触发休息倒计时Tick事件
        /// </summary>
        private void OnRestCountdownTick()
        {
            var remaining = RestCountdownRemaining;
            var formatted = DateTimeHelper.FormatCountdown(remaining);
            RestCountdownTick?.Invoke(this, new TimerTickEventArgs(remaining, formatted));
        }

        /// <summary>
        /// 重置并开始主倒计时（用于策略3跳过休息）
        /// </summary>
        private void ResetAndStartMainCountdown()
        {
            lock (_lock)
            {
                // 停止定时器，防止休息倒计时结束再次触发事件
                _timer?.Change(Timeout.Infinite, Timeout.Infinite);

                // 重置预提醒状态
                _reminderTriggered.Clear();
                // 保持 Running 状态，重置倒计时
                _mainCountdownRemaining = 0; // 会在 StartMainCountdown 中重新设置
                _restCountdownRemaining = 0;
            }

            _logService?.Info("[TimerService] 策略3跳过休息，停止休息倒计时，触发 SkipRest 事件");

            // 触发 SkipRest 事件通知主程序开始新的主倒计时
            SkipRest?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// 跳过休息事件（策略3触发）
        /// </summary>
        public event EventHandler? SkipRest;

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposed)
                    return;

                _disposed = true;
                _timer?.Dispose();
                _timer = null;
            }
        }
    }
}
