using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Tracking.Layers;

public sealed class EntityLayer : IConsistencyLayer
{
    public string LayerName => "Entity";

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

        ValidateCharacterConsistency(changes, factSnapshot, issues, ruleSet);

        var chapterCharacters = new HashSet<string>(
            factSnapshot.CharacterDescriptions?.Keys ?? Enumerable.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        ValidateItemOwnershipConsistency(changes, factSnapshot, issues, chapterCharacters);
        return Task.FromResult<IReadOnlyList<ConsistencyIssue>>(issues);
    }

    private void ValidateCharacterConsistency(
        ChapterChanges changes,
        FactSnapshot factSnapshot,
        List<ConsistencyIssue> issues,
        LedgerRuleSet ruleSet)
    {
        if (changes.CharacterStateChanges == null || changes.CharacterStateChanges.Count == 0)
            return;

        var involvedCharacterIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var states = factSnapshot.CharacterStates;

        if (states == null)
        {
            foreach (var change in changes.CharacterStateChanges.Where(c => !string.IsNullOrWhiteSpace(c.CharacterId)))
            {
                issues.Add(NewIssue(
                    entityId: change.CharacterId,
                    issueType: "CharacterNotInvolved",
                    expected: "本章涉及角色列表应可用（FactSnapshot.CharacterStates 不应为 null）",
                    actual: "FactSnapshot.CharacterStates 为 null"));
            }

            return;
        }

        foreach (var state in states)
        {
            if (!string.IsNullOrWhiteSpace(state.Id))
                involvedCharacterIds.Add(state.Id);
        }

        if (factSnapshot.CharacterDescriptions != null)
        {
            foreach (var pair in factSnapshot.CharacterDescriptions)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key))
                    involvedCharacterIds.Add(pair.Key);
            }
        }

        if (involvedCharacterIds.Count == 0)
        {
            foreach (var change in changes.CharacterStateChanges.Where(c => !string.IsNullOrWhiteSpace(c.CharacterId)))
            {
                issues.Add(NewIssue(
                    entityId: change.CharacterId,
                    issueType: "CharacterNotInvolved",
                    expected: "角色必须在章节上下文中定义",
                    actual: "上下文角色集合为空"));
            }

            return;
        }

        foreach (var change in changes.CharacterStateChanges)
        {
            if (string.IsNullOrWhiteSpace(change.CharacterId))
                continue;

            if (!involvedCharacterIds.Contains(change.CharacterId))
            {
                issues.Add(NewIssue(
                    entityId: change.CharacterId,
                    issueType: "CharacterNotInvolved",
                    expected: "CHANGES中的角色应在本章涉及角色列表中",
                    actual: $"角色 {change.CharacterId} 不在本章涉及角色中"));
            }

            var existing = states.FirstOrDefault(state =>
                string.Equals(state.Id, change.CharacterId, StringComparison.OrdinalIgnoreCase));
            if (existing == null)
                continue;

            if (!string.IsNullOrEmpty(change.NewLevel)
                && !string.IsNullOrEmpty(existing.Stage)
                && ruleSet.EnableLevelRegressionCheck)
            {
                var oldLevel = ParseLevelNumber(existing.Stage, ruleSet.LevelTextMap);
                var newLevel = ParseLevelNumber(change.NewLevel, ruleSet.LevelTextMap);

                if (oldLevel >= 0 && newLevel >= 0 && newLevel < oldLevel)
                {
                    var keywords = ruleSet.AbilityLossKeywords ?? new List<string>();
                    var hasLossEvent = !string.IsNullOrEmpty(change.KeyEvent)
                        && (keywords.Count == 0 || keywords.Any(keyword =>
                            !string.IsNullOrWhiteSpace(keyword)
                            && change.KeyEvent.Contains(keyword, StringComparison.OrdinalIgnoreCase)));

                    if (!hasLossEvent)
                    {
                        issues.Add(NewIssue(
                            entityId: change.CharacterId,
                            issueType: "LevelRegression",
                            expected: "等级只升不降（除非有明确失去事件）",
                            actual: $"{existing.Stage} → {change.NewLevel}（无失去事件说明）"));
                    }
                }
            }

            if (change.LostAbilities != null
                && change.LostAbilities.Count > 0
                && ruleSet.EnableAbilityLossRequiresEvent)
            {
                var lossKeywords = ruleSet.AbilityLossKeywords ?? new List<string>();
                var hasLossEvent = !string.IsNullOrEmpty(change.KeyEvent)
                    && lossKeywords.Any(keyword =>
                        !string.IsNullOrWhiteSpace(keyword)
                        && change.KeyEvent.Contains(keyword, StringComparison.OrdinalIgnoreCase));

                if (!hasLossEvent)
                {
                    issues.Add(NewIssue(
                        entityId: change.CharacterId,
                        issueType: "AbilityLossWithoutEvent",
                        expected: "能力失去必须有对应的KeyEvent说明原因",
                        actual: $"失去能力 [{string.Join(", ", change.LostAbilities)}] 但KeyEvent未说明原因"));
                }
            }
        }
    }

    private void ValidateItemOwnershipConsistency(
        ChapterChanges changes,
        FactSnapshot factSnapshot,
        List<ConsistencyIssue> issues,
        HashSet<string>? chapterCharacters)
    {
        if (changes.ItemTransfers == null || changes.ItemTransfers.Count == 0)
            return;

        var rollingHolderMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (factSnapshot.ItemStates != null)
        {
            foreach (var item in factSnapshot.ItemStates.Where(i =>
                         !string.IsNullOrWhiteSpace(i.Id) && !string.IsNullOrWhiteSpace(i.CurrentHolder)))
            {
                rollingHolderMap[item.Id] = item.CurrentHolder;
            }
        }

        foreach (var transfer in changes.ItemTransfers.Where(t =>
                     !string.IsNullOrWhiteSpace(t.ItemId) && !string.IsNullOrWhiteSpace(t.FromHolder)))
        {
            if (!rollingHolderMap.ContainsKey(transfer.ItemId))
            {
                rollingHolderMap[transfer.ItemId] = transfer.FromHolder;
            }
            else if (rollingHolderMap.TryGetValue(transfer.ItemId, out var knownHolder)
                     && !string.Equals(knownHolder, transfer.FromHolder, StringComparison.OrdinalIgnoreCase))
            {
                if (chapterCharacters != null && chapterCharacters.Contains(transfer.FromHolder))
                {
                    rollingHolderMap[transfer.ItemId] = transfer.ToHolder ?? string.Empty;
                    continue;
                }

                var holderLabel = string.IsNullOrWhiteSpace(knownHolder) ? "无人持有" : knownHolder;
                issues.Add(NewIssue(
                    entityId: transfer.ItemId,
                    issueType: "ItemOwnershipMismatch",
                    expected: $"物品由当前持有者 {holderLabel} 转让",
                    actual: $"CHANGES声称由 {transfer.FromHolder} 转让（持有者不符）"));
            }

            rollingHolderMap[transfer.ItemId] = transfer.ToHolder ?? string.Empty;
        }
    }

    private static int ParseLevelNumber(string level, Dictionary<string, int> levelMap)
    {
        if (string.IsNullOrWhiteSpace(level))
            return -1;

        var text = level.Trim();

        var tierMatch = Regex.Match(text, @"tier\s*[-_]?\s*(\d+)", RegexOptions.IgnoreCase);
        if (tierMatch.Success && int.TryParse(tierMatch.Groups[1].Value, out var tierNum))
        {
            if (levelMap != null && levelMap.TryGetValue($"Tier-{tierNum}", out var tierValue))
                return tierValue;
            return tierNum;
        }

        var match = Regex.Match(text, @"\d+");
        if (match.Success && int.TryParse(match.Value, out var number))
            return number;

        var romanMatch = Regex.Match(text, @"\b[IVXLCDM]+\b", RegexOptions.IgnoreCase);
        if (romanMatch.Success && TryParseRomanNumeral(romanMatch.Value, out var romanNumber))
            return romanNumber;

        var chineseMatch = Regex.Match(text, @"[零一二三四五六七八九十百千万两]+", RegexOptions.IgnoreCase);
        if (chineseMatch.Success && TryParseChineseNumber(chineseMatch.Value, out var chineseNumber))
            return chineseNumber;

        var gradeMatch = Regex.Match(
            text,
            @"\b(SSS|SS|S|A|B|C|D|E|F)\b|(?i)(SSS|SS|S|A|B|C|D|E|F)\s*(级|阶|段)",
            RegexOptions.IgnoreCase);
        if (gradeMatch.Success)
        {
            var grade = (gradeMatch.Groups[1].Success ? gradeMatch.Groups[1].Value : gradeMatch.Groups[2].Value)
                .ToUpperInvariant();
            if (!string.IsNullOrWhiteSpace(grade) && levelMap.TryGetValue(grade, out var gradeValue))
                return gradeValue;
        }

        foreach (var pair in levelMap)
        {
            if (level.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        }

        return -1;
    }

    private static bool TryParseRomanNumeral(string text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var total = 0;
        var previous = 0;
        foreach (var character in text.Trim().ToUpperInvariant())
        {
            var current = character switch
            {
                'I' => 1,
                'V' => 5,
                'X' => 10,
                'L' => 50,
                'C' => 100,
                'D' => 500,
                'M' => 1000,
                _ => 0
            };

            if (current == 0)
            {
                value = 0;
                return false;
            }

            total += current > previous ? current - 2 * previous : current;
            previous = current;
        }

        value = total;
        return value > 0;
    }

    private static bool TryParseChineseNumber(string text, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var result = 0;
        var section = 0;
        var number = 0;

        foreach (var character in text.Trim())
        {
            var digit = character switch
            {
                '零' => 0,
                '一' => 1,
                '二' => 2,
                '两' => 2,
                '三' => 3,
                '四' => 4,
                '五' => 5,
                '六' => 6,
                '七' => 7,
                '八' => 8,
                '九' => 9,
                _ => -1
            };

            if (digit >= 0)
            {
                number = digit;
                continue;
            }

            var unit = character switch
            {
                '十' => 10,
                '百' => 100,
                '千' => 1000,
                '万' => 10000,
                _ => 0
            };

            if (unit == 0)
            {
                value = 0;
                return false;
            }

            if (unit == 10000)
            {
                section += number;
                result += section * 10000;
                section = 0;
                number = 0;
            }
            else
            {
                section += (number == 0 ? 1 : number) * unit;
                number = 0;
            }
        }

        value = result + section + number;
        return value > 0;
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
