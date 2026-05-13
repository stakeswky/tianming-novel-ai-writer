using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Tests.Infrastructure;
using Tianming.Desktop.Avalonia.ViewModels.Editor;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Editor;

public class ChapterEditorViewModelTests
{
    [Fact]
    public void New_vm_has_empty_content_and_not_dirty()
    {
        var vm = NewVm(out _, out _);
        Assert.Equal(string.Empty, vm.Content);
        Assert.False(vm.IsDirty);
        Assert.Equal(0, vm.WordCount);
    }

    [Fact]
    public void Set_content_marks_dirty_and_updates_word_count()
    {
        var vm = NewVm(out _, out _);
        vm.Content = "天命之书";
        Assert.True(vm.IsDirty);
        Assert.Equal(4, vm.WordCount);
    }

    [Fact]
    public void Set_content_schedules_autosave()
    {
        var vm = NewVm(out var store, out var fake);
        vm.Content = "abc";
        fake.AdvanceAll();
        // store should have been called
        Assert.True(store.SaveCalls.Count >= 1);
        Assert.Equal("abc", store.SaveCalls[^1].content);
    }

    [Fact]
    public void Autosave_clears_dirty_after_save()
    {
        var vm = NewVm(out var store, out var fake);
        vm.Content = "abc";
        fake.AdvanceAll();
        // give the async save a chance
        store.WaitAllSaves();
        Assert.False(vm.IsDirty);
    }

    [Fact]
    public void Loaded_content_does_not_mark_dirty()
    {
        var vm = NewVm(out _, out _);
        vm.LoadContent("# initial");
        Assert.False(vm.IsDirty);
        Assert.Equal("# initial", vm.Content);
    }

    private static ChapterEditorViewModel NewVm(out FakeDraftStore store, out FakeTimerScheduler fake)
    {
        store = new FakeDraftStore();
        fake = new FakeTimerScheduler();
        var sch = new AutoSaveScheduler(fake, TimeSpan.FromMilliseconds(50));
        return new ChapterEditorViewModel(
            projectId: "proj-test",
            chapterId: "ch-1",
            title: "第 1 章",
            draftStore: store,
            autoSave: sch);
    }
}

internal sealed class FakeDraftStore : IChapterDraftStore
{
    public List<(string projectId, string chapterId, string content)> SaveCalls { get; } = new();
    private readonly List<Task> _inflight = new();

    public Task SaveDraftAsync(string projectId, string chapterId, string content)
    {
        SaveCalls.Add((projectId, chapterId, content));
        var t = Task.CompletedTask;
        _inflight.Add(t);
        return t;
    }
    public Task<string?> LoadDraftAsync(string projectId, string chapterId) => Task.FromResult<string?>(null);

    public void WaitAllSaves()
    {
        Task.WaitAll(_inflight.ToArray());
    }
}
