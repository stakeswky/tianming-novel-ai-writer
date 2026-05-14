using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Tianming.Desktop.Avalonia.ViewModels.Conversation;

namespace Tianming.Desktop.Avalonia.Controls;

public partial class ConversationHistoryDrawer : UserControl
{
    public static readonly StyledProperty<ObservableCollection<SessionListItemVm>?> SessionsProperty =
        AvaloniaProperty.Register<ConversationHistoryDrawer, ObservableCollection<SessionListItemVm>?>(nameof(Sessions));

    public static readonly StyledProperty<SessionListItemVm?> SelectedSessionProperty =
        AvaloniaProperty.Register<ConversationHistoryDrawer, SessionListItemVm?>(
            nameof(SelectedSession),
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public ObservableCollection<SessionListItemVm>? Sessions
    {
        get => GetValue(SessionsProperty);
        set => SetValue(SessionsProperty, value);
    }

    public SessionListItemVm? SelectedSession
    {
        get => GetValue(SelectedSessionProperty);
        set => SetValue(SelectedSessionProperty, value);
    }

    public ConversationHistoryDrawer() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
