using Tianming.Desktop.Avalonia.Navigation;

namespace Tianming.Desktop.Avalonia.Controls;

public sealed record NavRailItem(PageKey Key, string Label, string IconGlyph, bool IsEnabled = true);
