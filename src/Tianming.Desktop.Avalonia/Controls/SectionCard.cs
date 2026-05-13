using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Tianming.Desktop.Avalonia.Controls;

public class SectionCard : HeaderedContentControl
{
    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<SectionCard, string?>(nameof(Subtitle));

    public static readonly StyledProperty<object?> HeaderActionsProperty =
        AvaloniaProperty.Register<SectionCard, object?>(nameof(HeaderActions));

    public string? Subtitle { get => GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public object? HeaderActions { get => GetValue(HeaderActionsProperty); set => SetValue(HeaderActionsProperty, value); }
}
