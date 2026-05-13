using CommunityToolkit.Mvvm.ComponentModel;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "天命";
    [ObservableProperty] private ThreeColumnLayoutViewModel _layout;
    [ObservableProperty] private AppChromeViewModel _chrome;
    [ObservableProperty] private AppStatusBarViewModel _statusBar;

    public MainWindowViewModel(
        ThreeColumnLayoutViewModel layout,
        AppChromeViewModel chrome,
        AppStatusBarViewModel statusBar)
    {
        _layout = layout;
        _chrome = chrome;
        _statusBar = statusBar;
    }
}
