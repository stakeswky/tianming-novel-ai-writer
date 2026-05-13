using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Tianming.Desktop.Avalonia.Navigation;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _sp;
    private readonly PageRegistry _registry;
    private readonly Stack<(PageKey Key, object Vm)> _stack = new();

    public NavigationService(IServiceProvider sp, PageRegistry registry)
    {
        _sp = sp;
        _registry = registry;
    }

    public PageKey? CurrentKey       => _stack.Count == 0 ? null : _stack.Peek().Key;
    public object?  CurrentViewModel => _stack.Count == 0 ? null : _stack.Peek().Vm;
    public object?  LastParameter    { get; private set; }
    public bool     CanGoBack        => _stack.Count > 1;

    public event EventHandler<PageKey>? CurrentKeyChanged;

    public Task NavigateAsync(PageKey key, object? parameter = null, CancellationToken ct = default)
    {
        if (!_registry.TryResolve(key, out var vmType, out _))
            throw new InvalidOperationException($"PageKey 未注册：{key.Id}");

        var vm = _sp.GetRequiredService(vmType);
        LastParameter = parameter;
        _stack.Push((key, vm));
        CurrentKeyChanged?.Invoke(this, key);
        return Task.CompletedTask;
    }

    public Task GoBackAsync(CancellationToken ct = default)
    {
        if (!CanGoBack) return Task.CompletedTask;
        _stack.Pop();
        CurrentKeyChanged?.Invoke(this, CurrentKey!.Value);
        return Task.CompletedTask;
    }
}
