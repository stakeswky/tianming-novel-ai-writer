using System;
using System.Collections.Generic;
using Tianming.Desktop.Avalonia.Infrastructure;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public sealed class FakeDispatcherScheduler : IDispatcherScheduler
{
    private readonly List<Action> _recurring = new();
    private readonly List<Action> _posts = new();

    public IDisposable ScheduleRecurring(TimeSpan interval, Action callback)
    {
        _recurring.Add(callback);
        return new Disposable(() => _recurring.Remove(callback));
    }

    public void Post(Action callback) => _posts.Add(callback);

    public void Tick()
    {
        foreach (var callback in _recurring.ToArray())
            callback();
    }

    public void FlushPosts()
    {
        var snapshot = _posts.ToArray();
        _posts.Clear();
        foreach (var callback in snapshot)
            callback();
    }

    private sealed class Disposable : IDisposable
    {
        private readonly Action _dispose;

        public Disposable(Action dispose) => _dispose = dispose;

        public void Dispose() => _dispose();
    }
}
