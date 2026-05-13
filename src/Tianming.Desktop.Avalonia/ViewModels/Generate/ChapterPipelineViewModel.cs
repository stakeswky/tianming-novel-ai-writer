using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tianming.Desktop.Avalonia.ViewModels.Generate;

/// <summary>
/// M4.2 章节生成管道页 VM。
/// 仅静态布局 + mock 数据；真实 ChapterGenerationPipeline / FactSnapshot / CHANGES / Gate
/// 等接入留给 M4.4（pipeline 串联）/ M6.x（Canonicalizer / Humanize / WAL）。
/// </summary>
public partial class ChapterPipelineViewModel : ObservableObject
{
    public string PageTitle => "章节生成管道";
    public string PageIcon  => "⚙️";

    /// <summary>M4.2 阶段是否已接入真实管道。view 用此控制按钮 IsEnabled / badge 显示。</summary>
    public bool IsPipelineImplemented => false;

    /// <summary>左侧"启用章节"列：mock 列表，M4.4 替换为 ChapterPlanning 数据。</summary>
    public ObservableCollection<string> MockChapters { get; } = new()
    {
        "第 1 章 风起青萍",
        "第 2 章 相遇",
        "第 3 章 决意",
    };

    [ObservableProperty]
    private string? _selectedChapter;

    /// <summary>生成节奏 step list（Mac_UI/06 中心列）。</summary>
    public IReadOnlyList<string> GenerationSteps { get; } = new[]
    {
        "FactSnapshot",
        "生成",
        "Humanize",
        "CHANGES",
        "Gate",
        "WAL",
        "保存",
    };

    /// <summary>5 个列标题（页面顶端横向卡片标题）。</summary>
    public IReadOnlyList<string> ColumnTitles { get; } = new[]
    {
        "① 启用章节",
        "② 准备清单",
        "③ 生成节奏",
        "④ CHANGES 预览",
        "⑤ 操作历史",
    };

    /// <summary>M4.4 接入提示文本。</summary>
    public string PipelineDisabledHint => "M4.4 串联 ChapterGenerationPipeline 后启用";
}
