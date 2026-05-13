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

/// <summary>int ↔ string 双向转换器（Number 字段 TextBox 用）。
/// 设计：0 显示为空串；非法字符串回退到 0 不抛。</summary>
public sealed class NumberStringConverter : IValueConverter
{
    public static readonly NumberStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;
        if (value is int i) return i == 0 ? string.Empty : i.ToString(culture);
        if (value is long l) return l == 0 ? string.Empty : l.ToString(culture);
        return value.ToString() ?? string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s)) return 0;
        return int.TryParse(s, NumberStyles.Integer, culture, out var n) ? n : 0;
    }
}
