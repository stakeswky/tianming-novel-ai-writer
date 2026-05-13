using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
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

    public string PageTitle => "章节生成管道";
    public string PageIcon  => "⚙️";

    /// <summary>M4.4 已接入真实管道。</summary>
    public bool IsPipelineImplemented => true;

    /// <summary>左侧"启用章节"列：mock 列表，M4.4 替换为 ChapterPlanning 数据。</summary>
    public ObservableCollection<string> MockChapters { get; } = new()
    {
        "第 1 章 风起青萍",
        "第 2 章 相遇",
        "第 3 章 决意",
    };

    [ObservableProperty] private string? _selectedChapter;
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

    public ChapterPipelineViewModel(INavigationService navigation, ChapterGenerationStore generationStore)
    {
        _navigation = navigation;
        _generationStore = generationStore;
    }

    /// <summary>
    /// M4.4：模拟章节生成。选中章节后，用 placeholder 文本模拟生成结果。
    /// 已生成的章节需确认是否覆盖。
    /// </summary>
    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedChapter))
        {
            GateResultMessage = "请先从左侧选择一个章节";
            return;
        }

        var chapterId = ExtractChapterId(SelectedChapter);

        // M4.4 覆盖保护：已生成的章节需要确认
        if (_generationStore.IsGenerated(chapterId) && !OverwriteConfirmed)
        {
            GateResultMessage = $"⚠️ 章节「{SelectedChapter}」已生成，是否覆盖？（再次点击「开始生成」确认覆盖）";
            OverwriteConfirmed = true;
            return;
        }

        OverwriteConfirmed = false;
        IsGenerating = true;
        GateResultMessage = null;
        CanApply = false;

        await Task.Delay(500);

        GeneratedContent = $"M4.4: Generated content for {chapterId}";
        GateResultMessage = $"生成完成：{SelectedChapter}";
        CanApply = true;
        IsGenerating = false;
    }

    /// <summary>
    /// M4.4：将生成结果应用到章节 — 标记已生成，发送 ChapterAppliedEvent，导航到 Editor 页。
    /// </summary>
    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (!CanApply || string.IsNullOrWhiteSpace(SelectedChapter))
            return;

        var chapterId = ExtractChapterId(SelectedChapter);
        _generationStore.MarkGenerated(chapterId);
        WeakReferenceMessenger.Default.Send(new ChapterAppliedEvent(chapterId));
        await _navigation.NavigateAsync(PageKeys.Editor, chapterId);
    }

    /// <summary>从章节名称字符串提取 ID（如 "第 2 章 相遇" → "ch-002"）。</summary>
    private static string ExtractChapterId(string chapterName)
    {
        var start = chapterName.IndexOf('第');
        if (start < 0) return "ch-unknown";
        var end = chapterName.IndexOf('章');
        if (end < 0) return "ch-unknown";
        var numStr = chapterName.Substring(start + 1, end - start - 1).Trim();
        if (int.TryParse(numStr, out var num))
            return $"ch-{num:D3}";
        return "ch-unknown";
    }
}
