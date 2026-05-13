using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Messaging;

namespace Tianming.Desktop.Avalonia.ViewModels.Generate;

public sealed partial class ChapterPlanningViewModel
    : DataManagementViewModel<ChapterCategory, ChapterData, ChapterPlanningSchema>
    , IRecipient<ChapterAppliedEvent>
{
    private readonly ChapterGenerationStore _generationStore;

    public ChapterPlanningViewModel(ModuleDataAdapter<ChapterCategory, ChapterData> adapter, ChapterGenerationStore generationStore)
        : base(adapter)
    {
        _generationStore = generationStore;
        WeakReferenceMessenger.Default.Register<ChapterAppliedEvent>(this);
    }

    /// <summary>M4.4：检查指定章节是否已生成。</summary>
    public bool IsChapterGenerated(string chapterId) => _generationStore.IsGenerated(chapterId);

    /// <summary>M4.4：获取所有已生成的章节 ID 集合。</summary>
    public IReadOnlySet<string> GeneratedChapterIds => _generationStore.ListGenerated();

    /// <summary>M4.4：生成状态摘要文本（显示在条目列表上方）。</summary>
    public string GeneratedSummary
    {
        get
        {
            var ids = _generationStore.ListGenerated();
            if (ids.Count == 0) return "（暂无已生成章节）";
            return $"已生成 {ids.Count} 个章节：{string.Join(", ", ids)}";
        }
    }

    /// <summary>收到 ChapterAppliedEvent 时触发，通知 View 刷新状态。</summary>
    public event System.Action? ChapterStatusChanged;

    public void Receive(ChapterAppliedEvent message)
    {
        OnPropertyChanged(nameof(GeneratedSummary));
        OnPropertyChanged(nameof(GeneratedChapterIds));
        ChapterStatusChanged?.Invoke();
    }
}
