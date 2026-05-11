using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class ContentGenerationPreparer
    {
        private readonly GenerationGate _generationGate;

        private static readonly Regex RegexTagBlock = new(
            @"<\s*(think|thinking|analysis)\b[^>]*>[\s\S]*?<\s*/\s*\1\s*>",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RegexFencedBlock = new(
            @"```(?:thinking|analysis|reasoning)[\s\S]*?```",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex RegexOrphanTag = new(
            @"(?m)^\s*</?\s*(think|thinking|analysis)\b[^>]*>\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public ContentGenerationPreparer(GenerationGate generationGate)
        {
            _generationGate = generationGate;
        }

        public async Task<PreparedContentGeneration> PrepareStrictAsync(
            string chapterId,
            string rawContent,
            FactSnapshot factSnapshot,
            string? packagedTitle = null,
            IReadOnlyDictionary<string, string>? entityNameMap = null,
            GateResult? gateResult = null,
            DesignElementNames? designElements = null)
        {
            var effectiveGateResult = gateResult != null && gateResult.Success
                ? gateResult
                : await _generationGate.ValidateAsync(chapterId, rawContent, factSnapshot, designElements);

            if (!effectiveGateResult.Success)
            {
                return new PreparedContentGeneration
                {
                    ChapterId = chapterId,
                    GateResult = effectiveGateResult
                };
            }

            var protocolResult = new ChangesProtocolParser().ValidateChangesProtocol(rawContent);
            var content = protocolResult.ContentWithoutChanges ?? effectiveGateResult.ContentWithoutChanges ?? rawContent;
            var parsedChanges = effectiveGateResult.ParsedChanges ?? protocolResult.Changes;
            var persistedContent = NormalizePersistedContent(chapterId, content, packagedTitle);
            var summary = parsedChanges != null
                ? BuildStructuredSummary(persistedContent, parsedChanges, entityNameMap)
                : ExtractSummary(persistedContent);

            return new PreparedContentGeneration
            {
                ChapterId = chapterId,
                GateResult = effectiveGateResult,
                ParsedChanges = parsedChanges,
                ContentWithoutChanges = content,
                PersistedContent = persistedContent,
                Summary = summary
            };
        }

        public static string NormalizePersistedContent(string chapterId, string content, string? packagedTitle = null)
        {
            var normalizedBody = StripModelArtifacts(content);
            normalizedBody = StripLeadingHeadings(normalizedBody);

            var canonicalTitle = BuildCanonicalTitle(chapterId, NormalizeChapterTitle(packagedTitle ?? string.Empty));
            if (string.IsNullOrWhiteSpace(normalizedBody))
                return $"# {canonicalTitle}";

            return $"# {canonicalTitle}\n\n{normalizedBody}";
        }

        private static string BuildCanonicalTitle(string chapterId, string title)
        {
            var parsed = ParseChapterId(chapterId);
            if (parsed?.chapterNumber > 0)
                return string.IsNullOrWhiteSpace(title) ? $"第{parsed.Value.chapterNumber}章" : $"第{parsed.Value.chapterNumber}章 {title}";

            return string.IsNullOrWhiteSpace(title) ? chapterId : title;
        }

        private static (int volumeNumber, int chapterNumber)? ParseChapterId(string chapterId)
        {
            if (string.IsNullOrWhiteSpace(chapterId))
                return null;

            var patterns = new[]
            {
                @"vol(\d+)_ch(\d+)",
                @"v(\d+)_c(\d+)",
                @"(\d+)_(\d+)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(chapterId, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                    return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
            }

            return null;
        }

        private static string NormalizeChapterTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            var trimmed = title.Trim();
            var match = Regex.Match(trimmed, @"^第(\d+)(?:章节|章)[：:.]?\s*(.*)");
            if (!match.Success)
                return trimmed;

            var chapterName = match.Groups[2].Value.Trim();
            return string.IsNullOrEmpty(chapterName) ? $"第{match.Groups[1].Value}章" : chapterName;
        }

        private static string StripLeadingHeadings(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;

            var text = content.TrimStart();
            while (!string.IsNullOrEmpty(text))
            {
                var firstLineEnd = text.IndexOf('\n');
                var firstLine = (firstLineEnd >= 0 ? text[..firstLineEnd] : text).Trim();
                if (!firstLine.StartsWith("#", StringComparison.Ordinal))
                    break;

                text = firstLineEnd >= 0 ? text[(firstLineEnd + 1)..].TrimStart() : string.Empty;
            }

            return text.Trim();
        }

        private static string StripModelArtifacts(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;

            content = RegexTagBlock.Replace(content, string.Empty);
            content = RegexFencedBlock.Replace(content, string.Empty);
            content = RegexOrphanTag.Replace(content, string.Empty);

            return content.Trim();
        }

        private static string BuildStructuredSummary(
            string content,
            ChapterChanges changes,
            IReadOnlyDictionary<string, string>? nameMap = null)
        {
            string Resolve(string id) =>
                !string.IsNullOrWhiteSpace(id) && nameMap != null && nameMap.TryGetValue(id, out var name)
                    ? name
                    : id;

            var sb = new StringBuilder();
            sb.AppendLine(ExtractSummary(content, 200));

            foreach (var c in changes.CharacterStateChanges ?? new())
            {
                if (!string.IsNullOrWhiteSpace(c.KeyEvent))
                    sb.AppendLine($"[角色]{Resolve(c.CharacterId)}: {c.KeyEvent}");
            }

            foreach (var c in changes.ConflictProgress ?? new())
            {
                if (!string.IsNullOrWhiteSpace(c.Event))
                    sb.AppendLine($"[冲突]{Resolve(c.ConflictId)}: {c.Event}→{c.NewStatus}");
            }

            foreach (var p in changes.NewPlotPoints ?? new())
            {
                if (!string.IsNullOrWhiteSpace(p.Context))
                    sb.AppendLine($"[情节]{p.Context}");
            }

            foreach (var f in changes.ForeshadowingActions ?? new())
            {
                if (!string.IsNullOrWhiteSpace(f.ForeshadowId))
                    sb.AppendLine($"[伏笔]{Resolve(f.ForeshadowId)}: {f.Action}");
            }

            foreach (var l in changes.LocationStateChanges ?? new())
            {
                if (!string.IsNullOrWhiteSpace(l.Event))
                    sb.AppendLine($"[地点]{Resolve(l.LocationId)}: {l.Event}→{l.NewStatus}");
            }

            foreach (var faction in changes.FactionStateChanges ?? new())
            {
                if (!string.IsNullOrWhiteSpace(faction.Event))
                    sb.AppendLine($"[势力]{Resolve(faction.FactionId)}: {faction.Event}→{faction.NewStatus}");
            }

            if (changes.TimeProgression != null && !string.IsNullOrWhiteSpace(changes.TimeProgression.TimePeriod))
                sb.AppendLine($"[时间]{changes.TimeProgression.TimePeriod} 经过{changes.TimeProgression.ElapsedTime}");

            foreach (var movement in changes.CharacterMovements ?? new())
            {
                if (!string.IsNullOrWhiteSpace(movement.ToLocation))
                    sb.AppendLine($"[移动]{Resolve(movement.CharacterId)}: {Resolve(movement.FromLocation)}→{Resolve(movement.ToLocation)}");
            }

            foreach (var item in changes.ItemTransfers ?? new())
            {
                if (!string.IsNullOrWhiteSpace(item.Event))
                    sb.AppendLine($"[物品]{item.ItemName}: {item.Event} ({Resolve(item.FromHolder)}→{Resolve(item.ToHolder)})");
            }

            return sb.ToString().Trim();
        }

        private static string ExtractSummary(string content, int maxLength = 500)
        {
            if (string.IsNullOrEmpty(content))
                return string.Empty;

            var cleaned = content.Replace("\r\n", " ").Replace("\n", " ").Trim();
            if (cleaned.Length <= maxLength)
                return cleaned;

            var cutRegion = cleaned[..maxLength];
            var lastSentenceEnd = cutRegion.LastIndexOfAny(new[] { '。', '！', '？', '…', '"' });
            if (lastSentenceEnd > maxLength / 3)
                return cutRegion[..(lastSentenceEnd + 1)] + "……";

            return cutRegion + "……";
        }
    }

    public sealed class PreparedContentGeneration
    {
        public string ChapterId { get; set; } = string.Empty;
        public GateResult GateResult { get; set; } = new();
        public ChapterChanges? ParsedChanges { get; set; }
        public string ContentWithoutChanges { get; set; } = string.Empty;
        public string PersistedContent { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
    }
}
