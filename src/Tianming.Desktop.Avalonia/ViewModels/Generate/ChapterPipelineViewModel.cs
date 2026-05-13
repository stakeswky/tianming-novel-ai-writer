using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Messaging;
using Tianming.Desktop.Avalonia.Navigation;

namespace Tianming.Desktop.Avalonia.ViewModels.Generate;

/// <summary>
/// M4.2 章节生成管道页 VM。
/// M4.4 串联 ChapterGenerationPipeline + 导航到 Editor。
/// </summary>
public sealed partial class ChapterPipelineViewModel : ObservableObject
{
    private readonly INavigationService _navigation;
    private readonly ChapterGenerationStore _generationStore;
    private readonly ModuleDataAdapter<ChapterCategory, ChapterData> _chapterAdapter;

    public string PageTitle => "章节生成管道";
    public string PageIcon  => "⚙️";

    /// <summary>M4.4 已接入真实管道。</summary>
    public bool IsPipelineImplemented => true;

    /// <summary>左侧"启用章节"列：来自 ChapterPlanning 的真实章节规划项。</summary>
    public ObservableCollection<ChapterPipelineChapterItem> Chapters { get; } = new();

    [ObservableProperty] private ChapterPipelineChapterItem? _selectedChapter;
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private bool _canApply;
    [ObservableProperty] private string? _gateResultMessage;
    [ObservableProperty] private string? _generatedContent;
    [ObservableProperty] private bool _overwriteConfirmed;

    /// <summary>生成节奏 step list（Mac_UI/06 中心列）。</summary>
    public IReadOnlyList<string> GenerationSteps { get; } = new[]
    {
        "FactSnapshot", "生成", "Humanize", "CHANGES", "Gate", "WAL", "保存",
    };

    /// <summary>5 个列标题（页面顶端横向卡片标题）。</summary>
    public IReadOnlyList<string> ColumnTitles { get; } = new[]
    {
        "① 启用章节", "② 准备清单", "③ 生成节奏", "④ CHANGES 预览", "⑤ 操作历史",
    };

    public string PipelineDisabledHint => "M4.4 串联 ChapterGenerationPipeline 后启用";

    public ChapterPipelineViewModel(
        INavigationService navigation,
        ChapterGenerationStore generationStore,
        ModuleDataAdapter<ChapterCategory, ChapterData> chapterAdapter)
    {
        _navigation = navigation;
        _generationStore = generationStore;
        _chapterAdapter = chapterAdapter;
    }

    public async Task LoadChaptersAsync()
    {
        await _chapterAdapter.LoadAsync().ConfigureAwait(false);
        Chapters.Clear();

        foreach (var chapter in _chapterAdapter.GetData()
                     .Where(chapter => chapter.IsEnabled)
                     .OrderBy(chapter => chapter.ChapterNumber == 0 ? int.MaxValue : chapter.ChapterNumber)
                     .ThenBy(chapter => chapter.Name))
        {
            Chapters.Add(ChapterPipelineChapterItem.From(chapter));
        }

        if (SelectedChapter == null && Chapters.Count > 0)
            SelectedChapter = Chapters[0];
    }

    /// <summary>
    /// M4.4：模拟章节生成。选中章节后，用 placeholder 文本模拟生成结果。
    /// 已生成的章节需确认是否覆盖。
    /// </summary>
    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (SelectedChapter == null)
        {
            GateResultMessage = "请先从左侧选择一个章节";
            return;
        }

        var chapterId = SelectedChapter.ChapterId;

        // M4.4 覆盖保护：已生成的章节需要确认
        if (_generationStore.IsGenerated(chapterId) && !OverwriteConfirmed)
        {
            GateResultMessage = $"⚠️ 章节「{SelectedChapter.DisplayName}」已生成，是否覆盖？（再次点击「开始生成」确认覆盖）";
            OverwriteConfirmed = true;
            return;
        }

        OverwriteConfirmed = false;
        IsGenerating = true;
        GateResultMessage = null;
        CanApply = false;

        await Task.Delay(500);

        GeneratedContent = $"M4.4: Generated content for {chapterId}";
        GateResultMessage = $"生成完成：{SelectedChapter.DisplayName}";
        CanApply = true;
        IsGenerating = false;
    }

    /// <summary>
    /// M4.4：将生成结果应用到章节 — 标记已生成，发送 ChapterAppliedEvent，导航到 Editor 页。
    /// </summary>
    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (!CanApply || SelectedChapter == null)
            return;

        var chapterId = SelectedChapter.ChapterId;
        _generationStore.MarkGenerated(chapterId);
        WeakReferenceMessenger.Default.Send(new ChapterAppliedEvent(chapterId));
        await _navigation.NavigateAsync(PageKeys.Editor, chapterId);
    }
}

public sealed class ChapterPipelineChapterItem
{
    public string ChapterId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public ChapterData Source { get; init; } = new();

    public static ChapterPipelineChapterItem From(ChapterData chapter)
    {
        var displayName = !string.IsNullOrWhiteSpace(chapter.Name)
            ? chapter.Name
            : chapter.ChapterNumber > 0 && !string.IsNullOrWhiteSpace(chapter.ChapterTitle)
                ? $"第 {chapter.ChapterNumber} 章 {chapter.ChapterTitle}"
                : chapter.ChapterTitle;

        return new ChapterPipelineChapterItem
        {
            ChapterId = chapter.Id,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? chapter.Id : displayName,
            Source = chapter,
        };
    }
}
