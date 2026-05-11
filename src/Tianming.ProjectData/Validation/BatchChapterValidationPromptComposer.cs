using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class BatchChapterValidationPromptItem
    {
        public string ChapterId { get; init; } = string.Empty;
        public string ChapterTitle { get; init; } = string.Empty;
        public int VolumeNumber { get; init; }
        public int ChapterNumber { get; init; }
        public string Content { get; init; } = string.Empty;
        public List<string> Characters { get; init; } = new();
        public List<string> Factions { get; init; } = new();
        public List<string> PlotRules { get; init; } = new();
    }

    public static class BatchChapterValidationPromptComposer
    {
        private const int ChapterPreviewLength = 1000;

        public static string Build(IReadOnlyList<BatchChapterValidationPromptItem> chapters)
        {
            ArgumentNullException.ThrowIfNull(chapters);

            var sb = new StringBuilder();
            sb.AppendLine("<batch_validation_task>");
            sb.AppendLine($"<batch_size>{chapters.Count}</batch_size>");
            sb.AppendLine("请对以下每个章节分别执行校验，返回JSON数组，数组长度必须严格等于 batch_size，第i项对应第i个章节。");
            sb.AppendLine();

            for (var index = 0; index < chapters.Count; index++)
            {
                AppendChapter(sb, chapters[index], index + 1);
            }

            AppendRequirements(sb, chapters.Count);
            sb.AppendLine("</batch_validation_task>");
            return sb.ToString();
        }

        private static void AppendChapter(StringBuilder sb, BatchChapterValidationPromptItem chapter, int index)
        {
            sb.AppendLine($"<chapter index=\"{index}\">");
            sb.AppendLine($"<chapter_id>{chapter.ChapterId}</chapter_id>");
            sb.AppendLine($"<chapter_info>标题={chapter.ChapterTitle}, 卷={chapter.VolumeNumber}, 章={chapter.ChapterNumber}</chapter_info>");
            AppendInlineTag(sb, "characters", chapter.Characters, 5);
            AppendInlineTag(sb, "factions", chapter.Factions, 5);
            AppendInlineTag(sb, "plot_rules", chapter.PlotRules, 3);
            sb.AppendLine($"<正文内容>{PreviewContent(chapter.Content)}</正文内容>");
            sb.AppendLine("</chapter>");
            sb.AppendLine();
        }

        private static void AppendRequirements(StringBuilder sb, int count)
        {
            sb.AppendLine("<校验要求>");
            sb.AppendLine($"对每个章节执行{ValidationRules.TotalRuleCount}条校验规则，返回JSON数组，数组长度={count}，顺序与输入章节一致：");
            sb.AppendLine("```json");
            sb.AppendLine("[");
            sb.AppendLine("  {");
            sb.AppendLine("    \"chapterId\": \"章节ID\",");
            sb.AppendLine("    \"overallResult\": \"通过|警告|失败|未校验\",");
            sb.AppendLine("    \"moduleResults\": " + ChapterValidationPromptTemplate.BuildJsonTemplate().Replace("\n", "\n    ").TrimEnd());
            sb.AppendLine("  }");
            sb.AppendLine("]");
            sb.AppendLine("```");
            sb.AppendLine($"每个对象的 moduleResults 必须包含全部 {ValidationRules.TotalRuleCount} 条规则，moduleName 必须与模板一致，不得为 null 或省略。");
            sb.AppendLine("重要：summary、reason、suggestion 字段中不得引用提示词中的标签名称（如正文内容、缺失数据说明等），只描述内容本身。");
            sb.AppendLine("</校验要求>");
        }

        private static void AppendInlineTag(StringBuilder sb, string tagName, IEnumerable<string> values, int maxCount)
        {
            var list = values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Take(maxCount)
                .ToList();
            if (list.Count == 0)
                return;

            sb.AppendLine($"<{tagName}>{string.Join("; ", list)}</{tagName}>");
        }

        private static string PreviewContent(string content)
        {
            return content.Length > ChapterPreviewLength
                ? content.Substring(0, ChapterPreviewLength) + "..."
                : content;
        }
    }
}
