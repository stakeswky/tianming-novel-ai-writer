using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Tianming.Desktop.Avalonia.ViewModels.Shell;

namespace Tianming.Desktop.Avalonia.Controls;

public partial class ChatStreamView : UserControl
{
    private ObservableCollection<ConversationBubbleVm>? _subscribedBubbles;

    public static readonly StyledProperty<ObservableCollection<ConversationBubbleVm>?> BubblesProperty =
        AvaloniaProperty.Register<ChatStreamView, ObservableCollection<ConversationBubbleVm>?>(nameof(Bubbles));
    public static readonly StyledProperty<ICommand?> ApproveCommandProperty =
        AvaloniaProperty.Register<ChatStreamView, ICommand?>(nameof(ApproveCommand));
    public static readonly StyledProperty<ICommand?> RejectCommandProperty =
        AvaloniaProperty.Register<ChatStreamView, ICommand?>(nameof(RejectCommand));

    public ObservableCollection<ConversationBubbleVm>? Bubbles
    {
        get => GetValue(BubblesProperty);
        set => SetValue(BubblesProperty, value);
    }

    public ICommand? ApproveCommand
    {
        get => GetValue(ApproveCommandProperty);
        set => SetValue(ApproveCommandProperty, value);
    }

    public ICommand? RejectCommand
    {
        get => GetValue(RejectCommandProperty);
        set => SetValue(RejectCommandProperty, value);
    }

    public ChatStreamView()
    {
        InitializeComponent();
        BubblesProperty.Changed.AddClassHandler<ChatStreamView>((control, _) => control.AttachBubbles());
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void AttachBubbles()
    {
        if (_subscribedBubbles != null)
            _subscribedBubbles.CollectionChanged -= OnBubblesChanged;

        _subscribedBubbles = Bubbles;
        if (_subscribedBubbles != null)
            _subscribedBubbles.CollectionChanged += OnBubblesChanged;
    }

    private void OnBubblesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            var scrollViewer = this.FindControl<ScrollViewer>("StreamScroll");
            scrollViewer?.ScrollToEnd();
        }, DispatcherPriority.Background);
    }
}
