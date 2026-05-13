using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Tianming.Desktop.Avalonia.Infrastructure;

namespace Tianming.Desktop.Avalonia.ViewModels.Editor;

/// <summary>
/// 单个章节 tab 的 ViewModel：内容 / 字数 / dirty / 自动保存调度。
/// 由 EditorWorkspaceViewModel 创建并管理生命周期。
/// </summary>
public sealed partial class ChapterEditorViewModel : ObservableObject
{
    private readonly string _projectId;
    private readonly IChapterDraftStore _draftStore;
    private readonly AutoSaveScheduler _autoSave;
    private bool _loading;

    public string ChapterId { get; }
    public string Title { get; }

    [ObservableProperty] private string _content = string.Empty;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private int _wordCount;
    [ObservableProperty] private DateTime? _savedAt;

    public ChapterEditorViewModel(
        string projectId,
        string chapterId,
        string title,
        IChapterDraftStore draftStore,
        AutoSaveScheduler autoSave)
    {
        _projectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
        ChapterId = chapterId ?? throw new ArgumentNullException(nameof(chapterId));
        Title = title ?? string.Empty;
        _draftStore = draftStore ?? throw new ArgumentNullException(nameof(draftStore));
        _autoSave = autoSave ?? throw new ArgumentNullException(nameof(autoSave));
    }

    /// <summary>用于从存储载入初始内容（不触发 dirty / autosave）。</summary>
    public void LoadContent(string content)
    {
        _loading = true;
        try
        {
            Content = content ?? string.Empty;
            IsDirty = false;
        }
        finally { _loading = false; }
    }

    partial void OnContentChanged(string value)
    {
        WordCount = WordCounter.Count(value);
        if (_loading) return;
        IsDirty = true;
        _autoSave.Schedule(ChapterId, () => _ = SaveAsync(value));
    }

    private async Task SaveAsync(string content)
    {
        await _draftStore.SaveDraftAsync(_projectId, ChapterId, content).ConfigureAwait(false);
        SavedAt = DateTime.Now;
        IsDirty = false;
    }
}
