using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Tianming.Desktop.Avalonia.ViewModels.Conversation;

namespace Tianming.Desktop.Avalonia.Controls;

public partial class PlanStepListView : UserControl
{
    public static readonly StyledProperty<ObservableCollection<PlanStepVm>?> StepsProperty =
        AvaloniaProperty.Register<PlanStepListView, ObservableCollection<PlanStepVm>?>(nameof(Steps));

    public ObservableCollection<PlanStepVm>? Steps
    {
        get => GetValue(StepsProperty);
        set => SetValue(StepsProperty, value);
    }

    public PlanStepListView() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
