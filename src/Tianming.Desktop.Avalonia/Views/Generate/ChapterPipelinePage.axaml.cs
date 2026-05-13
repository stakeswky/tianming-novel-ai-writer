using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Tianming.Desktop.Avalonia.ViewModels.Generate;

namespace Tianming.Desktop.Avalonia.Views.Generate;

public partial class ChapterPipelinePage : UserControl
{
    public ChapterPipelinePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ChapterPipelineViewModel vm)
            await vm.LoadChaptersAsync();
    }
}
