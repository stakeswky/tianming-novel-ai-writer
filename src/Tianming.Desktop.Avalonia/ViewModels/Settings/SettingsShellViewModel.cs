using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Tianming.Desktop.Avalonia.Navigation;

namespace Tianming.Desktop.Avalonia.ViewModels.Settings;

public sealed record SettingsSubNavItem(PageKey Key, string Label, string Icon);

/// <summary>
/// M7 Lane A 设置 shell 容器。沿用 Avalonia 现有 PageRegistry + DataTemplate 模式，
/// 内部用一个 ObservableProperty `SelectedItem` 驱动右侧内容切换；具体 sub-page 内容
/// 由 SettingsShellView 上 ContentControl 的 DataTemplate 解析。
/// </summary>
public partial class SettingsShellViewModel : ObservableObject
{
    public IReadOnlyList<SettingsSubNavItem> SubNavItems { get; }

    [ObservableProperty] private SettingsSubNavItem? _selectedItem;

    public SettingsShellViewModel()
    {
        SubNavItems = new List<SettingsSubNavItem>
        {
            new(PageKeys.SettingsTheme,        "外观主题", "🎨"),
            new(PageKeys.SettingsFollowSystem, "跟随系统", "🌓"),
        };
        SelectedItem = SubNavItems[0];
    }
}
