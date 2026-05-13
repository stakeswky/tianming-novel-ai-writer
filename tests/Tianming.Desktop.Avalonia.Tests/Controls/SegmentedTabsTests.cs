using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class SegmentedTabsTests
{
    [AvaloniaFact]
    public void Defaults_ItemsEmpty_SelectedKeyNull()
    {
        var s = new SegmentedTabs();
        Assert.Empty(s.Items);
        Assert.Null(s.SelectedKey);
        Assert.Null(s.SelectCommand);
    }

    [AvaloniaFact]
    public void AddItems_PersistsAndSelectedKeyWorks()
    {
        var s = new SegmentedTabs();
        s.Items.Add(new SegmentItem("ask", "Ask"));
        s.Items.Add(new SegmentItem("plan", "Plan"));
        s.Items.Add(new SegmentItem("agent", "Agent"));
        s.SelectedKey = "plan";
        Assert.Equal(3, s.Items.Count);
        Assert.Equal("plan", s.SelectedKey);
    }

    [AvaloniaFact]
    public void SetSelectCommand_Persists()
    {
        var cmd = new RelayCommand<string>(_ => { });
        var s = new SegmentedTabs { SelectCommand = cmd };
        Assert.Same(cmd, s.SelectCommand);
    }
}
