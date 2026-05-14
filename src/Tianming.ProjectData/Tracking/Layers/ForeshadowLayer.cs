using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Tracking.Layers;

public sealed class ForeshadowLayer : IConsistencyLayer
{
    public string LayerName => "Foreshadow";

    public Task<IReadOnlyList<ConsistencyIssue>> CheckAsync(
        ChapterChanges changes,
        FactSnapshot factSnapshot,
        LedgerRuleSet ruleSet,
        CancellationToken ct = default)
    {
        var issues = new List<ConsistencyIssue>();
        if (changes?.ForeshadowingActions == null || changes.ForeshadowingActions.Count == 0)
            return Task.FromResult<IReadOnlyList<ConsistencyIssue>>(issues);

        factSnapshot ??= new FactSnapshot();

        foreach (var action in changes.ForeshadowingActions)
        {
            if (string.IsNullOrEmpty(action.ForeshadowId))
                continue;

            var existing = factSnapshot.ForeshadowingStatus
                .FirstOrDefault(item => item.Id == action.ForeshadowId);
            if (existing == null)
                continue;

            var actionLower = action.Action?.ToLowerInvariant() ?? string.Empty;

            if (existing.IsResolved && actionLower == "setup")
            {
                issues.Add(NewIssue(
                    entityId: action.ForeshadowId,
                    issueType: "ForeshadowingRollback",
                    expected: "已揭示状态不可回退",
                    actual: "尝试重新埋设"));
            }

            if (actionLower == "payoff" && !existing.IsSetup)
            {
                issues.Add(NewIssue(
                    entityId: action.ForeshadowId,
                    issueType: "PayoffBeforeSetup",
                    expected: "先埋设后揭示",
                    actual: "未埋设即揭示"));
            }
        }

        return Task.FromResult<IReadOnlyList<ConsistencyIssue>>(issues);
    }

    private ConsistencyIssue NewIssue(string entityId, string issueType, string expected, string actual)
    {
        return new ConsistencyIssue
        {
            Layer = LayerName,
            EntityId = entityId,
            IssueType = issueType,
            Expected = expected,
            Actual = actual
        };
    }
}
