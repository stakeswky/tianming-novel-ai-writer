using System;
using System.Collections.Generic;
using Avalonia.Threading;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class DispatcherTimerScheduler : ITimerScheduler
{
    private readonly Dictionary<string, DispatcherTimer> _timers = new();

    public IDisposable Debounce(string key, TimeSpan delay, Action callback)
    {
        if (_timers.TryGetValue(key, out var existing))
        {
            existing.Stop();
            _timers.Remove(key);
        }
        var timer = new DispatcherTimer { Interval = delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            _timers.Remove(key);
            try { callback(); } catch { /* swallow to keep dispatcher healthy */ }
        };
        _timers[key] = timer;
        timer.Start();
        return new TimerHandle(this, key);
    }

    private sealed class TimerHandle : IDisposable
    {
        private readonly DispatcherTimerScheduler _owner;
        private readonly string _key;
        public TimerHandle(DispatcherTimerScheduler owner, string key) { _owner = owner; _key = key; }
        public void Dispose()
        {
            if (_owner._timers.TryGetValue(_key, out var t))
            {
                t.Stop();
                _owner._timers.Remove(_key);
            }
        }
    }
}
