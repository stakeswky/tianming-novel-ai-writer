using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;
using TM.Services.Modules.ProjectData.Models.Validation;
using UnifiedChapterValidationResult = TM.Services.Modules.ProjectData.Interfaces.ChapterValidationResult;
using UnifiedValidationIssue = TM.Services.Modules.ProjectData.Interfaces.ValidationIssue;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ValidationReportBuilderTests
{
    [Fact]
    public void BuildChapterReport_maps_unified_module_issues_to_report_items()
    {
        var unified = new UnifiedChapterValidationResult
        {
            ChapterId = "vol1_ch8",
            ChapterTitle = "第八章 命火",
            OverallResult = "警告",
            IssuesByModule =
            {
                ["PlotConsistency"] =
                [
                    new UnifiedValidationIssue
                    {
                        Severity = "Warning",
                        Message = "伏笔回收节奏偏早",
                        Suggestion = "延后到试炼结束后再揭示",
                        Location = "第 12 段"
                    }
                ],
                ["CharacterConsistency"] =
                [
                    new UnifiedValidationIssue
                    {
                        Severity = "Error",
                        Message = "角色动机与前文冲突",
                        Suggestion = "补充拒绝结盟的原因",
                        Location = "第 5 段"
                    },
                    new UnifiedValidationIssue
                    {
                        Severity = "Info",
                        Message = "角色口癖延续良好",
                        Suggestion = "",
                        Location = "第 3 段"
                    }
                ]
            }
        };

        var report = ValidationReportBuilder.BuildChapterReport(unified);

        Assert.StartsWith("D", report.Id);
        Assert.Equal("vol1_ch8", report.ChapterId);
        Assert.Equal("第八章 命火", report.ChapterTitle);
        Assert.Equal(ValidationResult.Warning, report.Result);
        Assert.Equal("校验完成：警告（问题数：3）", report.Summary);
        Assert.Equal(ValidationRules.TotalRuleCount - 2 + 3, report.Items.Count);

        var plotItem = Assert.Single(report.Items, item => item.ValidationType == "PlotConsistency");
        Assert.Equal("剧情规则一致性", plotItem.Name);
        Assert.Equal("伏笔回收节奏偏早", plotItem.Description);
        Assert.Equal("伏笔回收节奏偏早", plotItem.Details);
        Assert.Equal("延后到试炼结束后再揭示", plotItem.Suggestion);
        Assert.Equal("第 12 段", plotItem.Location);
        Assert.Equal(ValidationItemResult.Warning, plotItem.Result);

        var characterItems = report.Items.Where(item => item.ValidationType == "CharacterConsistency").ToList();
        Assert.Equal([ValidationItemResult.Error, ValidationItemResult.Pass], characterItems.Select(item => item.Result).ToArray());

        var styleItem = Assert.Single(report.Items, item => item.ValidationType == "StyleConsistency");
        Assert.Equal("文风模板一致性校验通过", styleItem.Description);
        Assert.Equal(ValidationItemResult.Pass, styleItem.Result);
    }

    [Theory]
    [InlineData("通过", ValidationResult.Pass, "校验完成：通过（问题数：0）")]
    [InlineData("失败", ValidationResult.Error, "校验完成：失败（问题数：0）")]
    [InlineData("", ValidationResult.Error, "校验完成")]
    [InlineData("未知", ValidationResult.Error, "校验完成：未知（问题数：0）")]
    public void BuildChapterReport_maps_overall_result_and_summary(string overallResult, ValidationResult expected, string expectedSummary)
    {
        var report = ValidationReportBuilder.BuildChapterReport(new UnifiedChapterValidationResult
        {
            ChapterId = "vol2_ch1",
            ChapterTitle = "第一章 回潮",
            OverallResult = overallResult
        });

        Assert.Equal(expected, report.Result);
        Assert.Equal(expectedSummary, report.Summary);
        Assert.Equal(ValidationRules.TotalRuleCount, report.Items.Count);
        Assert.All(report.Items, item => Assert.Equal(ValidationItemResult.Pass, item.Result));
    }

    [Fact]
    public void BuildErrorReport_returns_original_exception_shape()
    {
        var report = ValidationReportBuilder.BuildErrorReport("vol3_ch2", new InvalidOperationException("AI校验服务未初始化"));

        Assert.StartsWith("D", report.Id);
        Assert.Equal("vol3_ch2", report.ChapterId);
        Assert.Equal(ValidationResult.Error, report.Result);
        Assert.Equal("校验异常: AI校验服务未初始化", report.Summary);
        Assert.Empty(report.Items);
    }
}
