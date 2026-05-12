using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Navigation;

namespace Tianming.Desktop.Avalonia.ViewModels.Shell;

public partial class LeftNavViewModel : ObservableObject
{
    private readonly INavigationService _nav;
    public ObservableCollection<NavEntry> Entries { get; } = new();

    public LeftNavViewModel(INavigationService nav)
    {
        _nav = nav;
        Entries.Add(new NavEntry("欢迎",     PageKeys.Welcome,   "\uE80F"));
        Entries.Add(new NavEntry("主页",     PageKeys.Dashboard, "\uE80F"));
        Entries.Add(new NavEntry("设置",     PageKeys.Settings,  "\uE713"));
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task NavigateAsync(NavEntry entry)
        => await _nav.NavigateAsync(entry.Key);
}

public sealed record NavEntry(string Label, PageKey Key, string Icon);
