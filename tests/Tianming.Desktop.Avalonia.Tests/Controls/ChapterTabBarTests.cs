using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class ChapterTabBarTests
{
    [AvaloniaFact]
    public void Defaults_tabs_empty_active_null()
    {
        var bar = new ChapterTabBar();
        Assert.NotNull(bar.Tabs);
        Assert.Empty(bar.Tabs!);
        Assert.Null(bar.ActiveTab);
    }

    [AvaloniaFact]
    public void Set_tabs_persists()
    {
        var bar = new ChapterTabBar();
        bar.Tabs = new ObservableCollection<ChapterTabItem>
        {
            new("ch-1", "第 1 章", IsDirty: false, IsActive: true),
            new("ch-2", "第 2 章", IsDirty: true,  IsActive: false),
        };
        Assert.Equal(2, bar.Tabs!.Count);
        Assert.Equal("第 1 章", bar.Tabs[0].Title);
        Assert.True(bar.Tabs[1].IsDirty);
    }

    [AvaloniaFact]
    public void Set_active_tab_persists()
    {
        var bar = new ChapterTabBar();
        var t = new ChapterTabItem("ch-7", "末章", false, true);
        bar.ActiveTab = t;
        Assert.Same(t, bar.ActiveTab);
    }
}
