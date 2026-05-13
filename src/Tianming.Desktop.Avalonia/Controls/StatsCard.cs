using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Controls;

public class StatsCard : TemplatedControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<StatsCard, string>(nameof(Label), string.Empty);
    public static readonly StyledProperty<string> ValueProperty =
        AvaloniaProperty.Register<StatsCard, string>(nameof(Value), string.Empty);
    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<StatsCard, string?>(nameof(Caption));
    public static readonly StyledProperty<StatusKind?> TrendKindProperty =
        AvaloniaProperty.Register<StatsCard, StatusKind?>(nameof(TrendKind));
    public static readonly StyledProperty<object?> AccessoryContentProperty =
        AvaloniaProperty.Register<StatsCard, object?>(nameof(AccessoryContent));

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Value { get => GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string? Caption { get => GetValue(CaptionProperty); set => SetValue(CaptionProperty, value); }
    public StatusKind? TrendKind { get => GetValue(TrendKindProperty); set => SetValue(TrendKindProperty, value); }
    public object? AccessoryContent { get => GetValue(AccessoryContentProperty); set => SetValue(AccessoryContentProperty, value); }
}
