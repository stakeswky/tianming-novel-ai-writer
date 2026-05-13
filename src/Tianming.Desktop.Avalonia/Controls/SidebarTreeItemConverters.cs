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
