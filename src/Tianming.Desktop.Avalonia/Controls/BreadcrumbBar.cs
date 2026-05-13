using System.Collections.ObjectModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls.Primitives;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Controls;

public class BreadcrumbBar : TemplatedControl
{
    public static readonly StyledProperty<ObservableCollection<BreadcrumbSegment>> SegmentsProperty =
        AvaloniaProperty.Register<BreadcrumbBar, ObservableCollection<BreadcrumbSegment>>(nameof(Segments));

    public static readonly StyledProperty<ICommand?> NavigateCommandProperty =
        AvaloniaProperty.Register<BreadcrumbBar, ICommand?>(nameof(NavigateCommand));

    public BreadcrumbBar()
    {
        SetCurrentValue(SegmentsProperty, new ObservableCollection<BreadcrumbSegment>());
    }

    public ObservableCollection<BreadcrumbSegment> Segments
    {
        get => GetValue(SegmentsProperty);
        set => SetValue(SegmentsProperty, value);
    }
    public ICommand? NavigateCommand { get => GetValue(NavigateCommandProperty); set => SetValue(NavigateCommandProperty, value); }
}
