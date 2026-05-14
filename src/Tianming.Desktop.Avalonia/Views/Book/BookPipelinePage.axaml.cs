using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Tianming.Desktop.Avalonia.ViewModels.Book;

namespace Tianming.Desktop.Avalonia.Views.Book;

public partial class BookPipelinePage : UserControl
{
    public BookPipelinePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BookPipelineViewModel vm)
            await vm.LoadAsync();
    }
}
