using System;
using System.Collections.Generic;
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
        private LockScreenBehaviorType _lockScreenBehavior = LockScreenBehaviorType.Pause;

        private readonly ILockStateService _lockStateService;

        // 策略3：到点且锁屏时，等待解锁后开始新主倒计时
        private bool _isWaitingForUnlock = false;

        // 策略2：因锁屏触发的暂停标志（用于解锁自动恢复）
        private bool _pausedByLockStrategy = false;

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
        public void SetLockScreenBehavior(LockScreenBehaviorType behavior)
        {
            lock (_lock)
            {
                _lockScreenBehavior = behavior;
            }
        }

        /// <summary>
        /// 获取当前锁屏处理行为
        /// </summary>
        public LockScreenBehaviorType GetLockScreenBehavior()
        {
            lock (_lock)
            {
                return _lockScreenBehavior;
            }
        }

        /// <summary>
        /// 是否处于“等待解锁后启动新主倒计时”状态（策略3）
        /// </summary>
        public bool IsWaitingForUnlock
        {
            get
            {
                lock (_lock)
                {
                    return _isWaitingForUnlock;
                }
            }
        }

        private void ResetWaitForUnlockFlag()
        {
            bool changed = false;
            lock (_lock)
            {
                if (_isWaitingForUnlock)
                {
                    _isWaitingForUnlock = false;
                    changed = true;
                }
            }

            if (changed)
            {
                WaitForUnlockChanged?.Invoke(this, new WaitForUnlockChangedEventArgs(false));
            }
        }

        private void SetWaitForUnlockFlag(bool value)
        {
            bool changed = false;
            lock (_lock)
            {
                if (_isWaitingForUnlock != value)
                {
                    _isWaitingForUnlock = value;
                    changed = true;
                }
            }

            if (changed)
            {
                WaitForUnlockChanged?.Invoke(this, new WaitForUnlockChangedEventArgs(value));
            }
        }

        private void OnLockStateChanged(object? sender, LockStateChangedEventArgs e)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                HandleLockStateChanged(e.IsLocked);
            }
            catch (Exception ex)
            {
                _logService?.Error(ex, "[TimerService] 处理锁屏状态变化失败");
            }
        }

        private void HandleLockStateChanged(bool isLocked)
        {
            var behavior = GetLockScreenBehavior();
            var currentState = State;

            if (isLocked)
            {
                _logService?.Info($"[TimerService] 检测到锁屏，当前策略={behavior}, 当前状态={currentState}");

                if (behavior == LockScreenBehaviorType.Pause && currentState == TimerState.Running)
                {
                    lock (_lock)
                    {
                        _pausedByLockStrategy = true;
                    }
                    Pause();
                }

                return;
            }

            _logService?.Info($"[TimerService] 检测到解锁，当前策略={behavior}, 当前状态={currentState}");

            if (behavior == LockScreenBehaviorType.Pause)
            {
                bool shouldResume = false;
                lock (_lock)
                {
                    if (_pausedByLockStrategy)
                    {
                        _pausedByLockStrategy = false;
                        shouldResume = true;
                    }
                }

                if (shouldResume)
                {
                    Resume();
                }
            }

            if (behavior == LockScreenBehaviorType.Skip && IsWaitingForUnlock)
            {
                _logService?.Info("[TimerService] 策略3：解锁后启动新主倒计时");
                SetWaitForUnlockFlag(false);
                ResetAndStartMainCountdown();
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
        public event EventHandler<WaitForUnlockChangedEventArgs>? WaitForUnlockChanged;

        public TimerService(ILogService? logService = null, ILockStateService? lockStateService = null)
        {
            _logService = logService;
            _lockStateService = lockStateService ?? new WindowsLockStateService(logService);
            _lockStateService.LockStateChanged += OnLockStateChanged;
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
                _reminderTriggered.Clear(); // 重置预提醒状态
                _pausedByLockStrategy = false;
            }

            ResetWaitForUnlockFlag();

            // 在锁外设置状态，避免死锁
            // 必须在锁外，因为 State setter 触发的事件会使用 Dispatcher.Invoke 更新UI
            State = TimerState.Running;

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
            bool shouldPause = false;
            lock (_lock)
            {
                if (_state == TimerState.Running)
                {
                    shouldPause = true;
                    // 定时器继续运行，但不会更新倒计时
                }
            }
            // 在锁外设置状态，避免死锁
            if (shouldPause)
            {
                State = TimerState.Paused;
            }
        }

        /// <summary>
        /// 恢复倒计时
        /// </summary>
        public void Resume()
        {
            bool shouldResume = false;
            lock (_lock)
            {
                if (_state == TimerState.Paused)
                {
                    shouldResume = true;
                }
            }
            // 在锁外设置状态，避免死锁
            if (shouldResume)
            {
                State = TimerState.Running;
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
            }
            // 在锁外设置状态，避免死锁
            State = TimerState.Running;
        }

        /// <summary>
        /// 开始休息倒计时
        /// </summary>
        public void StartRestCountdown(TimeSpan duration)
        {
            lock (_lock)
            {
                _restCountdownRemaining = (int)duration.TotalSeconds;
            }

            // 在锁外设置状态，避免死锁
            State = TimerState.Resting;

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

            var behavior = GetLockScreenBehavior();
            bool isCurrentlyLocked = _lockStateService.IsLocked;

            _logService?.Info($"[TimerService] 检测结果: isCurrentlyLocked={isCurrentlyLocked}, behavior={behavior}");

            switch (behavior)
            {
                case LockScreenBehaviorType.Skip:
                    // 策略3：仅在“到点时正在锁屏”时跳过休息
                    if (isCurrentlyLocked)
                    {
                        _logService?.Info("[TimerService] 策略3(skip)：到点时锁屏，进入等待解锁状态");
                        SetWaitForUnlockFlag(true);
                        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
                    }
                    else
                    {
                        _logService?.Info("[TimerService] 策略3(skip)：到点时未锁屏，正常显示休息窗口");
                        MainCountdownElapsed?.Invoke(this, EventArgs.Empty);
                    }
                    break;

                case LockScreenBehaviorType.Pause:
                case LockScreenBehaviorType.Normal:
                default:
                    // normal/pause：到点时统一进入休息流程（pause 已在锁屏时暂停主倒计时）
                    _logService?.Info($"[TimerService] 策略{behavior}：触发 MainCountdownElapsed");
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

            _lockStateService.LockStateChanged -= OnLockStateChanged;
            _lockStateService.Dispose();
        }
    }
}
