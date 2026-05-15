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
    private readonly PageRegistry _pages;

    public ObservableCollection<NavRailGroup> Groups { get; } = new();

    // 项目摘要顶栏（demo 数据，M4 后续接 FileProjectManager 真实项目上下文）
    [ObservableProperty] private string _projectName    = "山河长安";
    [ObservableProperty] private string _projectWordCount = "186,420 字";
    [ObservableProperty] private string _projectUpdatedAt = "上次更新 22:14";

    public LeftNavViewModel(INavigationService nav, PageRegistry pages)
    {
        _nav = nav;
        _pages = pages;

        Groups.Add(new NavRailGroup("写作", new List<NavRailItem>
        {
            new(PageKeys.Welcome,   Label(PageKeys.Welcome),   "home"),
            new(PageKeys.Dashboard, Label(PageKeys.Dashboard), "layout-dashboard"),
            new(PageKeys.Editor,    Label(PageKeys.Editor),    "📝"),
        }));

        Groups.Add(new NavRailGroup("设计", new List<NavRailItem>
        {
            new(PageKeys.DesignWorld,     Label(PageKeys.DesignWorld),     "🌍"),
            new(PageKeys.DesignCharacter, Label(PageKeys.DesignCharacter), "👤"),
            new(PageKeys.DesignFaction,   Label(PageKeys.DesignFaction),   "⚔️"),
            new(PageKeys.DesignLocation,  Label(PageKeys.DesignLocation),  "📍"),
            new(PageKeys.DesignPlot,      Label(PageKeys.DesignPlot),      "📖"),
            new(PageKeys.DesignMaterials, Label(PageKeys.DesignMaterials), "💡"),
        }));

        Groups.Add(new NavRailGroup("生成", new List<NavRailItem>
        {
            new(PageKeys.GenerateOutline,   Label(PageKeys.GenerateOutline),   "📖"),
            new(PageKeys.GenerateVolume,    Label(PageKeys.GenerateVolume),    "📚"),
            new(PageKeys.GenerateChapter,   Label(PageKeys.GenerateChapter),   "📑"),
            new(PageKeys.GenerateBlueprint, Label(PageKeys.GenerateBlueprint), "🎬"),
            new(PageKeys.GeneratePipeline,  Label(PageKeys.GeneratePipeline),  "⚙️"),
        }));

        Groups.Add(new NavRailGroup("AI 管理", new List<NavRailItem>
        {
            // 模型 page 已合并 API Key 管理（问题 #3），LeftNav 不再单列 AIKeys 入口；
            // PageRegistry / ApiKeysPage 保留以防破坏旧 navigation history / 直接路由
            new(PageKeys.AIModels,  Label(PageKeys.AIModels),  "🤖"),
            new(PageKeys.AIPrompts, Label(PageKeys.AIPrompts), "📝"),
            new(PageKeys.AIUsage,   Label(PageKeys.AIUsage),   "📊"),
        }));

        Groups.Add(new NavRailGroup("工具", new List<NavRailItem>
        {
            new(new PageKey("conversation"),"AI 对话", "message-square", IsEnabled: false),
            new(new PageKey("validation"),  "校验",   "shield-check",   IsEnabled: false),
            new(PageKeys.BookPipeline,      Label(PageKeys.BookPipeline), "📚"),
            new(PageKeys.Packaging,         Label(PageKeys.Packaging),    "package"),
            new(PageKeys.Settings,          Label(PageKeys.Settings),     "settings"),
        }));
    }

    private string Label(PageKey key) => _pages.GetDisplayName(key);

    [RelayCommand]
    private async Task NavigateAsync(PageKey key)
    {
        // 防御：未注册的 PageKey 会抛，跳过保护
        await _nav.NavigateAsync(key);
    }
}
