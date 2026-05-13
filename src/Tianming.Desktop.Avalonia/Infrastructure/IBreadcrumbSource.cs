using System;
using System.Collections.Generic;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public interface IBreadcrumbSource
{
    IReadOnlyList<BreadcrumbSegment> Current { get; }
    event EventHandler<IReadOnlyList<BreadcrumbSegment>>? SegmentsChanged;
}
