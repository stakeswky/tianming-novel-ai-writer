using System;
using System.Security.Cryptography;
using System.Text;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public static class ChapterValidationPromptTemplate
    {
        public static string BuildJsonTemplate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"overallResult\": \"通过|警告|失败|未校验\",");
            sb.AppendLine("  \"moduleResults\": [");

            for (var i = 0; i < ValidationRules.AllModuleNames.Length; i++)
            {
                var moduleName = ValidationRules.AllModuleNames[i];
                var displayName = ValidationRules.GetDisplayName(moduleName);
                var verificationType = GetVerificationType(moduleName);
                var fields = ValidationRules.GetExtendedDataSchema(moduleName);

                sb.AppendLine("    {");
                sb.AppendLine($"      \"moduleName\": \"{moduleName}\",");
                sb.AppendLine($"      \"displayName\": \"{displayName}\",");
                sb.AppendLine($"      \"verificationType\": \"{verificationType}\",");
                sb.AppendLine("      \"result\": \"通过|警告|失败|未校验\",");
                sb.AppendLine("      \"issueDescription\": \"问题描述（可空）\",");
                sb.AppendLine("      \"fixSuggestion\": \"修复建议（可空）\",");
                sb.AppendLine("      \"extendedData\": {");

                for (var f = 0; f < fields.Length; f++)
                {
                    var field = fields[f];
                    var camelCaseField = char.ToLowerInvariant(field[0]) + field.Substring(1);
                    var suffix = f == fields.Length - 1 ? string.Empty : ",";
                    sb.AppendLine($"        \"{camelCaseField}\": \"\"{suffix}");
                }

                sb.AppendLine("      },");
                sb.AppendLine("      \"problemItems\": [");
                sb.AppendLine("        {");
                sb.AppendLine("          \"summary\": \"问题简述\",");
                sb.AppendLine("          \"reason\": \"原因依据\",");
                sb.AppendLine("          \"details\": \"补充详情（可选）\",");
                sb.AppendLine("          \"suggestion\": \"修复建议（可选）\"");
                sb.AppendLine("        }");
                sb.AppendLine("      ]");
                sb.Append("    }");
                sb.AppendLine(i == ValidationRules.AllModuleNames.Length - 1 ? string.Empty : ",");
            }

            sb.AppendLine("  ]");
            sb.AppendLine("}");
            return sb.ToString();
        }

        public static string BuildRulesDescription()
        {
            var sb = new StringBuilder();
            sb.AppendLine("1. StyleConsistency（文风模板一致性）：对齐创作模板文风/类型/构思");
            sb.AppendLine("   - extendedData: templateName, genre, overallIdea, styleHint");
            sb.AppendLine("2. WorldviewConsistency（世界观一致性）：对齐硬规则/力量体系/特殊法则");
            sb.AppendLine("   - extendedData: worldRuleName, hardRules, powerSystem, specialLaws");
            sb.AppendLine("3. CharacterConsistency（角色设定一致性）：对齐身份/特质/弧光目标");
            sb.AppendLine("   - extendedData: characterName, identity, coreTraits, arcGoal");
            sb.AppendLine("4. FactionConsistency（势力设定一致性）：对齐势力类型/目标/领袖");
            sb.AppendLine("   - extendedData: factionName, factionType, goal, leader");
            sb.AppendLine("5. LocationConsistency（地点设定一致性）：对齐地点类型/描述/地形");
            sb.AppendLine("   - extendedData: locationName, locationType, description, terrain");
            sb.AppendLine("6. PlotConsistency（剧情规则一致性）：对齐剧情阶段/目标/冲突/结果");
            sb.AppendLine("   - extendedData: plotName, storyPhase, goal, conflict, result");
            sb.AppendLine("7. OutlineConsistency（大纲一致性）：对齐一句话大纲/核心冲突/主题/结局");
            sb.AppendLine("   - extendedData: oneLineOutline, coreConflict, theme, endingState");
            sb.AppendLine("8. ChapterPlanConsistency（章节规划一致性）：对齐本章目标/转折/伏笔");
            sb.AppendLine("   - extendedData: chapterTitle, mainGoal, keyTurn, hook, foreshadowing");
            sb.AppendLine("9. BlueprintConsistency（章节蓝图一致性）：对齐结构/节奏/角色地点清单");
            sb.AppendLine("   - extendedData: chapterId, oneLineStructure, pacingCurve, cast, locations");
            sb.AppendLine("10. VolumeDesignConsistency（分卷设计一致性）：对齐卷主题/阶段目标/主冲突/关键事件");
            sb.AppendLine("   - extendedData: volumeTitle, volumeTheme, stageGoal, mainConflict, keyEvents");
            return sb.ToString();
        }

        public static string GetVerificationType(string moduleName)
        {
            return moduleName switch
            {
                "StyleConsistency" => "文风",
                "WorldviewConsistency" => "世界观",
                "CharacterConsistency" => "角色",
                "FactionConsistency" => "势力",
                "LocationConsistency" => "地点",
                "PlotConsistency" => "剧情",
                "OutlineConsistency" => "大纲",
                "ChapterPlanConsistency" => "章节规划",
                "BlueprintConsistency" => "章节蓝图",
                "VolumeDesignConsistency" => "分卷设计",
                _ => "通用"
            };
        }

        public static string BuildRulesSignature()
        {
            var sb = new StringBuilder();
            foreach (var moduleName in ValidationRules.AllModuleNames)
            {
                sb.Append(moduleName).Append(':');
                foreach (var field in ValidationRules.GetExtendedDataSchema(moduleName))
                {
                    sb.Append(field).Append(',');
                }
                sb.Append('|');
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            var hash = SHA256.HashData(bytes);
            return Convert.ToHexString(hash)[..16];
        }
    }
}
