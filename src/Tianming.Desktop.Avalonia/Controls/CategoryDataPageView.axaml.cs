using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.Controls;

public partial class CategoryDataPageView : UserControl
{
    private StackPanel? _formHost;

    public CategoryDataPageView()
    {
        InitializeComponent();
        _formHost = this.FindControl<StackPanel>("FormHost");
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // 当 DataContext 是 DataManagementViewModel 派生 VM 时，监听 SelectedItem 变化重建表单。
        if (DataContext is INotifyPropertyChanged inpc)
        {
            inpc.PropertyChanged -= OnVmPropertyChanged;
            inpc.PropertyChanged += OnVmPropertyChanged;
        }
        RebuildForm();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "SelectedItem")
            RebuildForm();
    }

    private void RebuildForm()
    {
        if (_formHost == null) return;
        _formHost.Children.Clear();

        var vm = DataContext;
        if (vm == null) return;

        var fieldsProp = vm.GetType().GetProperty("Fields");
        var selectedItemProp = vm.GetType().GetProperty("SelectedItem");
        if (fieldsProp == null || selectedItemProp == null) return;

        var fields = fieldsProp.GetValue(vm) as IReadOnlyList<FieldDescriptor>;
        var selectedItem = selectedItemProp.GetValue(vm);
        if (fields == null) return;

        foreach (var field in fields)
        {
            var row = BuildFieldRow(field, selectedItem);
            _formHost.Children.Add(row);
        }
    }

    private static Control BuildFieldRow(FieldDescriptor field, object? selectedItem)
    {
        var label = new TextBlock
        {
            Text = field.Label + (field.Required ? " *" : string.Empty),
            FontWeight = global::Avalonia.Media.FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
        };

        Control editor = field.Type switch
        {
            FieldType.MultiLineText => CreateMultiLineTextBox(field, selectedItem),
            FieldType.Number        => CreateNumericTextBox(field, selectedItem),
            FieldType.Tags          => CreateTagsTextBox(field, selectedItem),
            FieldType.Enum          => CreateEnumComboBox(field, selectedItem),
            FieldType.Boolean       => CreateBooleanCheckBox(field, selectedItem),
            _                       => CreateSingleLineTextBox(field, selectedItem),
        };

        return new StackPanel
        {
            Spacing = 2,
            Children = { label, editor }
        };
    }

    private static TextBox CreateSingleLineTextBox(FieldDescriptor field, object? item)
    {
        var tb = new TextBox { Watermark = field.Placeholder ?? string.Empty };
        BindProperty(tb, TextBox.TextProperty, field.PropertyName, item, converter: null);
        return tb;
    }

    private static TextBox CreateMultiLineTextBox(FieldDescriptor field, object? item)
    {
        var tb = new TextBox
        {
            Watermark = field.Placeholder ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = global::Avalonia.Media.TextWrapping.Wrap,
            MinHeight = 80,
            MaxHeight = 200,
        };
        BindProperty(tb, TextBox.TextProperty, field.PropertyName, item, converter: null);
        return tb;
    }

    private static TextBox CreateNumericTextBox(FieldDescriptor field, object? item)
    {
        // 简化：用 TextBox + 数字格式。NumericUpDown 在 Avalonia 11 行为略不一致，先 TextBox 凑合。
        var tb = new TextBox { Watermark = field.Placeholder ?? "0" };
        BindProperty(tb, TextBox.TextProperty, field.PropertyName, item, converter: null);
        return tb;
    }

    private static TextBox CreateTagsTextBox(FieldDescriptor field, object? item)
    {
        var tb = new TextBox { Watermark = field.Placeholder ?? "逗号分隔" };
        BindProperty(tb, TextBox.TextProperty, field.PropertyName, item, converter: TagsListStringConverter.Instance);
        return tb;
    }

    private static ComboBox CreateEnumComboBox(FieldDescriptor field, object? item)
    {
        var cb = new ComboBox
        {
            ItemsSource = field.EnumOptions?.ToList() ?? new List<string>(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        BindProperty(cb, ComboBox.SelectedItemProperty, field.PropertyName, item, converter: null);
        return cb;
    }

    private static CheckBox CreateBooleanCheckBox(FieldDescriptor field, object? item)
    {
        var cbox = new CheckBox { Content = field.Label };
        BindProperty(cbox, ToggleButton.IsCheckedProperty, field.PropertyName, item, converter: null);
        return cbox;
    }

    private static void BindProperty(
        Control control,
        AvaloniaProperty targetProperty,
        string sourcePropertyName,
        object? source,
        IValueConverter? converter)
    {
        if (source == null) return;
        var prop = source.GetType().GetProperty(sourcePropertyName);
        if (prop == null) return;

        // 用 Binding：DataContext = SelectedItem，Path = PropertyName
        // Avalonia 11 TwoWay 对 TextBox 默认在 LostFocus 时 update source；对 ComboBox SelectedItem 在变更时立即 update。
        control.DataContext = source;
        var binding = new Binding(sourcePropertyName)
        {
            Mode = BindingMode.TwoWay,
            Converter = converter,
        };
        control.Bind(targetProperty, binding);
    }
}
