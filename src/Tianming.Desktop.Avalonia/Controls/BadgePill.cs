using Avalonia;
using Avalonia.Controls.Primitives;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Controls;

public class BadgePill : TemplatedControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<BadgePill, string>(nameof(Text), string.Empty);

    public static readonly StyledProperty<StatusKind> KindProperty =
        AvaloniaProperty.Register<BadgePill, StatusKind>(nameof(Kind), StatusKind.Neutral);

    public static readonly StyledProperty<bool> ShowDotProperty =
        AvaloniaProperty.Register<BadgePill, bool>(nameof(ShowDot), false);

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public StatusKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public bool ShowDot { get => GetValue(ShowDotProperty); set => SetValue(ShowDotProperty, value); }
}
