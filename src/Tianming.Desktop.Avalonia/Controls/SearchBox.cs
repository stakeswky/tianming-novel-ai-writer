using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Data;

namespace Tianming.Desktop.Avalonia.Controls;

public class SearchBox : TemplatedControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<SearchBox, string>(nameof(Text), string.Empty, defaultBindingMode: BindingMode.TwoWay);
    public static readonly StyledProperty<string?> PlaceholderProperty =
        AvaloniaProperty.Register<SearchBox, string?>(nameof(Placeholder));
    public static readonly StyledProperty<ICommand?> SubmitCommandProperty =
        AvaloniaProperty.Register<SearchBox, ICommand?>(nameof(SubmitCommand));

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string? Placeholder { get => GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public ICommand? SubmitCommand { get => GetValue(SubmitCommandProperty); set => SetValue(SubmitCommandProperty, value); }
}
