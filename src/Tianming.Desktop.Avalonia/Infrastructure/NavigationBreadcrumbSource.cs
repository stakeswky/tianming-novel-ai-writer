using System;
using System.Collections.Generic;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class NavigationBreadcrumbSource : IBreadcrumbSource
{
    private static readonly Dictionary<PageKey, string> KnownLabels = new()
    {
        [PageKeys.Welcome]   = "欢迎",
        [PageKeys.Dashboard] = "仪表盘",
        [PageKeys.Settings]  = "设置",
    };

    private readonly INavigationService _nav;
    private List<BreadcrumbSegment> _current;

    public NavigationBreadcrumbSource(INavigationService nav)
    {
        _nav = nav;
        _current = new List<BreadcrumbSegment> { new("天命", null) };
        _nav.CurrentKeyChanged += OnNavigated;
    }

    public IReadOnlyList<BreadcrumbSegment> Current => _current;

    public event EventHandler<IReadOnlyList<BreadcrumbSegment>>? SegmentsChanged;

    private void OnNavigated(object? sender, PageKey key)
    {
        var label = KnownLabels.TryGetValue(key, out var known) ? known : key.Id;
        _current = new List<BreadcrumbSegment>
        {
            new("天命", null),
            new(label, key)
        };
        SegmentsChanged?.Invoke(this, _current);
    }
}
