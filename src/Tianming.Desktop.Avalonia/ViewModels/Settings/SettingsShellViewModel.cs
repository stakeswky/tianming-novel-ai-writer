using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Tianming.Desktop.Avalonia.Navigation;

namespace Tianming.Desktop.Avalonia.ViewModels.Settings;

public sealed record SettingsSubNavItem(PageKey Key, string Label, string Icon);

/// <summary>
/// M7 Lane A 设置 shell 容器。沿用 Avalonia 现有 PageRegistry + DataTemplate 模式，
/// 用 SelectedItem 驱动右侧内容切换：OnSelectedItemChanged 按 PageKey 查 PageRegistry 拿
/// VM 类型，再用 IServiceProvider 解析实例填到 CurrentPageViewModel。
/// View 上的 ContentControl 绑定 CurrentPageViewModel，App.axaml 的 DataTemplate
/// 会根据 VM 类型自动选 sub-page view。
/// </summary>
public partial class SettingsShellViewModel : ObservableObject
{
    private readonly PageRegistry? _pages;
    private readonly IServiceProvider? _sp;

    public IReadOnlyList<SettingsSubNavItem> SubNavItems { get; }

    [ObservableProperty] private SettingsSubNavItem? _selectedItem;

    [ObservableProperty] private object? _currentPageViewModel;

    /// <summary>
    /// 测试 / 设计期友好的无参 ctor：不接 PageRegistry / IServiceProvider 时
    /// CurrentPageViewModel 保持 null，只用 SubNavItems 验收。
    /// </summary>
    public SettingsShellViewModel() : this(null, null) { }

    public SettingsShellViewModel(PageRegistry? pages, IServiceProvider? sp)
    {
        _pages = pages;
        _sp = sp;
        SubNavItems = new List<SettingsSubNavItem>
        {
            new(PageKeys.SettingsTheme,        "外观主题", "🎨"),
            new(PageKeys.SettingsFollowSystem, "跟随系统", "🌓"),
        };
        SelectedItem = SubNavItems[0];
    }

    partial void OnSelectedItemChanged(SettingsSubNavItem? value)
    {
        if (value is null || _pages is null || _sp is null)
        {
            CurrentPageViewModel = null;
            return;
        }
        if (_pages.TryResolve(value.Key, out var viewModelType, out _))
        {
            CurrentPageViewModel = _sp.GetService(viewModelType);
        }
        else
        {
            CurrentPageViewModel = null;
        }
    }
}
