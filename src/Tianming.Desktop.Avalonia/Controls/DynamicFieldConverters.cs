using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.Controls;

/// <summary>List&lt;string&gt; ↔ "a, b, c" 双向转换（Tags 字段）。</summary>
public sealed class TagsListStringConverter : IValueConverter
{
    public static readonly TagsListStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is IEnumerable<string> list ? string.Join(", ", list) : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
            return new List<string>();
        return s.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}

/// <summary>FieldType 等值匹配 — 用于 DataTrigger / Visibility。</summary>
public sealed class FieldTypeEqualsConverter : IValueConverter
{
    public static readonly FieldTypeEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is FieldType t && parameter is string s && Enum.TryParse<FieldType>(s, out var p) && t == p;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
