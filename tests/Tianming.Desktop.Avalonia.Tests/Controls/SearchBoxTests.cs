using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class SearchBoxTests
{
    [AvaloniaFact]
    public void Defaults_TextEmpty_PlaceholderNull_CommandNull()
    {
        var s = new SearchBox();
        Assert.Equal(string.Empty, s.Text);
        Assert.Null(s.Placeholder);
        Assert.Null(s.SubmitCommand);
    }

    [AvaloniaFact]
    public void SetText_Persists()
    {
        var s = new SearchBox { Text = "查找章节" };
        Assert.Equal("查找章节", s.Text);
    }

    [AvaloniaFact]
    public void SetSubmitCommand_Persists()
    {
        var cmd = new RelayCommand(() => { });
        var s = new SearchBox { SubmitCommand = cmd };
        Assert.Same(cmd, s.SubmitCommand);
    }
}
