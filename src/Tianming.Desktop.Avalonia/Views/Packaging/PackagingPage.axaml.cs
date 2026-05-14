using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Tianming.Desktop.Avalonia.ViewModels.Packaging;

namespace Tianming.Desktop.Avalonia.Views.Packaging;

public partial class PackagingPage : UserControl
{
    public PackagingPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PackagingViewModel vm)
            return;

        await vm.RunPreflightCommand.ExecuteAsync(null);
        await vm.RefreshBackupsCommand.ExecuteAsync(null);
    }
}
