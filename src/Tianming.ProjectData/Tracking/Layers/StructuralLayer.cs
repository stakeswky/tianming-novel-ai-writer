using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Tracking.Layers;

/// <summary>
/// Chapter-shape validation only.
/// Legacy <c>ValidateStructuralOnly</c> parity is intentionally split across
/// <see cref="StructuralLayer"/>, <see cref="TimelineLayer"/>, and <see cref="RelationshipLayer"/>.
/// </summary>
public sealed class StructuralLayer : IConsistencyLayer
{
    public string LayerName => "Structural";

    public Task<IReadOnlyList<ConsistencyIssue>> CheckAsync(
        ChapterChanges changes,
        FactSnapshot factSnapshot,
        LedgerRuleSet ruleSet,
        CancellationToken ct = default)
    {
        var issues = new List<ConsistencyIssue>();
        if (changes == null)
        {
            issues.Add(NewIssue(
                entityId: string.Empty,
                issueType: "NullChanges",
                expected: "CHANGES对象不为空",
                actual: "CHANGES对象为空"));
            return Task.FromResult<IReadOnlyList<ConsistencyIssue>>(issues);
        }

        var movementsByCharacter = changes.CharacterMovements?
            .Where(m => !string.IsNullOrWhiteSpace(m.CharacterId))
            .GroupBy(m => m.CharacterId, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            ?? Enumerable.Empty<IGrouping<string, CharacterMovementChange>>();

        foreach (var group in movementsByCharacter)
        {
            var moves = group.ToList();
            for (var index = 1; index < moves.Count; index++)
            {
                var previous = moves[index - 1];
                var current = moves[index];
                if (!string.IsNullOrWhiteSpace(previous.ToLocation)
                    && !string.IsNullOrWhiteSpace(current.FromLocation)
                    && !string.Equals(previous.ToLocation, current.FromLocation, StringComparison.OrdinalIgnoreCase))
                {
                    issues.Add(NewIssue(
                        entityId: group.Key,
                        issueType: "MovementChainBreak",
                        expected: $"移动路径连续（到达{previous.ToLocation}后再从{previous.ToLocation}出发）",
                        actual: $"从{current.FromLocation}出发，但上次到达{previous.ToLocation}（路径断裂/同时在两地）"));
                }
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
