using System;
using System.Globalization;
using Avalonia.Data.Converters;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tianming.Desktop.Avalonia.ViewModels.Conversation;

public partial class PlanStepVm : ObservableObject
{
    [ObservableProperty] private int _index;
    [ObservableProperty] private string _title = string.Empty;
    [ObservableProperty] private string _detail = string.Empty;
    [ObservableProperty] private PlanStepStatus _status = PlanStepStatus.Pending;
    [ObservableProperty] private bool _isExpanded;
}

public enum PlanStepStatus
{
    Pending,
    Running,
    Done,
    Failed
}

public sealed class PlanStepStatusIconConverter : IValueConverter
{
    public static readonly PlanStepStatusIconConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            PlanStepStatus.Pending => "○",
            PlanStepStatus.Running => "◉",
            PlanStepStatus.Done => "✓",
            PlanStepStatus.Failed => "✗",
            _ => "○",
        };

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
