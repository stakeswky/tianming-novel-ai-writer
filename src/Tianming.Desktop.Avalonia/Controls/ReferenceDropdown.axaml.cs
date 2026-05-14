using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Tianming.Desktop.Avalonia.ViewModels.Conversation;

namespace Tianming.Desktop.Avalonia.Controls;

public partial class ReferenceDropdown : UserControl
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ReferenceDropdown, bool>(nameof(IsOpen));

    public static readonly StyledProperty<ObservableCollection<ReferenceItemVm>?> ItemsProperty =
        AvaloniaProperty.Register<ReferenceDropdown, ObservableCollection<ReferenceItemVm>?>(nameof(Items));

    public static readonly StyledProperty<ReferenceItemVm?> SelectedItemProperty =
        AvaloniaProperty.Register<ReferenceDropdown, ReferenceItemVm?>(
            nameof(SelectedItem),
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public ObservableCollection<ReferenceItemVm>? Items
    {
        get => GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    public ReferenceItemVm? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public ReferenceDropdown() => InitializeComponent();

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
