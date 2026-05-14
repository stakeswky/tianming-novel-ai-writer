using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Tianming.Desktop.Avalonia.Views.Conversation;

public partial class ConversationPanelView : UserControl
{
    public ConversationPanelView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
