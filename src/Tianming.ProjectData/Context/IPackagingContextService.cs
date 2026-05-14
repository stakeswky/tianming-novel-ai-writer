using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Context;

public interface IPackagingContextService
{
    Task<PackagingSnapshot> BuildSnapshotAsync(CancellationToken ct = default);
}

public sealed class PackagingSnapshot
{
    public IReadOnlyList<DesignReference> AllDesignReferences { get; set; } = new List<DesignReference>();

    public IReadOnlyList<string> ChapterIds { get; set; } = new List<string>();

    public DateTime CapturedAt { get; set; } = DateTime.UtcNow;

    public string ProjectRoot { get; set; } = string.Empty;
}
