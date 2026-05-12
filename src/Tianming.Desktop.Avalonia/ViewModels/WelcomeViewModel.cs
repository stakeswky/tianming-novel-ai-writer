using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;

namespace Tianming.Desktop.Avalonia.ViewModels;

public partial class WelcomeViewModel : ObservableObject
{
    private readonly AppPaths _paths;
    private readonly INavigationService _nav;

    [ObservableProperty] private string _newProjectName = "未命名项目";

    public WelcomeViewModel(AppPaths paths, INavigationService nav)
    {
        _paths = paths;
        _nav = nav;
    }

    [RelayCommand]
    private async Task CreateProjectAsync()
    {
        var safe = string.IsNullOrWhiteSpace(NewProjectName) ? "未命名项目" : NewProjectName.Trim();
        var dir = Path.Combine(_paths.AppSupportDirectory, "Projects", $"{safe}-{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "project.json"),
            $"{{\"name\":\"{safe}\",\"createdAt\":\"{DateTime.UtcNow:o}\"}}");
        await _nav.NavigateAsync(PageKeys.Dashboard);
    }
}
