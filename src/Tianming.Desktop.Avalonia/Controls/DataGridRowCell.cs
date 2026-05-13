using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Controls;

public enum DataGridCellKind { Text, Badge, Number, Link }

public class DataGridRowCell : TemplatedControl
{
    public static readonly StyledProperty<DataGridCellKind> KindProperty =
        AvaloniaProperty.Register<DataGridRowCell, DataGridCellKind>(nameof(Kind), DataGridCellKind.Text);

    public static readonly StyledProperty<string> ContentProperty =
        AvaloniaProperty.Register<DataGridRowCell, string>(nameof(Content), string.Empty);

    public static readonly StyledProperty<StatusKind?> BadgeKindProperty =
        AvaloniaProperty.Register<DataGridRowCell, StatusKind?>(nameof(BadgeKind));

    public static readonly StyledProperty<ICommand?> ClickCommandProperty =
        AvaloniaProperty.Register<DataGridRowCell, ICommand?>(nameof(ClickCommand));

    public DataGridCellKind Kind { get => GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public string Content { get => GetValue(ContentProperty); set => SetValue(ContentProperty, value); }
    public StatusKind? BadgeKind { get => GetValue(BadgeKindProperty); set => SetValue(BadgeKindProperty, value); }
    public ICommand? ClickCommand { get => GetValue(ClickCommandProperty); set => SetValue(ClickCommandProperty, value); }
}
