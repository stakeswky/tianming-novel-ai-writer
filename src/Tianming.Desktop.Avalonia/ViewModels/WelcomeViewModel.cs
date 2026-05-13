using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;

namespace Tianming.Desktop.Avalonia.ViewModels;

/// <summary>
/// 单条"最近项目"条目（用于 ProjectCard 渲染）。
/// </summary>
public sealed class RecentProjectVm : ObservableObject
{
    public string Id           { get; init; } = Guid.NewGuid().ToString("N");
    public string Name         { get; init; } = string.Empty;
    public string Path         { get; init; } = string.Empty;
    public string? LastOpened  { get; init; }
    public string? ChapterText { get; init; }
    public double  Progress    { get; init; }
}

/// <summary>
/// M3 欢迎/项目选择 ViewModel。
/// 真实项目持久化在 M3 阶段仍是 stub —— 用内存 List + 模拟数据；M4 接 FileProjectManager。
/// </summary>
public partial class WelcomeViewModel : ObservableObject
{
    private readonly AppPaths _paths;
    private readonly INavigationService _nav;

    [ObservableProperty] private string _newProjectName = "未命名项目";
    [ObservableProperty] private string _defaultProjectRoot = string.Empty;
    [ObservableProperty] private string? _lastOpenedHint;
    [ObservableProperty] private bool _isLocalMode = true;

    public ObservableCollection<RecentProjectVm> RecentProjects { get; } = new();

    public bool HasRecent => RecentProjects.Count > 0;

    public WelcomeViewModel(AppPaths paths, INavigationService nav)
    {
        _paths = paths;
        _nav = nav;
        DefaultProjectRoot = Path.Combine(_paths.AppSupportDirectory, "Projects");

        // Stub：3 条示例最近项目（个人自用版无真实持久化，M4 接 FileProjectManager）
        SeedRecentProjects();
        LastOpenedHint = RecentProjects.Count > 0
            ? $"上次：{RecentProjects[0].Name}"
            : null;

        RecentProjects.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasRecent));
    }

    private void SeedRecentProjects()
    {
        RecentProjects.Add(new RecentProjectVm
        {
            Name        = "山河长安",
            Path        = Path.Combine(DefaultProjectRoot, "山河长安"),
            LastOpened  = "2026-05-11 22:14",
            ChapterText = "32 / 60 章",
            Progress    = 0.53,
        });
        RecentProjects.Add(new RecentProjectVm
        {
            Name        = "星海游侠",
            Path        = Path.Combine(DefaultProjectRoot, "星海游侠"),
            LastOpened  = "2026-05-09 18:02",
            ChapterText = "12 / 80 章",
            Progress    = 0.15,
        });
        RecentProjects.Add(new RecentProjectVm
        {
            Name        = "雾港夜话",
            Path        = Path.Combine(DefaultProjectRoot, "雾港夜话"),
            LastOpened  = "2026-04-30 09:47",
            ChapterText = "8 / 24 章",
            Progress    = 0.33,
        });
    }

    [RelayCommand]
    private async Task CreateProjectAsync()
    {
        var safe = string.IsNullOrWhiteSpace(NewProjectName) ? "未命名项目" : NewProjectName.Trim();
        var dir = Path.Combine(DefaultProjectRoot, $"{safe}-{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "project.json"),
            $"{{\"name\":\"{safe}\",\"createdAt\":\"{DateTime.UtcNow:o}\"}}");

        var recent = new RecentProjectVm
        {
            Name        = safe,
            Path        = dir,
            LastOpened  = DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            ChapterText = "0 / 0 章",
            Progress    = 0.0,
        };
        RecentProjects.Insert(0, recent);
        LastOpenedHint = $"上次：{recent.Name}";

        await _nav.NavigateAsync(PageKeys.Dashboard);
    }

    [RelayCommand]
    private async Task OpenProjectAsync(RecentProjectVm? recent)
    {
        // Stub：M3 没有真实文件选择对话框，只有 RecentProject 触发；新项目走 Create
        if (recent is not null)
        {
            LastOpenedHint = $"上次：{recent.Name}";
            // 把它移到列表顶
            if (RecentProjects.Remove(recent))
                RecentProjects.Insert(0, recent);
        }
        await _nav.NavigateAsync(PageKeys.Dashboard);
    }

    [RelayCommand]
    private void ClearRecent()
    {
        RecentProjects.Clear();
        LastOpenedHint = null;
    }
}
