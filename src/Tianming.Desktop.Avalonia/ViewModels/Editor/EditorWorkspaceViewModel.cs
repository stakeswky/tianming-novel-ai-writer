using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Infrastructure;

namespace Tianming.Desktop.Avalonia.ViewModels.Editor;

/// <summary>
/// 章节编辑器工作区根 VM：管理多个 ChapterEditorViewModel（每 tab 一个），
/// 暴露 Tabs（投影成 ChapterTabItem 给 ChapterTabBar 渲染）、ActiveTab、ActiveEditor。
/// </summary>
public sealed partial class EditorWorkspaceViewModel : ObservableObject
{
    private readonly string _projectId;
    private readonly IChapterDraftStore _draftStore;
    private readonly AutoSaveScheduler _autoSave;

    /// <summary>所有打开的章节编辑器（每 tab 一个 VM）。</summary>
    public ObservableCollection<ChapterEditorViewModel> Editors { get; } = new();

    /// <summary>给 ChapterTabBar 渲染的投影。</summary>
    public ObservableCollection<ChapterTabItem> Tabs { get; } = new();

    [ObservableProperty] private ChapterTabItem? _activeTab;
    [ObservableProperty] private ChapterEditorViewModel? _activeEditor;

    public EditorWorkspaceViewModel(string projectId, IChapterDraftStore draftStore, AutoSaveScheduler autoSave)
    {
        _projectId = projectId ?? throw new ArgumentNullException(nameof(projectId));
        _draftStore = draftStore ?? throw new ArgumentNullException(nameof(draftStore));
        _autoSave = autoSave ?? throw new ArgumentNullException(nameof(autoSave));

        // 启动给一个示例草稿 tab，让 dotnet run 切到草稿页时可见非空内容。
        AddTab("ch-001", "第 1 章 蒙学开篇");
        ActiveEditor!.LoadContent("# 第 1 章 蒙学开篇\n\n清晨，少年推开木门——");
    }

    // AddTab 用 2 个参数，无法直接生成 RelayCommand；保持为公开方法。
    public void AddTab(string chapterId, string title)
    {
        var editor = new ChapterEditorViewModel(_projectId, chapterId, title, _draftStore, _autoSave);
        Editors.Add(editor);
        var item = new ChapterTabItem(chapterId, title, IsDirty: false, IsActive: true);
        Tabs.Add(item);
        ActivateTab(item);
    }

    [RelayCommand]
    public void CloseTab(ChapterTabItem? tab)
    {
        if (tab == null) return;
        var idx = Tabs.IndexOf(tab);
        if (idx < 0) return;
        Tabs.RemoveAt(idx);
        var ed = Editors.FirstOrDefault(e => e.ChapterId == tab.ChapterId);
        if (ed != null) Editors.Remove(ed);

        if (Tabs.Count == 0)
        {
            ActiveTab = null;
            ActiveEditor = null;
            return;
        }
        var neighbor = Tabs[Math.Min(idx, Tabs.Count - 1)];
        ActivateTab(neighbor);
    }

    [RelayCommand]
    public void ActivateTab(ChapterTabItem? tab)
    {
        if (tab == null) return;
        ActiveTab = tab;
        ActiveEditor = Editors.FirstOrDefault(e => e.ChapterId == tab.ChapterId);
    }
}
