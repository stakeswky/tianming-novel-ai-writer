using System;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class NavigationBreadcrumbSource : IBreadcrumbSource
{
    private readonly INavigationService _nav;
    private readonly PageRegistry _pages;
    private List<BreadcrumbSegment> _current;

    public NavigationBreadcrumbSource(INavigationService nav, PageRegistry pages)
    {
        _nav = nav;
        _pages = pages;
        _current = new List<BreadcrumbSegment> { new("天命", null) };
        _nav.CurrentKeyChanged += OnNavigated;
    }

    public IReadOnlyList<BreadcrumbSegment> Current => _current;

    public event EventHandler<IReadOnlyList<BreadcrumbSegment>>? SegmentsChanged;

    private void OnNavigated(object? sender, PageKey key)
    {
        var label = _pages.GetDisplayName(key);
        _current = new List<BreadcrumbSegment>
        {
            new("天命", null),
            new(label, key)
        };
        SegmentsChanged?.Invoke(this, _current);
    }
}
