using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly INavigationService _navigation;

    [ObservableProperty] private string _title = "天命";
    [ObservableProperty] private ThreeColumnLayoutViewModel _layout;
    [ObservableProperty] private AppChromeViewModel _chrome;
    [ObservableProperty] private AppStatusBarViewModel _statusBar;

    public MainWindowViewModel(
        ThreeColumnLayoutViewModel layout,
        AppChromeViewModel chrome,
        AppStatusBarViewModel statusBar,
        INavigationService navigation)
    {
        _layout = layout;
        _chrome = chrome;
        _statusBar = statusBar;
        _navigation = navigation ?? throw new ArgumentNullException(nameof(navigation));
    }

    // ─── M5 NativeMenu 命令 ────────────────────────────────────────────
    // 关于天命：暂时 console 输出，M4 起接入"关于"对话框或 Settings 内嵌片段。
    [RelayCommand]
    private void About()
    {
        Console.WriteLine("天命 macOS 迁移版（自用，开发态）");
    }

    // ⌘逗号 → 偏好（M3 Settings 占位页）
    [RelayCommand]
    private Task OpenPreferencesAsync()
        => _navigation.NavigateAsync(PageKeys.Settings);

    // ⌘N → 新建项目（M3/M4 由 Welcome 页承载真实新建对话，M5 先导航过去）
    [RelayCommand]
    private Task NewProjectAsync()
        => _navigation.NavigateAsync(PageKeys.Welcome);

    // ⌘O → 打开项目（同上，M3 时落到 Welcome；M4 接入文件夹选择对话框）
    [RelayCommand]
    private Task OpenProjectAsync()
        => _navigation.NavigateAsync(PageKeys.Welcome);

    // ⌘S → 保存（M4 编辑器接入前是 stub；先 hook 命令通路，避免菜单项灰）
    [RelayCommand]
    private void Save()
    {
        // intentional no-op; editor save 由 M4 编辑器实装
    }

    // ⌘Q → 退出
    [RelayCommand]
    private void Quit()
    {
        if (global::Avalonia.Application.Current?.ApplicationLifetime
            is global::Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
