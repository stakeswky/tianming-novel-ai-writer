using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace Tianming.Desktop.Avalonia.Controls;

public class ToolCallCard : TemplatedControl
{
    public static readonly StyledProperty<string> ToolNameProperty =
        AvaloniaProperty.Register<ToolCallCard, string>(nameof(ToolName), string.Empty);
    public static readonly StyledProperty<string?> ArgumentsPreviewProperty =
        AvaloniaProperty.Register<ToolCallCard, string?>(nameof(ArgumentsPreview));
    public static readonly StyledProperty<ToolCallState> StateProperty =
        AvaloniaProperty.Register<ToolCallCard, ToolCallState>(nameof(State), ToolCallState.Pending);
    public static readonly StyledProperty<ICommand?> ApproveCommandProperty =
        AvaloniaProperty.Register<ToolCallCard, ICommand?>(nameof(ApproveCommand));
    public static readonly StyledProperty<ICommand?> RejectCommandProperty =
        AvaloniaProperty.Register<ToolCallCard, ICommand?>(nameof(RejectCommand));

    public string ToolName { get => GetValue(ToolNameProperty); set => SetValue(ToolNameProperty, value); }
    public string? ArgumentsPreview { get => GetValue(ArgumentsPreviewProperty); set => SetValue(ArgumentsPreviewProperty, value); }
    public ToolCallState State { get => GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public ICommand? ApproveCommand { get => GetValue(ApproveCommandProperty); set => SetValue(ApproveCommandProperty, value); }
    public ICommand? RejectCommand { get => GetValue(RejectCommandProperty); set => SetValue(RejectCommandProperty, value); }
}
