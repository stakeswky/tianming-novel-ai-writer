using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Tianming.Desktop.Avalonia.Controls;

public class SidebarTreeItem : TemplatedControl
{
    public static readonly StyledProperty<string> LabelProperty =
        AvaloniaProperty.Register<SidebarTreeItem, string>(nameof(Label), string.Empty);
    public static readonly StyledProperty<string?> IconGlyphProperty =
        AvaloniaProperty.Register<SidebarTreeItem, string?>(nameof(IconGlyph));
    public static readonly StyledProperty<Control?> TrailingProperty =
        AvaloniaProperty.Register<SidebarTreeItem, Control?>(nameof(Trailing));
    public static readonly StyledProperty<bool> IsExpandedProperty =
        AvaloniaProperty.Register<SidebarTreeItem, bool>(nameof(IsExpanded), false);
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<SidebarTreeItem, bool>(nameof(IsSelected), false);
    public static readonly StyledProperty<int> DepthProperty =
        AvaloniaProperty.Register<SidebarTreeItem, int>(nameof(Depth), 0);
    public static readonly StyledProperty<ObservableCollection<SidebarTreeItem>?> ChildrenProperty =
        AvaloniaProperty.Register<SidebarTreeItem, ObservableCollection<SidebarTreeItem>?>(nameof(Children));

    public string Label { get => GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string? IconGlyph { get => GetValue(IconGlyphProperty); set => SetValue(IconGlyphProperty, value); }
    public Control? Trailing { get => GetValue(TrailingProperty); set => SetValue(TrailingProperty, value); }
    public bool IsExpanded { get => GetValue(IsExpandedProperty); set => SetValue(IsExpandedProperty, value); }
    public bool IsSelected { get => GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }
    public int Depth { get => GetValue(DepthProperty); set => SetValue(DepthProperty, value); }
    public ObservableCollection<SidebarTreeItem>? Children { get => GetValue(ChildrenProperty); set => SetValue(ChildrenProperty, value); }
}
