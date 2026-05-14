using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Context;

public sealed class PackagingContextService : IPackagingContextService
{
    private static readonly string[] Categories =
    [
        "Characters",
        "WorldRules",
        "Factions",
        "Locations",
        "Plot",
        "CreativeMaterials",
    ];

    private readonly string _projectRoot;
    private readonly IDesignContextService _design;

    public PackagingContextService(string projectRoot, IDesignContextService design)
    {
        _projectRoot = projectRoot;
        _design = design;
    }

    public async Task<PackagingSnapshot> BuildSnapshotAsync(CancellationToken ct = default)
    {
        var designs = new List<DesignReference>();
        foreach (var category in Categories)
        {
            designs.AddRange(await _design.ListByCategoryAsync(category, ct).ConfigureAwait(false));
        }

        var chaptersDir = Path.Combine(_projectRoot, "Generate", "Chapters");
        var chapterIds = Directory.Exists(chaptersDir)
            ? Directory.GetFiles(chaptersDir, "*.md", SearchOption.AllDirectories)
                .Select(Path.GetFileNameWithoutExtension)
                .OfType<string>()
                .Where(name => !string.IsNullOrEmpty(name))
                .ToList()
            : [];

        return new PackagingSnapshot
        {
            AllDesignReferences = designs,
            ChapterIds = chapterIds,
            ProjectRoot = _projectRoot,
        };
    }
}
