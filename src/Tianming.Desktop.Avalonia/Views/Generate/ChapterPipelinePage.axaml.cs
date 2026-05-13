using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Tianming.Desktop.Avalonia.Views.Generate;

public partial class ChapterPipelinePage : UserControl
{
    public ChapterPipelinePage()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
