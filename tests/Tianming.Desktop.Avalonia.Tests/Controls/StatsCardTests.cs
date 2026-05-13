using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class StatsCardTests
{
    [AvaloniaFact]
    public void Defaults_AllStringsEmptyOrNull()
    {
        var s = new StatsCard();
        Assert.Equal(string.Empty, s.Label);
        Assert.Equal(string.Empty, s.Value);
        Assert.Null(s.Caption);
        Assert.Null(s.TrendKind);
        Assert.Null(s.AccessoryContent);
    }

    [AvaloniaFact]
    public void SetLabelAndValueAndCaption_Persists()
    {
        var s = new StatsCard { Label = "总字数", Value = "328,742", Caption = "本周 +12.3%", TrendKind = StatusKind.Success };
        Assert.Equal("总字数", s.Label);
        Assert.Equal("328,742", s.Value);
        Assert.Equal("本周 +12.3%", s.Caption);
        Assert.Equal(StatusKind.Success, s.TrendKind);
    }

    [AvaloniaFact]
    public void AccessoryContent_AcceptsControl()
    {
        var sparkline = new Border();
        var s = new StatsCard { AccessoryContent = sparkline };
        Assert.Same(sparkline, s.AccessoryContent);
    }
}
