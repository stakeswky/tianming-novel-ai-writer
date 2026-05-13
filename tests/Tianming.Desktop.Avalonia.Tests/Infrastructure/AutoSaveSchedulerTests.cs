using System;
using System.Collections.Generic;
using Tianming.Desktop.Avalonia.Infrastructure;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class AutoSaveSchedulerTests
{
    [Fact]
    public void Schedule_does_not_fire_immediately()
    {
        var fake = new FakeTimerScheduler();
        var calls = 0;
        var s = new AutoSaveScheduler(fake, TimeSpan.FromSeconds(2));

        s.Schedule("chap-1", () => calls++);

        Assert.Equal(0, calls);
    }

    [Fact]
    public void Schedule_then_advance_fires_callback()
    {
        var fake = new FakeTimerScheduler();
        var calls = 0;
        var s = new AutoSaveScheduler(fake, TimeSpan.FromSeconds(2));

        s.Schedule("chap-1", () => calls++);
        fake.AdvanceAll();

        Assert.Equal(1, calls);
    }

    [Fact]
    public void Schedule_twice_same_key_only_fires_latest()
    {
        var fake = new FakeTimerScheduler();
        var first = 0; var second = 0;
        var s = new AutoSaveScheduler(fake, TimeSpan.FromSeconds(2));

        s.Schedule("chap-1", () => first++);
        s.Schedule("chap-1", () => second++);
        fake.AdvanceAll();

        Assert.Equal(0, first);
        Assert.Equal(1, second);
    }

    [Fact]
    public void Different_keys_independent()
    {
        var fake = new FakeTimerScheduler();
        var a = 0; var b = 0;
        var s = new AutoSaveScheduler(fake, TimeSpan.FromSeconds(2));

        s.Schedule("chap-1", () => a++);
        s.Schedule("chap-2", () => b++);
        fake.AdvanceAll();

        Assert.Equal(1, a);
        Assert.Equal(1, b);
    }
}

internal sealed class FakeTimerScheduler : ITimerScheduler
{
    private readonly Dictionary<string, Action> _pending = new();
    public IDisposable Debounce(string key, TimeSpan delay, Action callback)
    {
        _pending[key] = callback;
        return new Handle(this, key);
    }
    public void AdvanceAll()
    {
        var snapshot = new List<Action>(_pending.Values);
        _pending.Clear();
        foreach (var a in snapshot) a();
    }
    private sealed class Handle : IDisposable
    {
        private readonly FakeTimerScheduler _o; private readonly string _k;
        public Handle(FakeTimerScheduler o, string k) { _o = o; _k = k; }
        public void Dispose() => _o._pending.Remove(_k);
    }
}
