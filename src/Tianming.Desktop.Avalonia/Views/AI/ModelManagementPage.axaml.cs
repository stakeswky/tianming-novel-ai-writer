using Avalonia.Controls;
using Avalonia.Interactivity;
using Tianming.Desktop.Avalonia.ViewModels.AI;

namespace Tianming.Desktop.Avalonia.Views.AI;

public partial class ModelManagementPage : UserControl
{
    public ModelManagementPage()
    {
        InitializeComponent();
    }

    private void OnSetActiveClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id } && DataContext is ModelManagementViewModel vm)
        {
            vm.SetActiveCommand.Execute(id);
        }
    }

    private void OnDeleteModelClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id } && DataContext is ModelManagementViewModel vm)
        {
            vm.DeleteModelCommand.Execute(id);
        }
    }
}
