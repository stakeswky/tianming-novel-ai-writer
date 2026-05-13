using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class StatusBarItemTests
{
    [AvaloniaFact]
    public void Defaults_LabelEmpty_KindNeutral_TooltipNull()
    {
        var i = new StatusBarItem();
        Assert.Equal(string.Empty, i.Label);
        Assert.Equal(StatusKind.Neutral, i.Kind);
        Assert.Null(i.TooltipText);
    }

    [AvaloniaFact]
    public void SetLabelAndKind_Persists()
    {
        var i = new StatusBarItem { Label = "Keychain", Kind = StatusKind.Success };
        Assert.Equal("Keychain", i.Label);
        Assert.Equal(StatusKind.Success, i.Kind);
    }

    [AvaloniaFact]
    public void SetTooltipText_Persists()
    {
        var i = new StatusBarItem { TooltipText = "macOS Keychain 可用" };
        Assert.Equal("macOS Keychain 可用", i.TooltipText);
    }
}
