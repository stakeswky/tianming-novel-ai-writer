using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TM.Framework.Common.Helpers.Id;
using TM.Services.Modules.ProjectData.Interfaces;
using TM.Services.Modules.ProjectData.Models.Generated;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public static class VolumeValidationSummaryBuilder
    {
        public static ValidationSummaryData Build(
            int volumeNumber,
            string volumeName,
            IEnumerable<ChapterInfo> sampledChapters,
            IEnumerable<ChapterValidationResult> chapterResults,
            IDictionary<string, int>? dependencyModuleVersions = null)
        {
            var sampledChapterList = sampledChapters.ToList();
            var chapterResultList = chapterResults.ToList();
            var moduleResults = new List<ModuleValidationResult>();

            foreach (var moduleName in ValidationRules.AllModuleNames)
            {
                moduleResults.Add(AggregateModuleResult(moduleName, chapterResultList));
            }

            var overallResult = CalculateOverallResult(moduleResults);

            return new ValidationSummaryData
            {
                Id = ShortIdGenerator.New("D"),
                Name = $"第{volumeNumber}卷校验",
                Icon = GetOverallResultIcon(overallResult),
                Category = $"第{volumeNumber}卷",
                TargetVolumeNumber = volumeNumber,
                TargetVolumeName = volumeName,
                SampledChapterCount = sampledChapterList.Count,
                SampledChapterIds = sampledChapterList.Select(chapter => chapter.Id).ToList(),
                LastValidatedTime = DateTime.Now,
                OverallResult = overallResult,
                ModuleResults = moduleResults,
                DependencyModuleVersions = dependencyModuleVersions?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? new Dictionary<string, int>()
            };
        }

        public static string GetOverallResultIcon(string overallResult)
        {
            return overallResult switch
            {
                "通过" => "✅",
                "警告" => "⚠️",
                "失败" => "❌",
                _ => "⏳"
            };
        }

        private static ModuleValidationResult AggregateModuleResult(
            string moduleName,
            List<ChapterValidationResult> chapterResults)
        {
            var allIssues = chapterResults
                .Where(chapter => chapter.IssuesByModule.ContainsKey(moduleName))
                .SelectMany(chapter => chapter.IssuesByModule[moduleName])
                .ToList();

            var problemItems = chapterResults
                .Where(chapter => chapter.IssuesByModule.ContainsKey(moduleName))
                .SelectMany(chapter => chapter.IssuesByModule[moduleName].Select(issue => new ProblemItem
                {
                    Summary = issue.Message,
                    Reason = issue.Type,
                    Details = !string.IsNullOrEmpty(issue.EntityName) ? $"相关实体: {issue.EntityName}" : null,
                    Suggestion = !string.IsNullOrEmpty(issue.Suggestion) ? issue.Suggestion : null,
                    ChapterId = chapter.ChapterId,
                    ChapterTitle = chapter.ChapterTitle
                }))
                .ToList();

            return new ModuleValidationResult
            {
                ModuleName = moduleName,
                DisplayName = ValidationRules.GetDisplayName(moduleName),
                VerificationType = ChapterValidationPromptTemplate.GetVerificationType(moduleName),
                Result = CalculateModuleResult(allIssues),
                IssueDescription = GenerateIssueDescription(allIssues),
                FixSuggestion = GenerateFixSuggestion(allIssues),
                ExtendedDataJson = JsonSerializer.Serialize(GenerateExtendedData(moduleName)),
                ProblemItemsJson = JsonSerializer.Serialize(problemItems)
            };
        }

        private static string CalculateModuleResult(List<ValidationIssue> issues)
        {
            if (issues.Any(issue => issue.Severity == "Error"))
                return "失败";
            if (issues.Any(issue => issue.Severity == "Warning"))
                return "警告";
            if (issues.Count == 0)
                return "通过";
            return "警告";
        }

        private static string CalculateOverallResult(List<ModuleValidationResult> moduleResults)
        {
            if (moduleResults.Any(module => module.Result == "失败"))
                return "失败";
            if (moduleResults.Any(module => module.Result == "警告"))
                return "警告";
            if (moduleResults.All(module => module.Result == "通过"))
                return "通过";
            return "未校验";
        }

        private static string GenerateIssueDescription(List<ValidationIssue> issues)
        {
            if (issues.Count == 0)
                return string.Empty;

            var descriptions = issues
                .Select(issue => issue.Message)
                .Where(message => !string.IsNullOrEmpty(message))
                .Distinct()
                .Take(3);

            return string.Join("; ", descriptions);
        }

        private static string GenerateFixSuggestion(List<ValidationIssue> issues)
        {
            var suggestions = issues
                .Select(issue => issue.Suggestion)
                .Where(suggestion => !string.IsNullOrEmpty(suggestion))
                .Distinct()
                .Take(3);

            return string.Join("; ", suggestions);
        }

        private static Dictionary<string, string> GenerateExtendedData(string moduleName)
        {
            var extendedData = new Dictionary<string, string>();
            foreach (var fieldName in ValidationRules.GetExtendedDataSchema(moduleName))
            {
                var camelCaseName = char.ToLowerInvariant(fieldName[0]) + fieldName.Substring(1);
                extendedData[camelCaseName] = string.Empty;
            }

            return extendedData;
        }
    }
}
