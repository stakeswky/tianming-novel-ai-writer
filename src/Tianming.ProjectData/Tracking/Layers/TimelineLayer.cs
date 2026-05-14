using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Tracking.Layers;

public sealed class TimelineLayer : IConsistencyLayer
{
    public string LayerName => "Timeline";

    public Task<IReadOnlyList<ConsistencyIssue>> CheckAsync(
        ChapterChanges changes,
        FactSnapshot factSnapshot,
        LedgerRuleSet ruleSet,
        CancellationToken ct = default)
    {
        var issues = new List<ConsistencyIssue>();
        if (changes == null)
            return Task.FromResult<IReadOnlyList<ConsistencyIssue>>(issues);

        factSnapshot ??= new FactSnapshot();
        ruleSet ??= LedgerRuleSet.CreateUniversalDefault();

        ValidateConflictConsistency(changes, factSnapshot, issues, ruleSet);

        var chapterLocations = new HashSet<string>(
            factSnapshot.LocationDescriptions?.Keys ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        ValidateMovementStartLocations(changes, factSnapshot, issues, chapterLocations);
        return Task.FromResult<IReadOnlyList<ConsistencyIssue>>(issues);
    }

    private void ValidateConflictConsistency(
        ChapterChanges changes,
        FactSnapshot factSnapshot,
        List<ConsistencyIssue> issues,
        LedgerRuleSet ruleSet)
    {
        if (!ruleSet.EnableConflictFlowCheck || ruleSet.ConflictStatusSequence.Count < 2)
            return;

        if (changes.ConflictProgress == null || changes.ConflictProgress.Count == 0)
            return;

        var statusIndexMap = BuildStatusIndexMap(ruleSet.ConflictStatusSequence);
        foreach (var progress in changes.ConflictProgress)
        {
            if (string.IsNullOrWhiteSpace(progress.ConflictId))
                continue;

            var existing = factSnapshot.ConflictProgress
                .FirstOrDefault(conflict => conflict.Id == progress.ConflictId);
            if (existing == null)
                continue;

            var currentStatus = NormalizeConflictStatus(existing.Status);
            var newStatus = NormalizeConflictStatus(progress.NewStatus);
            if (string.IsNullOrWhiteSpace(currentStatus)
                || string.IsNullOrWhiteSpace(newStatus)
                || currentStatus == newStatus)
                continue;

            if (!statusIndexMap.TryGetValue(currentStatus, out var oldIndex)
                || !statusIndexMap.TryGetValue(newStatus, out var newIndex))
                continue;

            if (newIndex < oldIndex)
            {
                issues.Add(NewIssue(
                    entityId: progress.ConflictId,
                    issueType: "ConflictStatusSkip",
                    expected: $"冲突状态不可回退（{currentStatus} → {newStatus}）",
                    actual: $"尝试回退到 {newStatus}"));
            }
        }
    }

    private void ValidateMovementStartLocations(
        ChapterChanges changes,
        FactSnapshot factSnapshot,
        List<ConsistencyIssue> issues,
        HashSet<string>? chapterLocations)
    {
        if (changes.CharacterMovements == null || changes.CharacterMovements.Count == 0)
            return;

        if (factSnapshot.CharacterLocations == null || factSnapshot.CharacterLocations.Count == 0)
            return;

        var rollingLocationMap = factSnapshot.CharacterLocations
            .Where(location =>
                !string.IsNullOrWhiteSpace(location.CharacterId) && !string.IsNullOrWhiteSpace(location.CurrentLocation))
            .ToDictionary(location => location.CharacterId, location => location.CurrentLocation, StringComparer.OrdinalIgnoreCase);

        foreach (var move in changes.CharacterMovements
                     .Where(m => !string.IsNullOrWhiteSpace(m.CharacterId) && !string.IsNullOrWhiteSpace(m.FromLocation)))
        {
            if (rollingLocationMap.TryGetValue(move.CharacterId, out var knownLocation)
                && !string.IsNullOrWhiteSpace(knownLocation)
                && !string.Equals(knownLocation, move.FromLocation, StringComparison.OrdinalIgnoreCase))
            {
                if (chapterLocations != null && chapterLocations.Contains(move.FromLocation))
                {
                    if (!string.IsNullOrWhiteSpace(move.ToLocation))
                        rollingLocationMap[move.CharacterId] = move.ToLocation;
                    continue;
                }

                issues.Add(NewIssue(
                    entityId: move.CharacterId,
                    issueType: "MovementStartLocationMismatch",
                    expected: $"从已知位置 {knownLocation} 出发",
                    actual: $"CHANGES声称从 {move.FromLocation} 出发（账本位置不符）"));
            }

            if (!string.IsNullOrWhiteSpace(move.ToLocation))
                rollingLocationMap[move.CharacterId] = move.ToLocation;
        }
    }

    private static Dictionary<string, int> BuildStatusIndexMap(IReadOnlyList<string> sequence)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (sequence == null || sequence.Count < 2)
            return map;

        foreach (var item in sequence)
        {
            var normalized = NormalizeConflictStatus(item);
            if (!string.IsNullOrWhiteSpace(normalized) && !map.ContainsKey(normalized))
                map[normalized] = map.Count;
        }

        return map;
    }

    private static string NormalizeConflictStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return string.Empty;

        var text = status.Trim();
        var lower = text.ToLowerInvariant();

        if (lower is "pending" or "todo" or "new" or "open")
            return "pending";
        if (lower is "active" or "ongoing" or "running" or "inprogress" or "in_progress")
            return "active";
        if (lower is "climax" or "peak" or "burst" or "explosion")
            return "climax";
        if (lower is "resolved" or "done" or "closed" or "finished" or "complete" or "completed")
            return "resolved";

        if (text.Contains("未") && (text.Contains("开始") || text.Contains("触发") || text.Contains("启动")))
            return "pending";
        if (text.Contains("进行") || text.Contains("推进") || text.Contains("发展") || text.Contains("展开") || text.Contains("激活") || text.Contains("触发") || text.Contains("开启"))
            return "active";
        if (text.Contains("高潮") || text.Contains("爆发") || text.Contains("决战") || text.Contains("决裂") || text.Contains("临界"))
            return "climax";
        if (text.Contains("解决") || text.Contains("结束") || text.Contains("完结") || text.Contains("收束") || text.Contains("已结") || text.Contains("闭环"))
            return "resolved";

        return lower;
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
