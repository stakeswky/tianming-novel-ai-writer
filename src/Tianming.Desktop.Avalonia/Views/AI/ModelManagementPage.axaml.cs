using System;
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

    private void OnMoveModelUpClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id } && DataContext is ModelManagementViewModel vm)
        {
            vm.MoveModelUpCommand.Execute(id);
        }
    }

    private void OnMoveModelDownClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id } && DataContext is ModelManagementViewModel vm)
        {
            vm.MoveModelDownCommand.Execute(id);
        }
    }

    private void OnSaveKeyClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id } && DataContext is ModelManagementViewModel vm)
        {
            vm.SaveKeyCommand.Execute(id);
        }
    }

    private void OnClearKeyClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string id } && DataContext is ModelManagementViewModel vm)
        {
            vm.ClearKeyCommand.Execute(id);
        }
    }

    private async void OnPurposeDropDownClosed(object? sender, EventArgs e)
    {
        if (sender is ComboBox { DataContext: ModelConfigItem item } && DataContext is ModelManagementViewModel vm)
        {
            await vm.SaveModelCommand.ExecuteAsync(item);
        }
    }
}
