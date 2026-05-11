using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TM.Services.Modules.ProjectData.Interfaces;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class ChapterValidationPromptInput
    {
        public string ChapterId { get; init; } = string.Empty;
        public string ChapterTitle { get; init; } = string.Empty;
        public int VolumeNumber { get; init; }
        public int ChapterNumber { get; init; }
        public string VolumeName { get; init; } = string.Empty;
        public string ChapterContent { get; init; } = string.Empty;
        public List<string> TemplateItems { get; init; } = new();
        public List<string> WorldRuleItems { get; init; } = new();
        public List<string> CharacterItems { get; init; } = new();
        public List<string> FactionItems { get; init; } = new();
        public List<string> LocationItems { get; init; } = new();
        public List<string> PlotItems { get; init; } = new();
        public List<string> OutlineItems { get; init; } = new();
        public List<string> ChapterPlanItems { get; init; } = new();
        public List<string> BlueprintItems { get; init; } = new();
        public List<string> VolumeDesignItems { get; init; } = new();
        public List<ValidationIssue> KnownStructuralIssues { get; init; } = new();
    }

    public static class ChapterValidationPromptComposer
    {
        private const int ChapterPreviewLength = 1000;

        public static string Build(ChapterValidationPromptInput input)
        {
            ArgumentNullException.ThrowIfNull(input);

            var sb = new StringBuilder();
            sb.AppendLine("<validation_task>");
            sb.AppendLine();
            AppendChapterInfo(sb, input);

            AppendSection(sb, "创作模板（文风约束）", input.TemplateItems);
            AppendSection(sb, "世界观规则", input.WorldRuleItems);
            AppendSection(sb, "角色设定（本章相关）", input.CharacterItems);
            AppendSection(sb, "势力设定（本章相关）", input.FactionItems);
            AppendSection(sb, "地点设定（本章相关）", input.LocationItems);
            AppendSection(sb, "剧情规则（本章相关）", input.PlotItems);
            AppendSection(sb, "全书大纲", input.OutlineItems);
            AppendSection(sb, "章节规划", input.ChapterPlanItems);
            AppendSection(sb, "章节蓝图", input.BlueprintItems);
            AppendSection(sb, "分卷设计", input.VolumeDesignItems);

            AppendMissingRules(sb, input);
            AppendChapterContent(sb, input.ChapterContent);
            AppendKnownStructuralIssues(sb, input.KnownStructuralIssues);
            AppendRequirements(sb);

            sb.AppendLine("</validation_task>");
            return sb.ToString();
        }

        private static void AppendChapterInfo(StringBuilder sb, ChapterValidationPromptInput input)
        {
            sb.AppendLine("<chapter_info>");
            sb.AppendLine($"- 章节ID: {input.ChapterId}");
            sb.AppendLine($"- 章节标题: {input.ChapterTitle}");
            sb.AppendLine($"- 卷号: {input.VolumeNumber}");
            sb.AppendLine($"- 章节号: {input.ChapterNumber}");
            sb.AppendLine($"- 卷名: {input.VolumeName}");
            sb.AppendLine("</chapter_info>");
            sb.AppendLine();
        }

        private static void AppendMissingRules(StringBuilder sb, ChapterValidationPromptInput input)
        {
            var missingRules = new List<string>();
            if (input.TemplateItems.Count == 0) missingRules.Add("StyleConsistency");
            if (input.WorldRuleItems.Count == 0) missingRules.Add("WorldviewConsistency");
            if (input.CharacterItems.Count == 0) missingRules.Add("CharacterConsistency");
            if (input.FactionItems.Count == 0) missingRules.Add("FactionConsistency");
            if (input.LocationItems.Count == 0) missingRules.Add("LocationConsistency");
            if (input.PlotItems.Count == 0) missingRules.Add("PlotConsistency");
            if (input.OutlineItems.Count == 0) missingRules.Add("OutlineConsistency");
            if (input.ChapterPlanItems.Count == 0) missingRules.Add("ChapterPlanConsistency");
            if (input.BlueprintItems.Count == 0) missingRules.Add("BlueprintConsistency");
            if (input.VolumeDesignItems.Count == 0) missingRules.Add("VolumeDesignConsistency");

            if (missingRules.Count == 0)
                return;

            sb.AppendLine("<缺失数据说明>");
            sb.AppendLine("以下规则缺少对应数据，请将 result 填写为\"未校验\"（系统按警告处理），problemItems 可为空：");
            foreach (var rule in missingRules.Distinct())
            {
                sb.AppendLine($"- {rule}（{ValidationRules.GetDisplayName(rule)}）");
            }
            sb.AppendLine("</缺失数据说明>");
            sb.AppendLine();
        }

        private static void AppendChapterContent(StringBuilder sb, string chapterContent)
        {
            sb.AppendLine("<正文内容>");
            sb.AppendLine(chapterContent.Length > ChapterPreviewLength
                ? chapterContent.Substring(0, ChapterPreviewLength) + "..."
                : chapterContent);
            sb.AppendLine("</正文内容>");
            sb.AppendLine();
        }

        private static void AppendKnownStructuralIssues(StringBuilder sb, List<ValidationIssue> knownStructuralIssues)
        {
            if (knownStructuralIssues.Count == 0)
                return;

            sb.AppendLine("<已确认结构性问题>");
            foreach (var issue in knownStructuralIssues)
            {
                sb.AppendLine($"- [{issue.Type}] {issue.Message}");
            }
            sb.AppendLine("</已确认结构性问题>");
            sb.AppendLine("以上结构性问题已由规则层确认。你的任务是专注于设计数据的语义一致性（10条规则），不要重复检查上述已确认问题。");
            sb.AppendLine();
        }

        private static void AppendRequirements(StringBuilder sb)
        {
            sb.AppendLine("<校验要求>");
            sb.AppendLine($"请对章节执行{ValidationRules.TotalRuleCount}条校验规则，返回JSON格式的校验结果。");
            sb.AppendLine($"1. moduleResults必须输出完整规则清单（{ValidationRules.TotalRuleCount}项），缺失项视为协议错误");
            sb.AppendLine("2. extendedData为每个规则的差异字段容器，内容允许为空但不允许缺字段名");
            sb.AppendLine("3. 当result为警告/失败/未校验时，problemItems至少1条（未校验可说明原因）");
            sb.AppendLine("4. 当result为通过时，problemItems允许为空数组");
            sb.AppendLine("5. 重要：summary、reason、suggestion 字段中不得引用提示词中的标签名称（如 正文内容、缺失数据说明、已确认结构性问题、校验要求 等），只描述内容本身。");
            sb.AppendLine();
            sb.AppendLine("返回JSON格式：");
            sb.AppendLine("```json");
            sb.AppendLine(ChapterValidationPromptTemplate.BuildJsonTemplate());
            sb.AppendLine("```");
            sb.AppendLine("</校验要求>");
            sb.AppendLine();
            sb.AppendLine("<validation_rules_description>");
            sb.AppendLine(ChapterValidationPromptTemplate.BuildRulesDescription());
            sb.AppendLine("</validation_rules_description>");
            sb.AppendLine();
        }

        private static void AppendSection(StringBuilder sb, string title, IEnumerable<string> lines, int max = 8)
        {
            var list = lines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Take(max)
                .ToList();
            if (list.Count == 0)
                return;

            sb.AppendLine($"<section name=\"{title}\">");
            foreach (var line in list)
            {
                sb.AppendLine($"- {line}");
            }
            sb.AppendLine("</section>");
            sb.AppendLine();
        }
    }
}
