using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.Navigation;

namespace Tianming.Desktop.Avalonia.ViewModels.Shell;

/// <summary>
/// M3 主工作区左侧导航。重构成 NavRail.Groups 结构：项目摘要顶栏 + 写作组 + 工具组。
/// M3 阶段除「欢迎/仪表盘/设置」外其他项 IsEnabled=false（M4 起逐步实装）。
/// </summary>
public partial class LeftNavViewModel : ObservableObject
{
    private readonly INavigationService _nav;

    public ObservableCollection<NavRailGroup> Groups { get; } = new();

    // 项目摘要顶栏（demo 数据，M4 接 FileProjectManager 真实项目上下文）
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
            new(new PageKey("draft"),     "草稿",   "file-text",     IsEnabled: false),
            new(new PageKey("outline"),   "大纲",   "list",          IsEnabled: false),
            new(new PageKey("characters"),"角色",   "users",         IsEnabled: false),
            new(new PageKey("world"),     "世界观", "globe",         IsEnabled: false),
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
