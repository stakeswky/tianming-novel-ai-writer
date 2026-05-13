using System;

namespace Tianming.Desktop.Avalonia.Infrastructure;

/// <summary>
/// 章节编辑器自动保存调度：每个 chapterId 一个独立防抖 key，
/// Schedule(chapterId, save) 推迟到 delay 后触发，期间再次调用则重置。
/// </summary>
public sealed class AutoSaveScheduler
{
    private readonly ITimerScheduler _scheduler;
    private readonly TimeSpan _delay;

    public AutoSaveScheduler(ITimerScheduler scheduler, TimeSpan delay)
    {
        _scheduler = scheduler;
        _delay = delay;
    }

    public void Schedule(string chapterId, Action saveCallback)
    {
        if (string.IsNullOrEmpty(chapterId)) return;
        _scheduler.Debounce($"chapter:{chapterId}", _delay, saveCallback);
    }
}
