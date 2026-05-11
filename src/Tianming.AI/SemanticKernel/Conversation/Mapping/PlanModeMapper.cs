using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using TM.Framework.Common.Helpers;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Helpers;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Models;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Parsing;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation.Mapping;

public sealed class PlanModeMapper : IConversationMessageMapper
{
    private readonly IPlanParser _parser;
    private readonly string? _contentGuideDirectory;

    public PlanModeMapper(IPlanParser parser, string? contentGuideDirectory = null)
    {
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _contentGuideDirectory = contentGuideDirectory;
    }

    public ConversationMessage? TryBuildPlanWithoutModel(string userInput)
    {
        var guideSteps = BuildPlanFromContentGuide(userInput, out var usedContentGuide, out var noMatch, out var missingNumbers);
        if (!usedContentGuide)
            return null;

        var steps = guideSteps ?? Array.Empty<PlanStep>();
        var message = new ConversationMessage
        {
            Role = ConversationRole.Assistant,
            Timestamp = DateTime.Now,
            AnalysisRaw = BuildPseudoThinking(userInput, steps, noMatch, missingNumbers),
            Payload = new PlanPayload
            {
                Steps = steps,
                RawContent = "[基于打包数据直接生成计划]"
            }
        };

        message.Summary = noMatch
            ? "⚠️ 未匹配到章节，请检查@续写/章节号是否存在，或重新打包后再试。"
            : GenerateSummary(message);

        return message;
    }

    public ConversationMessage MapFromStreamingResult(string userInput, string rawContent, string? thinking)
    {
        var guideSteps = BuildPlanFromContentGuide(userInput, out var usedContentGuide, out var noMatch, out _);
        var steps = guideSteps ?? _parser.Parse(rawContent);
        var message = new ConversationMessage
        {
            Role = ConversationRole.Assistant,
            Timestamp = DateTime.Now,
            AnalysisRaw = thinking ?? string.Empty,
            AnalysisBlocks = ThinkingBlockParser.Parse(thinking),
            Payload = new PlanPayload
            {
                Steps = steps,
                RawContent = rawContent
            }
        };

        message.Summary = usedContentGuide && noMatch
            ? "⚠️ 未匹配到章节，请检查@续写/章节号是否存在，或重新打包后再试。"
            : GenerateSummary(message);
        return message;
    }

    public string GenerateSummary(ConversationMessage message)
    {
        if (message.Payload is PlanPayload { Steps.Count: > 0 } payload)
            return ConversationSummarizer.ForPlanGenerated(payload.Steps.Count);

        if (message.Payload is PlanPayload)
            return ConversationSummarizer.ForPlanParseFailed();

        return message.Summary;
    }

    private IReadOnlyList<PlanStep>? BuildPlanFromContentGuide(
        string userInput,
        out bool usedContentGuide,
        out bool noMatch,
        out IReadOnlyList<int> missingNumbers)
    {
        usedContentGuide = false;
        noMatch = false;
        missingNumbers = Array.Empty<int>();

        var guideFiles = FindContentGuideFiles();
        if (guideFiles.Count == 0)
            return null;

        usedContentGuide = true;
        var chapters = LoadChapters(guideFiles);
        if (chapters.Count == 0)
            return null;

        var filtered = ApplyChapterFilter(chapters, userInput);
        if (filtered.Count == 0)
        {
            noMatch = true;
            return Array.Empty<PlanStep>();
        }

        var requested = ComputeRequestedChapterNumbers(userInput);
        if (requested.Count > 0)
        {
            var matched = filtered
                .Select(item => item.ChapterNumber)
                .Where(number => number > 0)
                .ToHashSet();
            missingNumbers = requested.Where(number => !matched.Contains(number)).ToArray();
        }

        return filtered
            .Select((item, index) => new PlanStep
            {
                Index = index + 1,
                Title = BuildStepTitle(item.Entry, item.ChapterNumber),
                Detail = BuildStepDetail(item.Entry),
                ChapterId = item.Entry.ChapterId,
                ChapterNumber = item.ChapterNumber
            })
            .ToArray();
    }

    private IReadOnlyList<string> FindContentGuideFiles()
    {
        if (string.IsNullOrWhiteSpace(_contentGuideDirectory) || !Directory.Exists(_contentGuideDirectory))
            return Array.Empty<string>();

        var shardFiles = Directory
            .GetFiles(_contentGuideDirectory, "content_guide_vol*.json")
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (shardFiles.Length > 0)
            return shardFiles;

        var legacy = Path.Combine(_contentGuideDirectory, "content_guide.json");
        return File.Exists(legacy) ? new[] { legacy } : Array.Empty<string>();
    }

    private static List<ChapterPlanItem> LoadChapters(IReadOnlyList<string> guideFiles)
    {
        var merged = new Dictionary<string, ContentGuideEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var guideFile in guideFiles)
        {
            var guide = JsonSerializer.Deserialize<ContentGuide>(File.ReadAllText(guideFile), JsonOptions);
            if (guide?.Chapters == null)
                continue;

            foreach (var pair in guide.Chapters)
                merged[pair.Key] = pair.Value;
        }

        return merged.Values
            .Select(entry => new ChapterPlanItem(entry, ResolveVolumeNumber(entry), ResolveChapterNumber(entry)))
            .OrderBy(item => item.VolumeNumber > 0 ? 0 : 1)
            .ThenBy(item => item.VolumeNumber)
            .ThenBy(item => item.ChapterNumber > 0 ? 0 : 1)
            .ThenBy(item => item.ChapterNumber)
            .ThenBy(item => item.Entry.ChapterId, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<ChapterPlanItem> ApplyChapterFilter(IReadOnlyList<ChapterPlanItem> chapters, string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            return chapters.ToList();

        var continueChapterId = ChapterDirectiveParser.ParseSourceChapterId(userInput);
        if (!string.IsNullOrWhiteSpace(continueChapterId) && IsValidChapterId(continueChapterId))
        {
            var strippedInput = StripContinueDirective(userInput, continueChapterId);
            var hasExplicitRange = ComputeRequestedChapterNumbers(strippedInput).Count > 0;
            if (!hasExplicitRange)
            {
                var currentIndex = FindChapterIndex(chapters, continueChapterId);
                return currentIndex >= 0 && currentIndex + 1 < chapters.Count
                    ? new List<ChapterPlanItem> { chapters[currentIndex + 1] }
                    : new List<ChapterPlanItem>();
            }

            userInput = strippedInput;
        }

        var targetNumbers = ComputeRequestedChapterNumbers(userInput);
        if (targetNumbers.Count == 0)
            return chapters.ToList();

        var byNumber = chapters
            .Where(item => targetNumbers.Contains(item.ChapterNumber))
            .ToList();
        if (byNumber.Count > 0)
            return byNumber;

        var minNumber = targetNumbers.Min();
        var maxNumber = targetNumbers.Max();
        var startIndex = Math.Max(0, minNumber - 1);
        var count = Math.Max(0, maxNumber - minNumber + 1);
        if (startIndex >= chapters.Count || count == 0)
            return new List<ChapterPlanItem>();

        return chapters.Skip(startIndex).Take(count).ToList();
    }

    private static int FindChapterIndex(IReadOnlyList<ChapterPlanItem> chapters, string chapterId)
    {
        var parsed = ChapterParserHelper.ParseChapterId(chapterId);
        if (parsed.HasValue)
        {
            for (var i = 0; i < chapters.Count; i++)
            {
                if (chapters[i].VolumeNumber == parsed.Value.volumeNumber
                    && chapters[i].ChapterNumber == parsed.Value.chapterNumber)
                    return i;
            }
        }

        for (var i = 0; i < chapters.Count; i++)
        {
            if (string.Equals(chapters[i].Entry.ChapterId, chapterId, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static IReadOnlyList<int> ComputeRequestedChapterNumbers(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            return Array.Empty<int>();

        var ranges = ChapterParserHelper.ParseChapterRanges(userInput);
        if (ranges?.Count > 0)
        {
            var numbers = new HashSet<int>();
            foreach (var (start, end) in ranges)
            {
                for (var i = start; i <= end; i++)
                    numbers.Add(i);
            }

            return numbers.OrderBy(number => number).ToArray();
        }

        var (singleStart, singleEnd) = ChapterParserHelper.ParseChapterRange(userInput) ?? (0, 0);
        if (singleStart > 0 && singleEnd >= singleStart)
            return Enumerable.Range(singleStart, singleEnd - singleStart + 1).ToArray();

        var list = ChapterParserHelper.ParseChapterNumberList(userInput);
        if (list?.Count > 0)
            return list;

        var (_, chapter) = ChapterParserHelper.ParseFromNaturalLanguage(userInput);
        return chapter.HasValue && chapter.Value > 0
            ? new[] { chapter.Value }
            : Array.Empty<int>();
    }

    private static int ResolveVolumeNumber(ContentGuideEntry entry)
    {
        return ChapterParserHelper.ParseChapterId(entry.ChapterId)?.volumeNumber ?? 0;
    }

    private static int ResolveChapterNumber(ContentGuideEntry entry)
    {
        if (entry.ChapterNumber > 0)
            return entry.ChapterNumber;

        var parsed = ChapterParserHelper.ParseChapterId(entry.ChapterId);
        if (parsed.HasValue && parsed.Value.chapterNumber > 0)
            return parsed.Value.chapterNumber;

        var fromSuffix = ChapterParserHelper.ExtractChapterNumberFromSuffix(entry.ChapterId);
        if (fromSuffix > 0)
            return fromSuffix;

        if (!string.IsNullOrWhiteSpace(entry.Title))
        {
            var (number, _) = ChapterParserHelper.ExtractChapterParts(entry.Title);
            if (number.HasValue && number.Value > 0)
                return number.Value;

            var (_, chapter) = ChapterParserHelper.ParseFromNaturalLanguage(entry.Title);
            if (chapter.HasValue && chapter.Value > 0)
                return chapter.Value;
        }

        return 0;
    }

    private static string BuildStepTitle(ContentGuideEntry entry, int chapterNumber)
    {
        var title = string.IsNullOrWhiteSpace(entry.Title)
            ? entry.Scenes.FirstOrDefault()?.Title
            : entry.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
            title = chapterNumber > 0 ? $"第{chapterNumber}章" : entry.ChapterId;

        return chapterNumber > 0 && !ChapterParserHelper.IsChapterTitle(title)
            ? $"第{chapterNumber}章 · {title}"
            : title;
    }

    private static string BuildStepDetail(ContentGuideEntry entry)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"章节ID: {entry.ChapterId}");
        AppendLineIfPresent(sb, "章节蓝图ID", entry.ContextIds?.ChapterBlueprint);
        AppendListIfPresent(sb, "涉及角色", entry.ContextIds?.Characters);
        AppendListIfPresent(sb, "涉及地点", entry.ContextIds?.Locations);
        AppendListIfPresent(sb, "涉及势力", entry.ContextIds?.Factions);
        AppendListIfPresent(sb, "涉及剧情", entry.ContextIds?.PlotRules);

        sb.AppendLine();
        sb.AppendLine("章节信息:");
        AppendLineIfPresent(sb, "章节主题", entry.Title);
        AppendLineIfPresent(sb, "摘要", entry.Summary);
        AppendLineIfPresent(sb, "主目标", entry.MainGoal);
        AppendLineIfPresent(sb, "世界信息", entry.WorldInfoDrop);
        AppendLineIfPresent(sb, "主题", entry.ChapterTheme);
        AppendLineIfPresent(sb, "关键转折", entry.KeyTurn);
        AppendLineIfPresent(sb, "角色弧线进展", entry.CharacterArcProgress);
        AppendLineIfPresent(sb, "钩子", entry.Hook);
        AppendLineIfPresent(sb, "伏笔", entry.Foreshadowing);

        if (entry.Scenes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("场景:");
            foreach (var scene in entry.Scenes.OrderBy(scene => scene.SceneNumber))
            {
                var title = string.IsNullOrWhiteSpace(scene.Title) ? string.Empty : scene.Title.Trim();
                var pov = string.IsNullOrWhiteSpace(scene.PovCharacter) ? string.Empty : $" | 视角: {scene.PovCharacter.Trim()}";
                sb.AppendLine($"场景{scene.SceneNumber}: {title}{pov}");
                AppendLineIfPresent(sb, "目的", scene.Purpose);
                AppendLineIfPresent(sb, "起", scene.Opening);
                AppendLineIfPresent(sb, "承", scene.Development);
                AppendLineIfPresent(sb, "转", scene.Turning);
                AppendLineIfPresent(sb, "合", scene.Ending);
                AppendLineIfPresent(sb, "信息投放", scene.InfoDrop);
            }
        }

        return sb.ToString().Trim();
    }

    private static void AppendLineIfPresent(StringBuilder sb, string label, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            sb.AppendLine($"{label}: {value.Trim()}");
    }

    private static void AppendListIfPresent(StringBuilder sb, string label, IReadOnlyCollection<string>? values)
    {
        if (values is { Count: > 0 })
            sb.AppendLine($"{label}: {string.Join(", ", values)}");
    }

    private static string BuildPseudoThinking(
        string userInput,
        IReadOnlyList<PlanStep> steps,
        bool noMatch,
        IReadOnlyList<int> missingNumbers)
    {
        var sb = new StringBuilder();
        var trimmedInput = userInput.Length > 60 ? userInput[..60] + "..." : userInput;
        sb.AppendLine($"好的，用户的请求是「{trimmedInput}」，让我先理解一下意图。");
        sb.AppendLine();

        if (noMatch)
        {
            sb.AppendLine("我检查了本地的内容纲要数据，但没有找到与这个请求匹配的章节信息。可能是章节编号不存在，或者纲要数据需要重新打包更新。");
            sb.AppendLine();
            sb.AppendLine("我无法为这个请求生成有效的执行计划，需要用户确认章节信息后重试。");
        }
        else if (steps.Count > 0)
        {
            sb.AppendLine("看了一下本地的内容纲要，里面已经有完整的章节规划数据了。不需要我从零开始分析和拆解任务，直接基于已有的规划来构建执行计划就行。");
            sb.AppendLine();
            sb.AppendLine(steps.Count == 1
                ? $"匹配到的章节是「{steps[0].Title}」，这是一个单章任务，直接作为一个执行步骤。"
                : $"一共匹配到 {steps.Count} 个章节，按顺序排列：");

            if (steps.Count > 1)
            {
                foreach (var step in steps)
                    sb.AppendLine($"- {step.Title}");
            }

            if (missingNumbers.Count > 0)
                sb.AppendLine($"⚠️ 注意：{string.Join("、", missingNumbers.Select(n => $"第{n}章"))} 未在打包数据中找到，已自动跳过。");
        }

        return sb.ToString();
    }

    private static bool IsValidChapterId(string chapterId)
    {
        return chapterId.All(c => c < '\u4e00' || c > '\u9fff');
    }

    private static string StripContinueDirective(string input, string chapterId)
    {
        var escapedId = System.Text.RegularExpressions.Regex.Escape(chapterId);
        return System.Text.RegularExpressions.Regex.Replace(
            input,
            $@"@(?:续写|continue)[:：\s]*{escapedId}",
            "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed record ChapterPlanItem(ContentGuideEntry Entry, int VolumeNumber, int ChapterNumber);

    private sealed class ContentGuide
    {
        public Dictionary<string, ContentGuideEntry> Chapters { get; set; } = new();
    }

    private sealed class ContentGuideEntry
    {
        public string ChapterId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public GuideContextIds? ContextIds { get; set; } = new();
        public List<SceneGuideEntry> Scenes { get; set; } = new();
        public int ChapterNumber { get; set; }
        public string ChapterTheme { get; set; } = string.Empty;
        public string MainGoal { get; set; } = string.Empty;
        public string KeyTurn { get; set; } = string.Empty;
        public string Hook { get; set; } = string.Empty;
        public string WorldInfoDrop { get; set; } = string.Empty;
        public string CharacterArcProgress { get; set; } = string.Empty;
        public string Foreshadowing { get; set; } = string.Empty;
    }

    private sealed class GuideContextIds
    {
        public string ChapterBlueprint { get; set; } = string.Empty;
        public List<string> Characters { get; set; } = new();
        public List<string> Factions { get; set; } = new();
        public List<string> Locations { get; set; } = new();
        public List<string> PlotRules { get; set; } = new();
    }

    private sealed class SceneGuideEntry
    {
        public int SceneNumber { get; set; }
        public string Title { get; set; } = string.Empty;
        public string PovCharacter { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public string Opening { get; set; } = string.Empty;
        public string Development { get; set; } = string.Empty;
        public string Turning { get; set; } = string.Empty;
        public string Ending { get; set; } = string.Empty;
        public string InfoDrop { get; set; } = string.Empty;
    }
}
