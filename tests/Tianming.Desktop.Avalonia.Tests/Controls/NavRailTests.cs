using System.Collections.Generic;
using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Navigation;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class NavRailTests
{
    [AvaloniaFact]
    public void Defaults_GroupsEmpty_ActiveKeyNull_CommandNull()
    {
        var r = new NavRail();
        Assert.Empty(r.Groups);
        Assert.Null(r.ActiveKey);
        Assert.Null(r.NavigateCommand);
    }

    [AvaloniaFact]
    public void AddGroups_Persists()
    {
        var r = new NavRail();
        r.Groups.Add(new NavRailGroup("写作", new List<NavRailItem>
        {
            new(PageKeys.Welcome, "欢迎", "home"),
            new(PageKeys.Dashboard, "仪表盘", "layout-dashboard"),
        }));
        Assert.Single(r.Groups);
        Assert.Equal(2, r.Groups[0].Items.Count);
        Assert.Equal("欢迎", r.Groups[0].Items[0].Label);
    }

    [AvaloniaFact]
    public void SetActiveKey_AndCommand_Persist()
    {
        var cmd = new RelayCommand<PageKey>(_ => { });
        var r = new NavRail { ActiveKey = PageKeys.Welcome, NavigateCommand = cmd };
        Assert.Equal(PageKeys.Welcome, r.ActiveKey);
        Assert.Same(cmd, r.NavigateCommand);
    }
}
