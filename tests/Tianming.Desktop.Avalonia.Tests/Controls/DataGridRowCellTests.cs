using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class DataGridRowCellTests
{
    [AvaloniaFact]
    public void Defaults_KindText_ContentEmpty()
    {
        var c = new DataGridRowCell();
        Assert.Equal(DataGridCellKind.Text, c.Kind);
        Assert.Equal(string.Empty, c.Content);
        Assert.Null(c.BadgeKind);
        Assert.Null(c.ClickCommand);
    }

    [AvaloniaFact]
    public void SetKindAndContent_Persists()
    {
        var c = new DataGridRowCell { Kind = DataGridCellKind.Badge, Content = "已发布", BadgeKind = StatusKind.Success };
        Assert.Equal(DataGridCellKind.Badge, c.Kind);
        Assert.Equal("已发布", c.Content);
        Assert.Equal(StatusKind.Success, c.BadgeKind);
    }

    [AvaloniaFact]
    public void Link_SetClickCommand_Persists()
    {
        var cmd = new RelayCommand(() => { });
        var c = new DataGridRowCell { Kind = DataGridCellKind.Link, Content = "查看", ClickCommand = cmd };
        Assert.Same(cmd, c.ClickCommand);
    }
}
