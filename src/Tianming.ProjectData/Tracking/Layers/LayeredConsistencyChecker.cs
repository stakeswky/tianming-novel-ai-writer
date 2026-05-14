using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Tracking.Layers;

public sealed class LayeredConsistencyChecker
{
    private readonly IReadOnlyList<IConsistencyLayer> _layers;

    public LayeredConsistencyChecker(IEnumerable<IConsistencyLayer> layers)
    {
        _layers = new List<IConsistencyLayer>(layers);
    }

    public async Task<LayeredConsistencyResult> CheckAsync(
        ChapterChanges changes,
        FactSnapshot factSnapshot,
        LedgerRuleSet ruleSet,
        CancellationToken ct = default)
    {
        var issuesByLayer = new Dictionary<string, IReadOnlyList<ConsistencyIssue>>();
        foreach (var layer in _layers)
        {
            var issues = await layer.CheckAsync(changes, factSnapshot, ruleSet, ct).ConfigureAwait(false);
            issuesByLayer[layer.LayerName] = issues;
        }

        return new LayeredConsistencyResult
        {
            IssuesByLayer = issuesByLayer
        };
    }
}
