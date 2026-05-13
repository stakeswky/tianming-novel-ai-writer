using System;

namespace Tianming.Desktop.Avalonia.Infrastructure;

/// <summary>
/// 防抖 / 节流定时器抽象。
/// production 用 DispatcherTimerScheduler（Avalonia UI 线程）；测试用 Fake 控制时间推进。
/// </summary>
public interface ITimerScheduler
{
    /// <summary>
    /// 启动一个 one-shot 定时器。若已存在同 key 的定时器则重置（debounce 语义）。
    /// </summary>
    IDisposable Debounce(string key, TimeSpan delay, Action callback);
}
