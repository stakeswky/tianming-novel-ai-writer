using Tianming.Desktop.Avalonia.Navigation;

namespace Tianming.Desktop.Avalonia.Shell;

public sealed record BreadcrumbSegment(string Label, PageKey? Target);
