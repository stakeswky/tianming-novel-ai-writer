using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Navigation;

namespace Tianming.Desktop.Avalonia.ViewModels.Shell;

/// <summary>
/// 主工作区左侧导航。结构：项目摘要顶栏 + 写作组 + 设计组（M4.1 实装）+ 工具组。
/// M3 阶段除「欢迎/仪表盘/设置」外其他项 IsEnabled=false；M4.1 起设计组 6 项启用。
/// </summary>
public partial class LeftNavViewModel : ObservableObject
{
    private readonly INavigationService _nav;

    public ObservableCollection<NavRailGroup> Groups { get; } = new();

    // 项目摘要顶栏（demo 数据，M4 后续接 FileProjectManager 真实项目上下文）
    [ObservableProperty] private string _projectName    = "山河长安";
    [ObservableProperty] private string _projectWordCount = "186,420 字";
    [ObservableProperty] private string _projectUpdatedAt = "上次更新 22:14";

    public LeftNavViewModel(INavigationService nav)
    {
        _nav = nav;

        Groups.Add(new NavRailGroup("写作", new List<NavRailItem>
        {
            new(PageKeys.Welcome,   "欢迎",     "home"),
            new(PageKeys.Dashboard, "仪表盘",   "layout-dashboard"),
        }));

        Groups.Add(new NavRailGroup("设计", new List<NavRailItem>
        {
            new(PageKeys.DesignWorld,     "世界观", "🌍"),
            new(PageKeys.DesignCharacter, "角色",   "👤"),
            new(PageKeys.DesignFaction,   "势力",   "⚔️"),
            new(PageKeys.DesignLocation,  "地点",   "📍"),
            new(PageKeys.DesignPlot,      "剧情",   "📖"),
            new(PageKeys.DesignMaterials, "创意素材", "💡"),
        }));

        Groups.Add(new NavRailGroup("生成", new List<NavRailItem>
        {
            new(PageKeys.GenerateOutline,   "战略大纲",   "📖"),
            new(PageKeys.GenerateVolume,    "分卷设计",   "📚"),
            new(PageKeys.GenerateChapter,   "章节规划",   "📑"),
            new(PageKeys.GenerateBlueprint, "章节蓝图",   "🎬"),
            new(PageKeys.GeneratePipeline,  "章节生成管道", "⚙️"),
        }));

        Groups.Add(new NavRailGroup("工具", new List<NavRailItem>
        {
            new(new PageKey("conversation"),"AI 对话", "message-square", IsEnabled: false),
            new(new PageKey("validation"),  "校验",   "shield-check",   IsEnabled: false),
            new(new PageKey("packaging"),   "打包",   "package",        IsEnabled: false),
            new(PageKeys.Settings,          "设置",   "settings"),
        }));
    }

    [RelayCommand]
    private async Task NavigateAsync(PageKey key)
    {
        // 防御：未注册的 PageKey 会抛，跳过保护
        await _nav.NavigateAsync(key);
    }
}
