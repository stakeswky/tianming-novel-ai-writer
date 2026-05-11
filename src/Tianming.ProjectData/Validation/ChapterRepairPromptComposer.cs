using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TM.Services.Modules.ProjectData.Implementations;

public sealed class ChapterRepairPromptComposer
{
    public const int DefaultMaxOriginalContentChars = 8_000;

    private readonly ChangesProtocolParser _changesProtocolParser;
    private readonly int _maxOriginalContentChars;

    public ChapterRepairPromptComposer()
        : this(new ChangesProtocolParser(), DefaultMaxOriginalContentChars)
    {
    }

    public ChapterRepairPromptComposer(ChangesProtocolParser changesProtocolParser, int maxOriginalContentChars = DefaultMaxOriginalContentChars)
    {
        _changesProtocolParser = changesProtocolParser ?? throw new ArgumentNullException(nameof(changesProtocolParser));
        _maxOriginalContentChars = Math.Max(1, maxOriginalContentChars);
    }

    public string Compose(string? existingContentRaw, IReadOnlyList<string>? hints)
    {
        var existingContent = StripValidChangesProtocol(existingContentRaw ?? string.Empty);
        var originalContentForPrompt = existingContent;
        var isOriginalTruncated = false;

        if (!string.IsNullOrWhiteSpace(originalContentForPrompt) &&
            originalContentForPrompt.Length > _maxOriginalContentChars)
        {
            originalContentForPrompt = originalContentForPrompt[.._maxOriginalContentChars];
            isOriginalTruncated = true;
        }

        var repairHints = (hints ?? Array.Empty<string>())
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .Select(h => h.Trim())
            .ToList();

        var builder = new StringBuilder();
        builder.AppendLine("<repair_directive>");
        builder.AppendLine("本次任务是修复已有章节，不是全新创作。请严格按以下原则操作：");
        builder.AppendLine("1. 以下「章节原文」是当前已保存的内容，请以此为基础进行修复，不得大幅偏离原文的整体事件走向和写作风格。");
        builder.AppendLine("2. 只针对「需修复的具体问题」进行最小化修改，不得引入新的主要情节。");
        builder.AppendLine("3. 修复后必须保持与上下章的情节衔接。");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(originalContentForPrompt))
        {
            builder.AppendLine("<章节原文>");
            builder.AppendLine(originalContentForPrompt);
            if (isOriginalTruncated)
            {
                builder.AppendLine("（章节原文过长，已截断）");
            }
            builder.AppendLine("</章节原文>");
            builder.AppendLine();
        }

        if (repairHints.Count > 0)
        {
            builder.AppendLine("需修复的具体问题：");
            for (var i = 0; i < repairHints.Count; i++)
            {
                builder.AppendLine($"{i + 1}. {repairHints[i]}");
            }
        }

        builder.AppendLine("</repair_directive>");
        return builder.ToString();
    }

    private string StripValidChangesProtocol(string rawContent)
    {
        var protocol = _changesProtocolParser.ValidateChangesProtocol(rawContent);
        return protocol.Success && protocol.ContentWithoutChanges != null
            ? protocol.ContentWithoutChanges
            : rawContent;
    }
}
