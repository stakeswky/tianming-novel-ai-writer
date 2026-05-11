using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TM.Services.Modules.ProjectData.Interfaces;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public static class ChapterValidationResultParser
    {
        private const string SystemModuleName = "System";

        public static void ApplyAIContent(ChapterValidationResult result, string aiContent)
        {
            ArgumentNullException.ThrowIfNull(result);

            try
            {
                var jsonStart = aiContent.IndexOf('{');
                var jsonEnd = aiContent.LastIndexOf('}');
                if (jsonStart < 0 || jsonEnd < 0 || jsonEnd <= jsonStart)
                {
                    AddProtocolErrorIssue(result, "AI返回内容中未找到有效JSON");
                    return;
                }

                var json = aiContent.Substring(jsonStart, jsonEnd - jsonStart + 1);
                using var doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("moduleResults", out var moduleResultsArray))
                {
                    AddProtocolErrorIssue(result, "AI返回JSON中未找到moduleResults字段");
                    return;
                }

                var moduleCount = moduleResultsArray.GetArrayLength();
                if (moduleCount != ValidationRules.TotalRuleCount)
                {
                    AddProtocolErrorIssue(result, $"moduleResults应为{ValidationRules.TotalRuleCount}项，实际为{moduleCount}项");
                }

                ApplyModuleResults(result, moduleResultsArray);
            }
            catch (Exception ex)
            {
                AddProtocolErrorIssue(result, $"解析AI校验结果失败: {ex.Message}");
            }
        }

        public static string DetermineOverallResult(ChapterValidationResult result)
        {
            ArgumentNullException.ThrowIfNull(result);

            if (result.HasErrors)
                return "失败";
            if (result.HasWarnings)
                return "警告";
            if (result.TotalIssueCount > 0)
                return "警告";
            return "通过";
        }

        internal static void ApplyModuleResults(ChapterValidationResult result, JsonElement moduleResultsArray)
        {
            var parsedModuleNames = new HashSet<string>();

            foreach (var moduleElement in moduleResultsArray.EnumerateArray())
            {
                var moduleName = moduleElement.TryGetProperty("moduleName", out var moduleNameElement)
                    ? moduleNameElement.GetString() ?? "Unknown"
                    : "Unknown";

                if (!ValidationRules.AllModuleNames.Contains(moduleName))
                {
                    AddProtocolErrorIssue(result, $"未知的moduleName: {moduleName}");
                    continue;
                }

                parsedModuleNames.Add(moduleName);

                var moduleResult = moduleElement.TryGetProperty("result", out var resultElement)
                    ? resultElement.GetString() ?? "未校验"
                    : "未校验";

                if (moduleResult == "通过")
                {
                    continue;
                }

                var issues = BuildModuleIssues(moduleName, moduleResult, moduleElement);
                result.IssuesByModule[moduleName] = issues;
            }

            var missingModules = ValidationRules.AllModuleNames.Except(parsedModuleNames).ToList();
            if (missingModules.Count > 0)
            {
                AddProtocolErrorIssue(result, $"缺失模块: {string.Join(", ", missingModules)}");
            }
        }

        private static List<ValidationIssue> BuildModuleIssues(string moduleName, string moduleResult, JsonElement moduleElement)
        {
            var issues = new List<ValidationIssue>();
            var severity = moduleResult == "失败" ? "Error" : "Warning";

            if (moduleElement.TryGetProperty("problemItems", out var problemItems)
                && problemItems.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in problemItems.EnumerateArray())
                {
                    issues.Add(new ValidationIssue
                    {
                        Type = GetOptionalString(item, "reason"),
                        Severity = severity,
                        Message = GetOptionalString(item, "summary"),
                        Suggestion = GetOptionalString(item, "suggestion"),
                        EntityName = string.Empty
                    });
                }
            }

            if (issues.Count > 0)
            {
                return issues;
            }

            var issueDescription = GetOptionalString(moduleElement, "issueDescription");
            var fixSuggestion = GetOptionalString(moduleElement, "fixSuggestion");
            var defaultMessage = moduleResult == "未校验"
                ? $"规则未校验：{ValidationRules.GetDisplayName(moduleName)}"
                : !string.IsNullOrEmpty(issueDescription)
                    ? issueDescription
                    : $"{moduleName}校验{moduleResult}";

            issues.Add(new ValidationIssue
            {
                Type = moduleResult == "未校验" ? "UnvalidatedRule" : "ValidationIssue",
                Severity = severity,
                Message = defaultMessage,
                Suggestion = string.IsNullOrWhiteSpace(fixSuggestion)
                    ? moduleResult == "未校验" ? "补齐对应数据后再执行校验" : string.Empty
                    : fixSuggestion
            });

            return issues;
        }

        private static string GetOptionalString(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property)
                ? property.GetString() ?? string.Empty
                : string.Empty;
        }

        internal static void AddProtocolErrorIssue(ChapterValidationResult result, string message)
        {
            if (!result.IssuesByModule.ContainsKey(SystemModuleName))
            {
                result.IssuesByModule[SystemModuleName] = new List<ValidationIssue>();
            }

            result.IssuesByModule[SystemModuleName].Add(new ValidationIssue
            {
                Type = "ProtocolError",
                Severity = "Warning",
                Message = $"AI协议错误：{message}"
            });
        }
    }
}
