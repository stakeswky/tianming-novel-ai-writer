namespace Tianming.Desktop.Avalonia.Shell;

public enum StatusKind { Success, Warning, Danger, Info, Neutral }

public sealed record StatusIndicator(string Label, StatusKind Kind, string? Tooltip = null);
