using System;
using Avalonia.Threading;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class AvaloniaDispatcherScheduler : IDispatcherScheduler
{
    public IDisposable ScheduleRecurring(TimeSpan interval, Action callback)
    {
        var timer = new DispatcherTimer { Interval = interval };
        timer.Tick += (_, _) => callback();
        timer.Start();
        return new TimerHandle(timer);
    }

    public void Post(Action callback) => Dispatcher.UIThread.Post(callback);

    private sealed class TimerHandle : IDisposable
    {
        private readonly DispatcherTimer _timer;

        public TimerHandle(DispatcherTimer timer) => _timer = timer;

        public void Dispose() => _timer.Stop();
    }
}
