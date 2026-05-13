using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class SidebarTreeItemTests
{
    [AvaloniaFact]
    public void Defaults_LabelEmpty_IconNull_NotSelected_DepthZero()
    {
        var i = new SidebarTreeItem();
        Assert.Equal(string.Empty, i.Label);
        Assert.Null(i.IconGlyph);
        Assert.False(i.IsSelected);
        Assert.False(i.IsExpanded);
        Assert.Equal(0, i.Depth);
        Assert.Null(i.Children);
    }

    [AvaloniaFact]
    public void SetLabelAndDepth_Persists()
    {
        var i = new SidebarTreeItem { Label = "第一卷 · 序章", Depth = 1, IsExpanded = true };
        Assert.Equal("第一卷 · 序章", i.Label);
        Assert.Equal(1, i.Depth);
        Assert.True(i.IsExpanded);
    }

    [AvaloniaFact]
    public void Children_CanBeSet()
    {
        var children = new ObservableCollection<SidebarTreeItem>
        {
            new() { Label = "第 1 章", Depth = 2 },
            new() { Label = "第 2 章", Depth = 2 },
        };
        var i = new SidebarTreeItem { Label = "第一卷", Depth = 1, Children = children };
        Assert.Equal(2, i.Children!.Count);
    }
}
