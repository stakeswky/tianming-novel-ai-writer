using Avalonia;
using Avalonia.Controls.Primitives;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Controls;

public class StatusBarItem : TemplatedControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<StatusBarItem, string>(nameof(Label), string.Empty);

    public static readonly StyledProperty<StatusKind> KindProperty =
        AvaloniaProperty.Register<StatusBarItem, StatusKind>(nameof(Kind), StatusKind.Neutral);

    public static readonly StyledProperty<string?> TooltipTextProperty =
        AvaloniaProperty.Register<StatusBarItem, string?>(nameof(TooltipText));

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public StatusKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public string? TooltipText { get => GetValue(TooltipTextProperty); set => SetValue(TooltipTextProperty, value); }
}
