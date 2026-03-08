using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using eyesharp.Models;
using static eyesharp.Services.UsageStats.InputHookHelper;

namespace eyesharp.Services.UsageStats
{
    /// <summary>
    /// 输入监控服务实现
    /// </summary>
    public class InputMonitorService : IInputMonitorService
    {
        private readonly ILogService _logService;
        private readonly object _lock = new();

        // 钩子句柄
        private IntPtr _keyboardHookId = IntPtr.Zero;
        private IntPtr _mouseHookId = IntPtr.Zero;

        // 钩子回调委托（必须保持引用防止GC回收）
        private LowLevelKeyboardProc? _keyboardProc;
        private LowLevelMouseProc? _mouseProc;

        // 后台线程
        private Thread? _messagePumpThread;
        private CancellationTokenSource? _cancellationTokenSource;

        // 计数器
        private readonly InputEventCounter _counter = new();
        private DateTime _lastInputTime;

        // 状态
        private UserActivityState _currentState = UserActivityState.Unknown;
        private DateTime _stateStartTime;
        private bool _isPaused;

        // 定时保存
        private System.Timers.Timer? _saveTimer;

        /// <inheritdoc />
        public bool IsRunning { get; private set; }

        /// <inheritdoc />
        public UserActivityState CurrentState
        {
            get => _currentState;
            private set
            {
                if (_currentState != value)
                {
                    var oldState = _currentState;
                    var duration = DateTime.Now - _stateStartTime;

                    _currentState = value;
                    _stateStartTime = DateTime.Now;

                    _logService?.Info($"[InputMonitor] 状态变化: {oldState} -> {value}, 上一状态持续{duration.TotalMinutes:F1}分钟");

                    // 触发事件
                    ActivityStateChanged?.Invoke(this, new ActivityStateChangedEventArgs
                    {
                        OldState = oldState,
                        NewState = value,
                        ChangeTime = _stateStartTime,
                        PreviousStateDuration = duration,
                        CounterSnapshot = GetCurrentCounter()
                    });
                }
            }
        }

        /// <inheritdoc />
        public DateTime StateStartTime => _stateStartTime;

        /// <inheritdoc />
        public TimeSpan CurrentStateDuration => DateTime.Now - _stateStartTime;

        /// <inheritdoc />
        public int IdleThresholdMilliseconds { get; set; } = 5 * 60 * 1000; // 默认5分钟

        /// <inheritdoc />
        public event EventHandler<ActivityStateChangedEventArgs>? ActivityStateChanged;

        /// <inheritdoc />
        public event EventHandler<InputCounterEventArgs>? CounterUpdated;

        public InputMonitorService(ILogService logService)
        {
            _logService = logService ?? throw new ArgumentNullException(nameof(logService));
            _lastInputTime = DateTime.Now;
            _stateStartTime = DateTime.Now;
        }

        /// <inheritdoc />
        public void Start()
        {
            if (IsRunning)
            {
                _logService.Warn("[InputMonitor] 服务已在运行，忽略重复启动");
                return;
            }

            try
            {
                _logService.Info("[InputMonitor] 正在启动输入监控服务...");

                // 初始化钩子委托
                _keyboardProc = KeyboardHookCallback;
                _mouseProc = MouseHookCallback;

                // 安装钩子
                InstallHooks();

                // 启动后台线程（用于检测空闲状态）
                _cancellationTokenSource = new CancellationTokenSource();
                _messagePumpThread = new Thread(MessagePumpLoop)
                {
                    IsBackground = true,
                    Name = "InputMonitorThread"
                };
                _messagePumpThread.Start(_cancellationTokenSource.Token);

                // 启动定时保存（每60秒）
                _saveTimer = new System.Timers.Timer(60000);
                _saveTimer.Elapsed += (s, e) => OnCounterUpdated();
                _saveTimer.Start();

                IsRunning = true;
                CurrentState = UserActivityState.Active;

                _logService.Info("[InputMonitor] 输入监控服务启动成功");
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "[InputMonitor] 启动失败");
                throw;
            }
        }

        /// <inheritdoc />
        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            _logService.Info("[InputMonitor] 正在停止输入监控服务...");

            // 停止定时器
            _saveTimer?.Stop();
            _saveTimer?.Dispose();
            _saveTimer = null;

            // 触发最后一次计数更新
            OnCounterUpdated();

            // 取消后台线程
            _cancellationTokenSource?.Cancel();

            // 卸载钩子
            UninstallHooks();

            IsRunning = false;
            CurrentState = UserActivityState.Unknown;

            _logService.Info("[InputMonitor] 输入监控服务已停止");
        }

        /// <inheritdoc />
        public void Pause()
        {
            if (!IsRunning || _isPaused)
            {
                return;
            }

            _logService.Debug("[InputMonitor] 暂停监控");
            _isPaused = true;
            _saveTimer?.Stop();

            // 保存当前计数
            OnCounterUpdated();
        }

        /// <inheritdoc />
        public void Resume()
        {
            if (!IsRunning || !_isPaused)
            {
                return;
            }

            _logService.Debug("[InputMonitor] 恢复监控");
            _isPaused = false;
            _lastInputTime = DateTime.Now;
            _saveTimer?.Start();
        }

        /// <inheritdoc />
        public InputEventCounter GetCurrentCounter()
        {
            lock (_lock)
            {
                return new InputEventCounter
                {
                    KeyPressCount = _counter.KeyPressCount,
                    MouseMoveDistance = _counter.MouseMoveDistance,
                    MouseLeftClickCount = _counter.MouseLeftClickCount,
                    MouseRightClickCount = _counter.MouseRightClickCount,
                    MouseMiddleClickCount = _counter.MouseMiddleClickCount,
                    MouseWheelScrollCount = _counter.MouseWheelScrollCount
                };
            }
        }

        /// <inheritdoc />
        public void ResetCounter()
        {
            lock (_lock)
            {
                _counter.Reset();
                _logService.Debug("[InputMonitor] 计数器已重置");
            }
        }

        /// <inheritdoc />
        public void SetLockedState()
        {
            if (!IsRunning)
            {
                return;
            }

            _logService.Debug("[InputMonitor] 设置为锁屏状态");
            Pause();
            CurrentState = UserActivityState.Locked;
        }

        /// <inheritdoc />
        public void SetUnlockedState()
        {
            if (!IsRunning)
            {
                return;
            }

            _logService.Debug("[InputMonitor] 从锁屏恢复");
            Resume();
            CurrentState = UserActivityState.Active;
            _lastInputTime = DateTime.Now;
        }

        /// <summary>
        /// 安装钩子
        /// </summary>
        private void InstallHooks()
        {
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            using var module = process.MainModule;

            if (module == null)
            {
                throw new InvalidOperationException("无法获取当前模块");
            }

            var hModule = GetModuleHandle(module.ModuleName);

            // 安装键盘钩子
            _keyboardHookId = SetWindowsHookEx(WH_KEYBOARD_LL, _keyboardProc!, hModule, 0);
            if (_keyboardHookId == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                throw new Win32Exception(errorCode, "键盘钩子安装失败");
            }
            _logService.Debug("[InputMonitor] 键盘钩子安装成功");

            // 安装鼠标钩子
            _mouseHookId = SetWindowsHookEx(WH_MOUSE_LL, _mouseProc!, hModule, 0);
            if (_mouseHookId == IntPtr.Zero)
            {
                int errorCode = Marshal.GetLastWin32Error();
                UnhookWindowsHookEx(_keyboardHookId);
                _keyboardHookId = IntPtr.Zero;
                throw new Win32Exception(errorCode, "鼠标钩子安装失败");
            }
            _logService.Debug("[InputMonitor] 鼠标钩子安装成功");
        }

        /// <summary>
        /// 卸载钩子
        /// </summary>
        private void UninstallHooks()
        {
            if (_keyboardHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_keyboardHookId);
                _keyboardHookId = IntPtr.Zero;
                _logService.Debug("[InputMonitor] 键盘钩子已卸载");
            }

            if (_mouseHookId != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookId);
                _mouseHookId = IntPtr.Zero;
                _logService.Debug("[InputMonitor] 鼠标钩子已卸载");
            }
        }

        /// <summary>
        /// 键盘钩子回调
        /// </summary>
        private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && !_isPaused)
            {
                // 只统计按键按下（不统计释放）
                if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                {
                    lock (_lock)
                    {
                        _counter.KeyPressCount++;
                    }
                    OnInputDetected();
                }
            }

            return CallNextHookEx(_keyboardHookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// 鼠标钩子回调
        /// </summary>
        private IntPtr MouseHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && !_isPaused)
                {
                    var message = (int)wParam;

                    switch (message)
                    {
                        case WM_MOUSEMOVE:
                            HandleMouseMove(lParam);
                            break;

                        case WM_LBUTTONDOWN:
                            lock (_lock)
                            {
                                _counter.MouseLeftClickCount++;
                            }
                            OnInputDetected();
                            break;

                        case WM_RBUTTONDOWN:
                            lock (_lock)
                            {
                                _counter.MouseRightClickCount++;
                            }
                            OnInputDetected();
                            break;

                        case WM_MBUTTONDOWN:
                            lock (_lock)
                            {
                                _counter.MouseMiddleClickCount++;
                            }
                            OnInputDetected();
                            break;

                        case WM_MOUSEWHEEL:
                            lock (_lock)
                            {
                                _counter.MouseWheelScrollCount++;
                            }
                            OnInputDetected();
                            break;
                    }
                }

            return CallNextHookEx(_mouseHookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// 处理鼠标移动
        /// </summary>
        private void HandleMouseMove(IntPtr lParam)
        {
            var point = GetMousePoint(lParam);

            lock (_lock)
            {
                if (_counter.HasLastPosition)
                {
                    // 计算欧几里得距离
                    var dx = point.X - _counter.LastMousePosition.X;
                    var dy = point.Y - _counter.LastMousePosition.Y;
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    _counter.MouseMoveDistance += (long)distance;
                }

                _counter.LastMousePosition = new System.Drawing.Point(point.X, point.Y);
                _counter.HasLastPosition = true;
            }

            OnInputDetected();
        }

        /// <summary>
        /// 检测到输入时的处理
        /// </summary>
        private void OnInputDetected()
        {
            _lastInputTime = DateTime.Now;

            // 如果当前是空闲状态，切换回活跃
            if (CurrentState == UserActivityState.Idle)
            {
                CurrentState = UserActivityState.Active;
            }
        }

        /// <summary>
        /// 后台消息循环（检测空闲状态）
        /// </summary>
        private void MessagePumpLoop(object? state)
        {
            var token = (CancellationToken)state!;
            _logService.Debug("[InputMonitor] 后台检测线程启动");

            try
            {
                while (!token.IsCancellationRequested)
                {
                    // 每1秒检查一次空闲状态
                    Thread.Sleep(1000);

                    if (_isPaused)
                    {
                        continue;
                    }

                    // 检查是否超过空闲阈值
                    var idleTime = DateTime.Now - _lastInputTime;
                    if (CurrentState == UserActivityState.Active &&
                        idleTime.TotalMilliseconds >= IdleThresholdMilliseconds)
                    {
                        CurrentState = UserActivityState.Idle;
                        _logService.Debug($"[InputMonitor] 进入空闲状态，已空闲{idleTime.TotalMinutes:F1}分钟");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常取消，忽略
            }
            catch (Exception ex)
            {
                _logService.Error(ex, "[InputMonitor] 后台检测线程异常");
            }

            _logService.Debug("[InputMonitor] 后台检测线程结束");
        }

        /// <summary>
        /// 触发计数更新事件
        /// </summary>
        private void OnCounterUpdated()
        {
            if (!IsRunning)
            {
                return;
            }

            var args = new InputCounterEventArgs
            {
                RecordTime = DateTime.Now,
                Counter = GetCurrentCounter(),
                CurrentState = CurrentState
            };

            CounterUpdated?.Invoke(this, args);
            _logService.Debug($"[InputMonitor] 计数更新: 按键{args.Counter.KeyPressCount}次, " +
                $"移动{args.Counter.MouseMoveDistance}像素, " +
                $"左键{args.Counter.MouseLeftClickCount}次");
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Stop();
            _cancellationTokenSource?.Dispose();
            _saveTimer?.Dispose();
        }
    }
}
