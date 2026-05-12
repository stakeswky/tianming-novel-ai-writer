using CommunityToolkit.Mvvm.ComponentModel;

namespace Tianming.Desktop.Avalonia.ViewModels;

public partial class PlaceholderViewModel : ObservableObject
{
    [ObservableProperty]
    private string _message = "此页面将在 M4 实装。";
}
