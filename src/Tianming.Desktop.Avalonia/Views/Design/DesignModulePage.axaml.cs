using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Tianming.Desktop.Avalonia.Views.Design;

public partial class DesignModulePage : UserControl
{
    public DesignModulePage()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
