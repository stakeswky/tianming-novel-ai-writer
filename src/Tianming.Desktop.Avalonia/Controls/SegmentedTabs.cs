using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace Tianming.Desktop.Avalonia.Controls;

public class SegmentedTabs : TemplatedControl
{
    public static readonly StyledProperty<ObservableCollection<SegmentItem>> ItemsProperty =
        AvaloniaProperty.Register<SegmentedTabs, ObservableCollection<SegmentItem>>(nameof(Items));

    public static readonly StyledProperty<string?> SelectedKeyProperty =
        AvaloniaProperty.Register<SegmentedTabs, string?>(nameof(SelectedKey));

    public static readonly StyledProperty<ICommand?> SelectCommandProperty =
        AvaloniaProperty.Register<SegmentedTabs, ICommand?>(nameof(SelectCommand));

    public SegmentedTabs()
    {
        SetCurrentValue(ItemsProperty, new ObservableCollection<SegmentItem>());
    }

    public ObservableCollection<SegmentItem> Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }
    public string? SelectedKey { get => GetValue(SelectedKeyProperty); set => SetValue(SelectedKeyProperty, value); }
    public ICommand? SelectCommand { get => GetValue(SelectCommandProperty); set => SetValue(SelectCommandProperty, value); }
}
