using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class BadgePillTests
{
    [AvaloniaFact]
    public void Defaults_TextEmpty_KindNeutral_ShowDotFalse()
    {
        var b = new BadgePill();
        Assert.Equal(string.Empty, b.Text);
        Assert.Equal(StatusKind.Neutral, b.Kind);
        Assert.False(b.ShowDot);
    }

    [AvaloniaFact]
    public void SetText_PersistsValue()
    {
        var b = new BadgePill { Text = "已连接" };
        Assert.Equal("已连接", b.Text);
    }

    [AvaloniaFact]
    public void SetKind_PersistsAndPropertyChanged()
    {
        var b = new BadgePill();
        var raised = false;
        b.PropertyChanged += (_, e) =>
        {
            if (e.Property == BadgePill.KindProperty) raised = true;
        };
        b.Kind = StatusKind.Success;
        Assert.True(raised);
        Assert.Equal(StatusKind.Success, b.Kind);
    }
}
