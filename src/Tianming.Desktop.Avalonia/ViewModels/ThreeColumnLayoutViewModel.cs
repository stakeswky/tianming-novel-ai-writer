using CommunityToolkit.Mvvm.ComponentModel;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.ViewModels.Shell;

namespace Tianming.Desktop.Avalonia.ViewModels;

public partial class ThreeColumnLayoutViewModel : ObservableObject
{
    private readonly WindowStateStore _windowStore;
    private readonly INavigationService _nav;

    [ObservableProperty] private LeftNavViewModel _leftNav;
    [ObservableProperty] private RightConversationViewModel _rightPanel;
    [ObservableProperty] private object? _center;
    [ObservableProperty] private double _leftColumnWidth;
    [ObservableProperty] private double _rightColumnWidth;

    public ThreeColumnLayoutViewModel(
        WindowStateStore windowStore,
        INavigationService nav,
        LeftNavViewModel left,
        RightConversationViewModel right)
    {
        _windowStore = windowStore;
        _nav = nav;
        _leftNav = left;
        _rightPanel = right;

        var state = _windowStore.Load();
        _leftColumnWidth = state.LeftColumnWidth;
        _rightColumnWidth = state.RightColumnWidth;

        _nav.CurrentKeyChanged += OnNavigated;
    }

    private void OnNavigated(object? sender, PageKey key)
    {
        Center = _nav.CurrentViewModel;
    }
}
