using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class SectionCardTests
{
    [AvaloniaFact]
    public void Defaults_HeaderNull_SubtitleNull_HeaderActionsNull()
    {
        var c = new SectionCard();
        Assert.Null(c.Header);
        Assert.Null(c.Subtitle);
        Assert.Null(c.HeaderActions);
    }

    [AvaloniaFact]
    public void SetHeaderAndSubtitle_Persists()
    {
        var c = new SectionCard { Header = "最近项目", Subtitle = "上次打开" };
        Assert.Equal("最近项目", c.Header);
        Assert.Equal("上次打开", c.Subtitle);
    }

    [AvaloniaFact]
    public void HeaderActions_AcceptsControl()
    {
        var btn = new Button { Content = "+" };
        var c = new SectionCard { HeaderActions = btn };
        Assert.Same(btn, c.HeaderActions);
    }
}
