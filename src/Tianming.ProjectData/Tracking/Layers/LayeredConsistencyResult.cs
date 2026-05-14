using System.Collections.Generic;
using System.Linq;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Tracking.Layers;

public sealed class LayeredConsistencyResult
{
    public IReadOnlyDictionary<string, IReadOnlyList<ConsistencyIssue>> IssuesByLayer { get; init; }
        = new Dictionary<string, IReadOnlyList<ConsistencyIssue>>();

    public IReadOnlyList<string> LayerNames => IssuesByLayer.Keys.ToList();

    public IReadOnlyList<ConsistencyIssue> AllIssues => IssuesByLayer.Values.SelectMany(issues => issues).ToList();

    public bool Success => AllIssues.Count == 0;
}
