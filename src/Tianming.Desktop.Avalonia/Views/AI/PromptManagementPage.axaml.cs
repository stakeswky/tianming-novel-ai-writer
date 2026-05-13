using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Tianming.Desktop.Avalonia.ViewModels.AI;

namespace Tianming.Desktop.Avalonia.Views.AI;

public partial class PromptManagementPage : UserControl
{
    public PromptManagementPage()
    {
        InitializeComponent();
    }

    private void OnEditClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id } && DataContext is PromptManagementViewModel vm)
        {
            var item = vm.Templates.FirstOrDefault(t => t.Id == id);
            if (item != null)
            {
                vm.SelectTemplateCommand.Execute(item);
            }
        }
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id } && DataContext is PromptManagementViewModel vm)
        {
            vm.DeleteTemplateCommand.Execute(id);
        }
    }
}
