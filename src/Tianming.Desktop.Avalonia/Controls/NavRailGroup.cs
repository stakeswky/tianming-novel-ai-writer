using System.Collections.Generic;

namespace Tianming.Desktop.Avalonia.Controls;

public sealed record NavRailGroup(string Title, IReadOnlyList<NavRailItem> Items);
