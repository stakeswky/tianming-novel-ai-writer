using Avalonia.Controls;
using Avalonia.Interactivity;
using Tianming.Desktop.Avalonia.ViewModels.AI;

namespace Tianming.Desktop.Avalonia.Views.AI;

public partial class ApiKeysPage : UserControl
{
    public ApiKeysPage()
    {
        InitializeComponent();
    }

    private void OnDeleteKeyClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id } && DataContext is ApiKeysViewModel vm)
        {
            vm.DeleteKeyCommand.Execute(id);
        }
    }
}
