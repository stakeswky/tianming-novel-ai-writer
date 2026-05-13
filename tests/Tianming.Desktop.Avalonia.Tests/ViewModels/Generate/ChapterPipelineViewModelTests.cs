using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.ViewModels.Generate;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Generate;

public class ChapterPipelineViewModelTests
{
    private static ChapterPipelineViewModel CreateVm()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-pipeline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return new ChapterPipelineViewModel(
            new StubNavigation(),
            new ChapterGenerationStore(root),
            new ModuleDataAdapter<ChapterCategory, ChapterData>(new ChapterPlanningSchema(), root));
    }

    [Fact]
    public void PageTitle_is_章节生成管道()
    {
        var vm = CreateVm();
        Assert.Equal("章节生成管道", vm.PageTitle);
    }

    [Fact]
    public void Chapters_starts_empty_until_loaded_from_planning_adapter()
    {
        var vm = CreateVm();
        Assert.Empty(vm.Chapters);
    }

    [Fact]
    public async Task LoadChaptersAsync_populates_real_chapter_planning_items()
    {
        var (vm, _, _) = await CreateVmWithSeededChapterAsync();

        await vm.LoadChaptersAsync();

        var chapter = Assert.Single(vm.Chapters);
        Assert.Equal("chapter-plan-2", chapter.ChapterId);
        Assert.Equal("第 2 章 相遇", chapter.DisplayName);
        Assert.Same(chapter, vm.SelectedChapter);
    }

    [Fact]
    public async Task GenerateAsync_without_selected_chapter_sets_gate_message()
    {
        var vm = CreateVm();

        await vm.GenerateCommand.ExecuteAsync(null);

        Assert.Equal("请先从左侧选择一个章节", vm.GateResultMessage);
        Assert.False(vm.CanApply);
    }

    [Fact]
    public async Task GenerateAsync_then_ApplyAsync_marks_generated_and_navigates_to_editor()
    {
        var (vm, store, nav) = await CreateVmWithSeededChapterAsync();
        await vm.LoadChaptersAsync();

        await vm.GenerateCommand.ExecuteAsync(null);
        await vm.ApplyCommand.ExecuteAsync(null);

        Assert.True(vm.CanApply);
        Assert.Equal("M4.4: Generated content for chapter-plan-2", vm.GeneratedContent);
        Assert.True(store.IsGenerated("chapter-plan-2"));
        Assert.Equal(PageKeys.Editor, nav.CurrentKey);
        Assert.Equal("chapter-plan-2", nav.LastParameter);
    }

    [Fact]
    public async Task GenerateAsync_requires_second_click_when_chapter_was_already_generated()
    {
        var (vm, store, _) = await CreateVmWithSeededChapterAsync();
        store.MarkGenerated("chapter-plan-2");
        await vm.LoadChaptersAsync();

        await vm.GenerateCommand.ExecuteAsync(null);

        Assert.True(vm.OverwriteConfirmed);
        Assert.False(vm.CanApply);

        await vm.GenerateCommand.ExecuteAsync(null);

        Assert.False(vm.OverwriteConfirmed);
        Assert.True(vm.CanApply);
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

    private static async Task<(ChapterPipelineViewModel vm, ChapterGenerationStore store, StubNavigation nav)> CreateVmWithSeededChapterAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-pipeline-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var adapter = new ModuleDataAdapter<ChapterCategory, ChapterData>(new ChapterPlanningSchema(), root);
        await adapter.AddCategoryAsync(new ChapterCategory { Id = "cat-1", Name = "正稿" });
        await adapter.AddAsync(new ChapterData
        {
            Id = "chapter-plan-2",
            Category = "正稿",
            Name = "第 2 章 相遇",
            ChapterNumber = 2,
            ChapterTitle = "相遇",
            IsEnabled = true,
        });
        var nav = new StubNavigation();
        var store = new ChapterGenerationStore(root);
        return (new ChapterPipelineViewModel(nav, store, adapter), store, nav);
    }

    private sealed class StubNavigation : INavigationService
    {
#pragma warning disable CS0067 // Event declared but never used (stub)
        public PageKey? CurrentKey { get; private set; }
        public object? CurrentViewModel => null;
        public object? LastParameter { get; private set; }
        public bool CanGoBack => false;
        public event EventHandler<PageKey>? CurrentKeyChanged;
#pragma warning restore CS0067
        public Task NavigateAsync(PageKey key, object? parameter = null, CancellationToken ct = default)
        {
            CurrentKey = key;
            LastParameter = parameter;
            return Task.CompletedTask;
        }

        public Task GoBackAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
