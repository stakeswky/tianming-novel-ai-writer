using System;

namespace Tianming.Desktop.Avalonia.Infrastructure;

/// <summary>
/// 抽象 16ms 批量 flush 调度器；生产用 DispatcherTimer，测试用 Fake。
/// </summary>
public interface IDispatcherScheduler
{
    IDisposable ScheduleRecurring(TimeSpan interval, Action callback);
    void Post(Action callback);
}
