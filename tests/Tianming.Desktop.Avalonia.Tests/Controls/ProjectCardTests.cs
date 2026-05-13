using Avalonia.Headless.XUnit;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class ProjectCardTests
{
    [AvaloniaFact]
    public void Defaults_NameEmpty_CoverNull_ProgressZero()
    {
        var c = new ProjectCard();
        Assert.Equal(string.Empty, c.ProjectName);
        Assert.Null(c.Cover);
        Assert.Null(c.LastOpenedText);
        Assert.Null(c.ChapterProgress);
        Assert.Equal(0.0, c.ProgressPercent);
        Assert.Null(c.OpenCommand);
    }

    [AvaloniaFact]
    public void SetAllProperties_Persists()
    {
        var c = new ProjectCard
        {
            ProjectName = "第九纪元",
            LastOpenedText = "3 小时前",
            ChapterProgress = "12/60",
            ProgressPercent = 0.2
        };
        Assert.Equal("第九纪元", c.ProjectName);
        Assert.Equal("3 小时前", c.LastOpenedText);
        Assert.Equal("12/60", c.ChapterProgress);
        Assert.Equal(0.2, c.ProgressPercent);
    }

    [AvaloniaFact]
    public void SetOpenCommand_Persists()
    {
        var cmd = new RelayCommand(() => { });
        var c = new ProjectCard { OpenCommand = cmd };
        Assert.Same(cmd, c.OpenCommand);
    }
}
