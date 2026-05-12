using CommunityToolkit.Mvvm.ComponentModel;

namespace Tianming.Desktop.Avalonia.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "天命";

    [ObservableProperty]
    private ThreeColumnLayoutViewModel _layout;

    public MainWindowViewModel(ThreeColumnLayoutViewModel layout) { _layout = layout; }
}
