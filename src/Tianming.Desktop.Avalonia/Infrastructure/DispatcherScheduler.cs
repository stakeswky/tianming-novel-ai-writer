using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class DispatcherScheduler
{
    public bool IsUIThread => Dispatcher.UIThread.CheckAccess();

    public void Post(Action action) => Dispatcher.UIThread.Post(action);

    public Task InvokeAsync(Action action) => Dispatcher.UIThread.InvokeAsync(action).GetTask();

    public Task<T> InvokeAsync<T>(Func<T> func) => Dispatcher.UIThread.InvokeAsync(func).GetTask();
}
