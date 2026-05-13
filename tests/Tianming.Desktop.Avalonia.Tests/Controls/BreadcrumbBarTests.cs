using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class BreadcrumbBarTests
{
    [AvaloniaFact]
    public void Defaults_SegmentsEmpty_CommandNull()
    {
        var b = new BreadcrumbBar();
        Assert.Empty(b.Segments);
        Assert.Null(b.NavigateCommand);
    }

    [AvaloniaFact]
    public void AddSegments_Persists()
    {
        var b = new BreadcrumbBar();
        b.Segments.Add(new BreadcrumbSegment("天命", null));
        b.Segments.Add(new BreadcrumbSegment("欢迎", PageKeys.Welcome));
        Assert.Equal(2, b.Segments.Count);
        Assert.Null(b.Segments[0].Target);
        Assert.Equal(PageKeys.Welcome, b.Segments[1].Target);
    }

    [AvaloniaFact]
    public void SetNavigateCommand_Persists()
    {
        var cmd = new RelayCommand<BreadcrumbSegment>(_ => { });
        var b = new BreadcrumbBar { NavigateCommand = cmd };
        Assert.Same(cmd, b.NavigateCommand);
    }
}
