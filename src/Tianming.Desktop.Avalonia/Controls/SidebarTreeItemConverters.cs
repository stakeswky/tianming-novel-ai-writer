using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Controls;

internal sealed class DepthToWidthConverter : IValueConverter
{
    public static readonly DepthToWidthConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int d ? d * 16.0 : 0.0;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed class ExpandedToChevronConverter : IValueConverter
{
    public static readonly ExpandedToChevronConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? "▾" : "▸";
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed class BadgeKindFallbackConverter : IValueConverter
{
    public static readonly BadgeKindFallbackConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is StatusKind k ? k : StatusKind.Neutral;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed class TimestampShortConverter : IValueConverter
{
    public static readonly TimestampShortConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is DateTime dt && dt != default ? dt.ToString("HH:mm", culture) : string.Empty;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed class ToolCallStateLabelConverter : IValueConverter
{
    public static readonly ToolCallStateLabelConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            ToolCallState.Pending  => "待确认",
            ToolCallState.Applied  => "已应用",
            ToolCallState.Rejected => "已拒绝",
            _ => string.Empty
        };
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

internal sealed class ToolCallStateKindConverter : IValueConverter
{
    public static readonly ToolCallStateKindConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            ToolCallState.Pending  => StatusKind.Warning,
            ToolCallState.Applied  => StatusKind.Success,
            ToolCallState.Rejected => StatusKind.Danger,
            _ => StatusKind.Neutral
        };
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>IconGlyph 判定 — 长度 ≤ 2 视为已经渲染好的字符（emoji / Unicode 符号），>2 视为 Lucide 名（未渲染）隐藏。</summary>
internal sealed class IconGlyphIsShortConverter : IValueConverter
{
    public static readonly IconGlyphIsShortConverter Instance = new();
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string s && !string.IsNullOrEmpty(s) && s.Length <= 2;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
