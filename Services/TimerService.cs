using System;
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
                lock (_lock)
                {
                    if (_state != value)
                    {
                        var oldState = _state;
                        _state = value;
                        StateChanged?.Invoke(this, new TimerStateChangedEventArgs(oldState, value));
                    }
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

        public TimerService()
        {
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
                MainCountdownElapsed?.Invoke(this, EventArgs.Empty);
            }

            if (restElapsed)
            {
                RestCountdownElapsed?.Invoke(this, EventArgs.Empty);
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
