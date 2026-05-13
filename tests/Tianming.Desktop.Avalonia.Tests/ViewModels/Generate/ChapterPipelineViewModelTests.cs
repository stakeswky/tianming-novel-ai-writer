using System.Linq;
using Tianming.Desktop.Avalonia.ViewModels.Generate;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Generate;

public class ChapterPipelineViewModelTests
{
    [Fact]
    public void PageTitle_is_章节生成管道()
    {
        var vm = new ChapterPipelineViewModel();
        Assert.Equal("章节生成管道", vm.PageTitle);
    }

    [Fact]
    public void MockChapters_has_three_items()
    {
        var vm = new ChapterPipelineViewModel();
        Assert.Equal(3, vm.MockChapters.Count);
        Assert.Contains(vm.MockChapters, c => c.Contains("第 1 章"));
    }

    [Fact]
    public void GenerationSteps_lists_seven_phases()
    {
        var vm = new ChapterPipelineViewModel();
        Assert.Equal(7, vm.GenerationSteps.Count);
        Assert.Equal("FactSnapshot",  vm.GenerationSteps[0]);
        Assert.Equal("生成",          vm.GenerationSteps[1]);
        Assert.Equal("Humanize",      vm.GenerationSteps[2]);
        Assert.Equal("CHANGES",       vm.GenerationSteps[3]);
        Assert.Equal("Gate",          vm.GenerationSteps[4]);
        Assert.Equal("WAL",           vm.GenerationSteps[5]);
        Assert.Equal("保存",          vm.GenerationSteps[6]);
    }

    [Fact]
    public void IsImplemented_is_false_for_M4_2()
    {
        // M4.2 阶段所有真实命令都禁用；view 用这个 flag 控 IsEnabled
        var vm = new ChapterPipelineViewModel();
        Assert.False(vm.IsPipelineImplemented);
    }
}
