using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Tracking.Layers;

public sealed class RelationshipLayer : IConsistencyLayer
{
    public string LayerName => "Relationship";

    public Task<IReadOnlyList<ConsistencyIssue>> CheckAsync(
        ChapterChanges changes,
        FactSnapshot factSnapshot,
        LedgerRuleSet ruleSet,
        CancellationToken ct = default)
    {
        var issues = new List<ConsistencyIssue>();
        if (changes == null)
            return Task.FromResult<IReadOnlyList<ConsistencyIssue>>(issues);

        ruleSet ??= LedgerRuleSet.CreateUniversalDefault();

        ValidateTrustDeltas(changes, issues, ruleSet);
        ValidateRelationshipContradictions(changes, issues);
        return Task.FromResult<IReadOnlyList<ConsistencyIssue>>(issues);
    }

    private void ValidateTrustDeltas(ChapterChanges changes, List<ConsistencyIssue> issues, LedgerRuleSet ruleSet)
    {
        if (changes.CharacterStateChanges == null || changes.CharacterStateChanges.Count == 0)
            return;

        foreach (var change in changes.CharacterStateChanges)
        {
            if (string.IsNullOrWhiteSpace(change.CharacterId) || change.RelationshipChanges == null)
                continue;

            foreach (var (targetId, relationshipChange) in change.RelationshipChanges)
            {
                if (Math.Abs(relationshipChange.TrustDelta) > ruleSet.MaxTrustDelta)
                {
                    issues.Add(NewIssue(
                        entityId: change.CharacterId,
                        issueType: "TrustDeltaExceedsLimit",
                        expected: $"单章信任值变化不超过±{ruleSet.MaxTrustDelta}",
                        actual: $"与 {targetId} 的信任值变化为 {relationshipChange.TrustDelta}"));
                }
            }
        }
    }

    private void ValidateRelationshipContradictions(ChapterChanges changes, List<ConsistencyIssue> issues)
    {
        if (changes.CharacterStateChanges == null || changes.CharacterStateChanges.Count == 0)
            return;

        var allyPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var enemyPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var change in changes.CharacterStateChanges.Where(c => !string.IsNullOrWhiteSpace(c.CharacterId)))
        {
            foreach (var pair in change.RelationshipChanges ?? new Dictionary<string, RelationshipChange>())
            {
                var targetId = pair.Key;
                if (string.IsNullOrWhiteSpace(targetId))
                    continue;

                var relation = pair.Value?.Relation ?? string.Empty;
                var pairKey = string.Compare(change.CharacterId, targetId, StringComparison.OrdinalIgnoreCase) <= 0
                    ? $"{change.CharacterId}|{targetId}"
                    : $"{targetId}|{change.CharacterId}";

                if (IsAllyRelation(relation))
                    allyPairs.Add(pairKey);
                if (IsEnemyRelation(relation))
                    enemyPairs.Add(pairKey);
            }
        }

        foreach (var pair in allyPairs.Where(enemyPairs.Contains))
        {
            var ids = pair.Split('|');
            issues.Add(NewIssue(
                entityId: ids[0],
                issueType: "RelationshipContradiction",
                expected: "同一章节角色关系申报一致",
                actual: $"角色 {ids[0]} 与 {ids[1]} 在本章同时被声明为盟友和仇敌（关系矛盾）"));
        }
    }

    private static bool IsAllyRelation(string relation)
    {
        return !string.IsNullOrWhiteSpace(relation)
               && (relation.Contains("盟友")
                   || relation.Contains("同盟")
                   || relation.Contains("结盟")
                   || relation.Contains("ally", StringComparison.OrdinalIgnoreCase)
                   || relation.Contains("friend", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsEnemyRelation(string relation)
    {
        return !string.IsNullOrWhiteSpace(relation)
               && (relation.Contains("仇敌")
                   || relation.Contains("敌对")
                   || relation.Contains("宿敌")
                   || relation.Contains("enemy", StringComparison.OrdinalIgnoreCase)
                   || relation.Contains("hostile", StringComparison.OrdinalIgnoreCase));
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
