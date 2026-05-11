using System;
using TM.Framework.Common.Helpers.Id;
using TM.Services.Modules.ProjectData.Interfaces;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;
using TM.Services.Modules.ProjectData.Models.Validation;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public static class ValidationReportBuilder
    {
        public static ValidationReport BuildChapterReport(ChapterValidationResult unified)
        {
            ArgumentNullException.ThrowIfNull(unified);

            var report = new ValidationReport
            {
                Id = ShortIdGenerator.New("D"),
                ChapterId = unified.ChapterId,
                ChapterTitle = unified.ChapterTitle,
                ValidatedTime = DateTime.Now
            };

            foreach (var moduleName in ValidationRules.AllModuleNames)
            {
                var displayName = ValidationRules.GetDisplayName(moduleName);

                if (unified.IssuesByModule.TryGetValue(moduleName, out var issues) && issues.Count > 0)
                {
                    foreach (var issue in issues)
                    {
                        report.Items.Add(new ValidationItem
                        {
                            Id = ShortIdGenerator.New("D"),
                            ValidationType = moduleName,
                            Name = displayName,
                            Description = issue.Message,
                            Details = issue.Message,
                            Suggestion = issue.Suggestion,
                            Location = issue.Location,
                            Result = MapItemResult(issue.Severity)
                        });
                    }
                }
                else
                {
                    report.Items.Add(new ValidationItem
                    {
                        Id = ShortIdGenerator.New("D"),
                        ValidationType = moduleName,
                        Name = displayName,
                        Description = $"{displayName}校验通过",
                        Details = string.Empty,
                        Suggestion = string.Empty,
                        Location = string.Empty,
                        Result = ValidationItemResult.Pass
                    });
                }
            }

            report.Result = MapReportResult(unified.OverallResult);
            report.Summary = string.IsNullOrWhiteSpace(unified.OverallResult)
                ? "校验完成"
                : $"校验完成：{unified.OverallResult}（问题数：{unified.TotalIssueCount}）";

            return report;
        }

        public static ValidationReport BuildErrorReport(string chapterId, Exception exception)
        {
            ArgumentNullException.ThrowIfNull(exception);

            return new ValidationReport
            {
                Id = ShortIdGenerator.New("D"),
                ChapterId = chapterId,
                ValidatedTime = DateTime.Now,
                Result = ValidationResult.Error,
                Summary = $"校验异常: {exception.Message}"
            };
        }

        private static ValidationItemResult MapItemResult(string severity)
        {
            return severity switch
            {
                "Error" => ValidationItemResult.Error,
                "Warning" => ValidationItemResult.Warning,
                "Info" => ValidationItemResult.Pass,
                _ => ValidationItemResult.Warning
            };
        }

        private static ValidationResult MapReportResult(string overallResult)
        {
            return overallResult switch
            {
                "通过" => ValidationResult.Pass,
                "警告" => ValidationResult.Warning,
                "失败" => ValidationResult.Error,
                _ => ValidationResult.Error
            };
        }
    }
}
