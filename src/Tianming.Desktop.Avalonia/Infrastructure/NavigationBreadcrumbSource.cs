using System;
using System.Collections.Generic;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class NavigationBreadcrumbSource : IBreadcrumbSource
{
    private static readonly Dictionary<PageKey, string> KnownLabels = new()
    {
        [PageKeys.Welcome]           = "欢迎",
        [PageKeys.Dashboard]         = "仪表盘",
        [PageKeys.Editor]            = "草稿",
        [PageKeys.DesignWorld]       = "世界观",
        [PageKeys.DesignCharacter]   = "角色",
        [PageKeys.DesignFaction]     = "势力",
        [PageKeys.DesignLocation]    = "地点",
        [PageKeys.DesignPlot]        = "剧情",
        [PageKeys.DesignMaterials]   = "创意素材",
        [PageKeys.GenerateOutline]   = "战略大纲",
        [PageKeys.GenerateVolume]    = "分卷设计",
        [PageKeys.GenerateChapter]   = "章节规划",
        [PageKeys.GenerateBlueprint] = "章节蓝图",
        [PageKeys.GeneratePipeline]  = "章节生成管道",
        [PageKeys.AIModels]          = "模型",
        [PageKeys.AIKeys]            = "API 密钥",
        [PageKeys.AIPrompts]         = "提示词",
        [PageKeys.AIUsage]           = "使用统计",
        [PageKeys.BookPipeline]      = "一键成书",
        [PageKeys.Packaging]         = "打包",
        [PageKeys.Settings]          = "设置",
    };

    private readonly INavigationService _nav;
    private List<BreadcrumbSegment> _current;

    public NavigationBreadcrumbSource(INavigationService nav)
    {
        _nav = nav;
        _current = new List<BreadcrumbSegment> { new("天命", null) };
        _nav.CurrentKeyChanged += OnNavigated;
    }

    public IReadOnlyList<BreadcrumbSegment> Current => _current;

    public event EventHandler<IReadOnlyList<BreadcrumbSegment>>? SegmentsChanged;

    private void OnNavigated(object? sender, PageKey key)
    {
        var label = KnownLabels.TryGetValue(key, out var known) ? known : key.Id;
        _current = new List<BreadcrumbSegment>
        {
            new("天命", null),
            new(label, key)
        };
        SegmentsChanged?.Invoke(this, _current);
    }
}
