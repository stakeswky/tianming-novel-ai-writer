using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.ViewModels.Generate;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Generate;

public class ChapterPipelineViewModelTests
{
    private static ChapterPipelineViewModel CreateVm()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-pipeline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return new ChapterPipelineViewModel(new StubNavigation(), new ChapterGenerationStore(root));
    }

    [Fact]
    public void PageTitle_is_章节生成管道()
    {
        var vm = CreateVm();
        Assert.Equal("章节生成管道", vm.PageTitle);
    }

    [Fact]
    public void MockChapters_has_three_items()
    {
        var vm = CreateVm();
        Assert.Equal(3, vm.MockChapters.Count);
        Assert.Contains(vm.MockChapters, c => c.Contains("第 1 章"));
    }

    [Fact]
    public void GenerationSteps_lists_seven_phases()
    {
        var vm = CreateVm();
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
    public void IsImplemented_is_true_for_M4_4()
    {
        // M4.4 已接入真实管道：IsPipelineImplemented 为 true
        var vm = CreateVm();
        Assert.True(vm.IsPipelineImplemented);
    }

    private sealed class StubNavigation : INavigationService
    {
#pragma warning disable CS0067 // Event declared but never used (stub)
        public PageKey? CurrentKey => null;
        public object? CurrentViewModel => null;
        public object? LastParameter => null;
        public bool CanGoBack => false;
        public event EventHandler<PageKey>? CurrentKeyChanged;
#pragma warning restore CS0067
        public Task NavigateAsync(PageKey key, object? parameter = null, CancellationToken ct = default) => Task.CompletedTask;
        public Task GoBackAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
