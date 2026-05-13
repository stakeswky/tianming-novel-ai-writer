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
using Avalonia.Media;
using Avalonia.Threading;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.Controls;
using Tianming.Desktop.Avalonia.ViewModels.Generate;

namespace Tianming.Desktop.Avalonia.Views.Generate;

public partial class ChapterPlanningPage : UserControl
{
    private StackPanel? _formHost;

    public ChapterPlanningPage()
    {
        AvaloniaXamlLoader.Load(this);
        _formHost = this.FindControl<StackPanel>("FormHost");
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ChapterPlanningViewModel vm)
        {
            vm.ChapterStatusChanged += OnStatusChanged;
            vm.PropertyChanged += OnVmPropertyChanged;
            RebuildForm();
        }
    }

    private void OnStatusChanged()
    {
        if (DataContext is not ChapterPlanningViewModel vm) return;
        Dispatcher.UIThread.Post(() =>
        {
            var snapshot = vm.Items.ToList();
            vm.Items.Clear();
            foreach (var item in snapshot)
                vm.Items.Add(item);
        });
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
            FontWeight = FontWeight.SemiBold,
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
        BindProperty(tb, TextBox.TextProperty, field.PropertyName, item);
        return tb;
    }

    private static TextBox CreateMultiLineTextBox(FieldDescriptor field, object? item)
    {
        var tb = new TextBox
        {
            Watermark = field.Placeholder ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 80,
            MaxHeight = 200,
        };
        BindProperty(tb, TextBox.TextProperty, field.PropertyName, item);
        return tb;
    }

    private static TextBox CreateNumericTextBox(FieldDescriptor field, object? item)
    {
        var tb = new TextBox { Watermark = field.Placeholder ?? "0" };
        BindProperty(tb, TextBox.TextProperty, field.PropertyName, item, converter: NumberStringConverter.Instance);
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
        BindProperty(cb, ComboBox.SelectedItemProperty, field.PropertyName, item);
        return cb;
    }

    private static CheckBox CreateBooleanCheckBox(FieldDescriptor field, object? item)
    {
        var cbox = new CheckBox { Content = field.Label };
        BindProperty(cbox, ToggleButton.IsCheckedProperty, field.PropertyName, item);
        return cbox;
    }

    private static void BindProperty(
        Control control,
        AvaloniaProperty targetProperty,
        string sourcePropertyName,
        object? source,
        IValueConverter? converter = null)
    {
        if (source == null) return;
        var prop = source.GetType().GetProperty(sourcePropertyName);
        if (prop == null) return;

        control.DataContext = source;
        var binding = new Binding(sourcePropertyName)
        {
            Mode = BindingMode.TwoWay,
            Converter = converter,
        };
        control.Bind(targetProperty, binding);
    }
}
