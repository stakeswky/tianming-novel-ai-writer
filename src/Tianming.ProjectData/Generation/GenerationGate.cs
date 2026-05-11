using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TM.Framework.Common.Helpers;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public class GenerationGate
    {
        private readonly LedgerConsistencyChecker _ledgerConsistencyChecker;
        private readonly LedgerRuleSetProvider _ledgerRuleSetProvider;
        private readonly ChangesProtocolParser _changesProtocolParser;

        public GenerationGate(LedgerConsistencyChecker ledgerConsistencyChecker, LedgerRuleSetProvider ledgerRuleSetProvider)
        {
            _ledgerConsistencyChecker = ledgerConsistencyChecker;
            _ledgerRuleSetProvider = ledgerRuleSetProvider;
            _changesProtocolParser = new ChangesProtocolParser();
        }

        public ConsistencyResult ValidateStructuralOnly(ChapterChanges changes)
            => _ledgerConsistencyChecker.ValidateStructuralOnly(changes);

        public Task<GateResult> ValidateAsync(
            string chapterId,
            string rawContent,
            FactSnapshot factSnapshot,
            DesignElementNames? designElements = null)
        {
            var result = new GateResult { ChapterId = chapterId };
            factSnapshot ??= new FactSnapshot();

            var protocolResult = _changesProtocolParser.ValidateChangesProtocol(rawContent);
            if (!protocolResult.Success)
            {
                result.AddFailure(FailureType.Protocol, protocolResult.Errors);
                return Task.FromResult(result);
            }

            result.ParsedChanges = protocolResult.Changes;
            result.ContentWithoutChanges = protocolResult.ContentWithoutChanges;

            var ruleSet = _ledgerRuleSetProvider.GetRuleSetForGate();
            var consistencyResult = _ledgerConsistencyChecker.Validate(
                protocolResult.Changes!,
                factSnapshot,
                ruleSet);

            if (!consistencyResult.Success)
            {
                result.AddFailure(FailureType.Consistency, consistencyResult.GetIssueDescriptions());
                return Task.FromResult(result);
            }

            var contentToValidate = protocolResult.ContentWithoutChanges ?? string.Empty;

            var entityExtractor = new ContentEntityExtractor(factSnapshot);
            var unknownEntities = entityExtractor.GetUnknownEntities(contentToValidate);
            if (unknownEntities.Count > 0)
            {
                var changes = protocolResult.Changes;
                var functionalEntities = unknownEntities
                    .Where(e => IsEntityInChanges(e, changes))
                    .ToList();
                var backgroundEntities = unknownEntities
                    .Where(e => !IsEntityInChanges(e, changes))
                    .ToList();

                if (unknownEntities.Count > 5 || backgroundEntities.Count > 3)
                {
                    var functionalSet = new HashSet<string>(functionalEntities, StringComparer.OrdinalIgnoreCase);
                    var failures = unknownEntities
                        .Select(e => functionalSet.Contains(e)
                            ? $"正文引入未登记实体(有剧情作用): {e}"
                            : $"正文引入未登记实体(龙套): {e}")
                        .ToList();
                    result.AddFailure(FailureType.Consistency, failures);
                    return Task.FromResult(result);
                }
            }

            if (factSnapshot.CharacterDescriptions.Count > 0 || factSnapshot.LocationDescriptions.Count > 0)
            {
                var descValidator = new ContentDescriptionValidator();
                var descIssues = new List<string>();
                descIssues.AddRange(descValidator.ValidateCharacterDescriptions(contentToValidate, factSnapshot.CharacterDescriptions));
                descIssues.AddRange(descValidator.ValidateLocationDescriptions(contentToValidate, factSnapshot.LocationDescriptions));
                if (descIssues.Count > 0)
                {
                    result.AddFailure(FailureType.Consistency, descIssues);
                    return Task.FromResult(result);
                }
            }

            if (factSnapshot.WorldRuleConstraints.Count > 0)
            {
                var ruleViolations = ValidateWorldRuleConstraints(contentToValidate, factSnapshot.WorldRuleConstraints);
                if (ruleViolations.Count > 0)
                {
                    result.AddFailure(FailureType.Consistency, ruleViolations);
                    return Task.FromResult(result);
                }
            }

            if (designElements != null)
            {
                var (_, designIssues, _) = ValidateDesignElementPresence(contentToValidate, designElements);
                var totalElements = designElements.CharacterNames.Count
                                  + designElements.FactionNames.Count
                                  + designElements.LocationNames.Count
                                  + designElements.PlotKeyNames.Count;
                var threshold = Math.Max(3, totalElements / 3);
                if (designIssues.Count > threshold)
                {
                    result.AddFailure(FailureType.Consistency, designIssues);
                    return Task.FromResult(result);
                }
            }

            result.Success = true;
            return Task.FromResult(result);
        }

        private static (List<string> povFailures, List<string> issues, List<string> qualityWarnings)
            ValidateDesignElementPresence(string content, DesignElementNames elements)
        {
            var povFailures = new List<string>();
            var issues = new List<string>();
            var qualityWarnings = new List<string>();

            foreach (var name in elements.PovCharacterNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var count = CountNameInContent(content, name);
                if (count == 0)
                    povFailures.Add($"视角角色未在正文出现: {name}");
                else if (count == 1)
                    qualityWarnings.Add($"视角角色'{name}'仅出现1次，叙事力度不足");
            }

            foreach (var name in elements.CharacterNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var count = CountNameInContent(content, name);
                if (count == 0)
                    issues.Add($"指定角色未在正文出现: {name}");
                else if (count == 1)
                    qualityWarnings.Add($"角色'{name}'仅出现1次，疑似背景一提");
            }

            foreach (var name in elements.FactionNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var count = CountNameInContent(content, name);
                if (count == 0)
                    issues.Add($"指定势力未在正文出现: {name}");
                else if (count == 1)
                    qualityWarnings.Add($"势力'{name}'仅出现1次，疑似背景一提");
            }

            foreach (var name in elements.LocationNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var count = CountNameInContent(content, name);
                if (count == 0)
                    issues.Add($"指定地点未在正文出现: {name}");
                else if (count == 1)
                    qualityWarnings.Add($"地点'{name}'仅出现1次，疑似背景一提");
            }

            foreach (var name in elements.PlotKeyNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                var count = CountNameInContent(content, name);
                if (count == 0)
                    issues.Add($"剧情关键角色未在正文出现: {name}");
                else if (count == 1)
                    qualityWarnings.Add($"剧情关键角色'{name}'仅出现1次，疑似背景一提");
            }

            return (povFailures, issues, qualityWarnings);
        }

        private static int CountNameInContent(string content, string fullName)
        {
            if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(fullName))
                return 0;

            var variants = new List<string>();
            void AddVariant(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return;
                var v = s.Trim();
                if (v.Length < 2) return;
                if (!variants.Contains(v)) variants.Add(v);
            }

            AddVariant(fullName);

            var primaryName = EntityNameNormalizeHelper.StripBracketAnnotation(fullName);
            if (!string.IsNullOrWhiteSpace(primaryName) && !string.Equals(primaryName, fullName, StringComparison.Ordinal))
                AddVariant(primaryName);

            var aliasMatches = System.Text.RegularExpressions.Regex.Matches(fullName, @"[\(（\[【](.+?)[\)）\]】]");
            foreach (System.Text.RegularExpressions.Match m in aliasMatches)
                AddVariant(m.Groups[1].Value);

            var maxCount = 0;
            foreach (var nameToCount in variants)
            {
                var count = 0;
                var idx = 0;
                while ((idx = content.IndexOf(nameToCount, idx, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    count++;
                    idx += nameToCount.Length;
                }
                if (count > maxCount) maxCount = count;
            }

            return maxCount;
        }

        private static bool IsEntityInChanges(string entityName, ChapterChanges? changes)
        {
            if (changes == null || string.IsNullOrWhiteSpace(entityName))
                return false;

            var name = entityName.Trim();

            foreach (var c in changes.CharacterStateChanges ?? new())
                if (ContainsEntityName(c.CharacterId, name) || ContainsEntityName(c.KeyEvent, name))
                    return true;

            foreach (var c in changes.ConflictProgress ?? new())
                if (ContainsEntityName(c.ConflictId, name) || ContainsEntityName(c.Event, name))
                    return true;

            foreach (var f in changes.ForeshadowingActions ?? new())
                if (ContainsEntityName(f.ForeshadowId, name))
                    return true;

            foreach (var p in changes.NewPlotPoints ?? new())
                if (ContainsEntityName(p.Context, name) ||
                    (p.InvolvedCharacters?.Any(ic => ContainsEntityName(ic, name)) == true))
                    return true;

            foreach (var l in changes.LocationStateChanges ?? new())
                if (ContainsEntityName(l.LocationId, name) || ContainsEntityName(l.Event, name))
                    return true;

            foreach (var fa in changes.FactionStateChanges ?? new())
                if (ContainsEntityName(fa.FactionId, name) || ContainsEntityName(fa.Event, name))
                    return true;

            foreach (var m in changes.CharacterMovements ?? new())
                if (ContainsEntityName(m.CharacterId, name))
                    return true;

            foreach (var it in changes.ItemTransfers ?? new())
                if (ContainsEntityName(it.ItemName, name) || ContainsEntityName(it.Event, name))
                    return true;

            return false;
        }

        private static bool ContainsEntityName(string? text, string entityName)
        {
            return !string.IsNullOrEmpty(text)
                && !string.IsNullOrEmpty(entityName)
                && text.Contains(entityName, StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> ValidateWorldRuleConstraints(string content, List<WorldRuleConstraint> constraints)
        {
            var violations = new List<string>();
            if (string.IsNullOrEmpty(content))
                return violations;

            foreach (var rule in constraints.Where(c => c.IsHardConstraint))
            {
                if (string.IsNullOrEmpty(rule.Constraint))
                    continue;

                var violation = CheckConstraintViolation(content, rule);
                if (violation != null)
                    violations.Add($"世界观硬约束违反 [{rule.RuleName}]: {violation}");
            }

            return violations;
        }

        private static string? CheckConstraintViolation(string content, WorldRuleConstraint rule)
        {
            var constraint = rule.Constraint;
            var negationPatterns = new[] { "不能", "不可", "禁止", "无法", "不得", "不会" };
            foreach (var negation in negationPatterns)
            {
                var idx = constraint.IndexOf(negation, StringComparison.Ordinal);
                if (idx < 0)
                    continue;

                var actionStart = idx + negation.Length;
                var actionLength = Math.Min(10, constraint.Length - actionStart);
                if (actionLength <= 0)
                    continue;

                var forbiddenAction = constraint.Substring(actionStart, actionLength).Trim();
                forbiddenAction = forbiddenAction.TrimEnd('，', '。', '、', '；');

                if (!string.IsNullOrEmpty(forbiddenAction) &&
                    forbiddenAction.Length >= 2 &&
                    HasViolatingOccurrence(content, forbiddenAction))
                {
                    return $"正文出现被禁止的内容「{forbiddenAction}」（约束：{constraint}）";
                }
            }

            return null;
        }

        private static bool HasViolatingOccurrence(string content, string forbiddenAction)
        {
            var negationPrefixes = new[] { "不能", "不可", "禁止", "无法", "不得", "不会", "别", "莫", "勿", "未曾", "从未", "并未", "没有", "岂能", "怎能", "哪能", "焉能" };

            var searchStart = 0;
            while (searchStart < content.Length)
            {
                var pos = content.IndexOf(forbiddenAction, searchStart, StringComparison.Ordinal);
                if (pos < 0) break;

                var contextStart = Math.Max(0, pos - 15);
                var prefix = content.Substring(contextStart, pos - contextStart);
                var sentBreak = prefix.LastIndexOfAny(new[] { '。', '！', '？', '；', '\n' });
                if (sentBreak >= 0) prefix = prefix[(sentBreak + 1)..];

                var isNegated = negationPrefixes.Any(neg => prefix.Contains(neg, StringComparison.Ordinal));
                if (!isNegated)
                    return true;

                searchStart = pos + forbiddenAction.Length;
            }

            return false;
        }
    }
}
