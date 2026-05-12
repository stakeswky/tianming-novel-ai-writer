using CommunityToolkit.Mvvm.ComponentModel;

namespace Tianming.Desktop.Avalonia.ViewModels.Shell;

public partial class RightConversationViewModel : ObservableObject
{
    [ObservableProperty]
    private string _placeholderText = "对话面板（M4.5 实装）";
}
