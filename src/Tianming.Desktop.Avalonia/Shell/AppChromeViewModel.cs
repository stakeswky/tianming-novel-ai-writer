using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;

namespace Tianming.Desktop.Avalonia.Shell;

public partial class AppChromeViewModel : ObservableObject
{
    private readonly IBreadcrumbSource _source;
    private readonly INavigationService _nav;

    public ObservableCollection<BreadcrumbSegment> Segments { get; } = new();

    public AppChromeViewModel(IBreadcrumbSource source, INavigationService nav)
    {
        _source = source;
        _nav = nav;
        ApplySegments(_source.Current);
        _source.SegmentsChanged += (_, list) => ApplySegments(list);
    }

    private void ApplySegments(IReadOnlyList<BreadcrumbSegment> list)
    {
        Segments.Clear();
        foreach (var s in list) Segments.Add(s);
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task NavigateAsync(BreadcrumbSegment? segment)
    {
        if (segment?.Target is not { } key) return;
        await _nav.NavigateAsync(key);
    }
}
