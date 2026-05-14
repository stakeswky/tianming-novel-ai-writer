using System;
using System.Collections.Generic;

namespace Tianming.Desktop.Avalonia.Navigation;

public sealed class PageRegistry
{
    private readonly Dictionary<PageKey, (Type ViewModelType, Type ViewType, string? DisplayName)> _map = new();

    public void Register<TViewModel, TView>(PageKey key, string? displayName = null)
        where TViewModel : class
        where TView      : class
    {
        _map[key] = (typeof(TViewModel), typeof(TView), NormalizeDisplayName(displayName));
    }

    public bool TryResolve(PageKey key, out Type viewModelType, out Type viewType)
    {
        if (_map.TryGetValue(key, out var pair))
        {
            viewModelType = pair.ViewModelType;
            viewType      = pair.ViewType;
            return true;
        }
        viewModelType = typeof(object);
        viewType      = typeof(object);
        return false;
    }

    public string GetDisplayName(PageKey key)
    {
        return _map.TryGetValue(key, out var pair) && !string.IsNullOrWhiteSpace(pair.DisplayName)
            ? pair.DisplayName
            : key.Id;
    }

    public IReadOnlyCollection<PageKey> Keys => _map.Keys;

    private static string? NormalizeDisplayName(string? displayName)
    {
        return string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
    }
}
