using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Tianming.Desktop.Avalonia.Navigation;

namespace Tianming.Desktop.Avalonia.Controls;

public class NavRail : TemplatedControl
{
    public static readonly StyledProperty<ObservableCollection<NavRailGroup>> GroupsProperty =
        AvaloniaProperty.Register<NavRail, ObservableCollection<NavRailGroup>>(nameof(Groups));

    public static readonly StyledProperty<PageKey?> ActiveKeyProperty =
        AvaloniaProperty.Register<NavRail, PageKey?>(nameof(ActiveKey));

    public static readonly StyledProperty<ICommand?> NavigateCommandProperty =
        AvaloniaProperty.Register<NavRail, ICommand?>(nameof(NavigateCommand));

    public NavRail()
    {
        SetCurrentValue(GroupsProperty, new ObservableCollection<NavRailGroup>());
    }

    public ObservableCollection<NavRailGroup> Groups
    {
        get => GetValue(GroupsProperty);
        set => SetValue(GroupsProperty, value);
    }
    public PageKey? ActiveKey { get => GetValue(ActiveKeyProperty); set => SetValue(ActiveKeyProperty, value); }
    public ICommand? NavigateCommand { get => GetValue(NavigateCommandProperty); set => SetValue(NavigateCommandProperty, value); }
}
