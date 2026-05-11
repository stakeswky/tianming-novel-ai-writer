using System.Text.Json;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Generated;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;
using UnifiedChapterValidationResult = TM.Services.Modules.ProjectData.Interfaces.ChapterValidationResult;
using UnifiedValidationIssue = TM.Services.Modules.ProjectData.Interfaces.ValidationIssue;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class VolumeValidationSummaryBuilderTests
{
    [Fact]
    public void Build_aggregates_module_statuses_problem_items_and_volume_metadata()
    {
        var sampledChapters = new[]
        {
            new ChapterInfo { Id = "vol1_ch1", ChapterNumber = 1 },
            new ChapterInfo { Id = "vol1_ch3", ChapterNumber = 3 }
        };
        var chapterResults = new[]
        {
            new UnifiedChapterValidationResult
            {
                ChapterId = "vol1_ch1",
                ChapterTitle = "第一章 命火初现",
                IssuesByModule =
                {
                    ["PlotConsistency"] =
                    [
                        new UnifiedValidationIssue
                        {
                            Type = "ForeshadowingTiming",
                            Severity = "Warning",
                            Message = "伏笔回收略早",
                            Suggestion = "延后一场再揭示",
                            EntityName = "命火"
                        }
                    ]
                }
            },
            new UnifiedChapterValidationResult
            {
                ChapterId = "vol1_ch3",
                ChapterTitle = "第三章 禁地",
                IssuesByModule =
                {
                    ["CharacterConsistency"] =
                    [
                        new UnifiedValidationIssue
                        {
                            Type = "MotivationConflict",
                            Severity = "Error",
                            Message = "主角动机与上一章冲突",
                            Suggestion = "补充拒绝交易的理由",
                            EntityName = "沈天命"
                        },
                        new UnifiedValidationIssue
                        {
                            Type = "MotivationConflict",
                            Severity = "Warning",
                            Message = "主角动机与上一章冲突",
                            Suggestion = "补充拒绝交易的理由"
                        }
                    ]
                }
            }
        };

        var summary = VolumeValidationSummaryBuilder.Build(
            volumeNumber: 1,
            volumeName: "第一卷 试炼",
            sampledChapters,
            chapterResults,
            new Dictionary<string, int> { ["CharacterRules"] = 7 });

        Assert.StartsWith("D", summary.Id);
        Assert.Equal("第1卷校验", summary.Name);
        Assert.Equal("❌", summary.Icon);
        Assert.Equal("第1卷", summary.Category);
        Assert.Equal(1, summary.TargetVolumeNumber);
        Assert.Equal("第一卷 试炼", summary.TargetVolumeName);
        Assert.Equal(2, summary.SampledChapterCount);
        Assert.Equal(["vol1_ch1", "vol1_ch3"], summary.SampledChapterIds);
        Assert.Equal("失败", summary.OverallResult);
        Assert.Equal(ValidationRules.TotalRuleCount, summary.ModuleResults.Count);
        Assert.Equal(7, summary.DependencyModuleVersions["CharacterRules"]);

        var plot = summary.ModuleResults.Single(result => result.ModuleName == "PlotConsistency");
        Assert.Equal("警告", plot.Result);
        Assert.Equal("伏笔回收略早", plot.IssueDescription);
        Assert.Equal("延后一场再揭示", plot.FixSuggestion);

        var plotProblems = JsonSerializer.Deserialize<List<ProblemItem>>(plot.ProblemItemsJson)!;
        var plotProblem = Assert.Single(plotProblems);
        Assert.Equal("伏笔回收略早", plotProblem.Summary);
        Assert.Equal("ForeshadowingTiming", plotProblem.Reason);
        Assert.Equal("相关实体: 命火", plotProblem.Details);
        Assert.Equal("vol1_ch1", plotProblem.ChapterId);
        Assert.Equal("第一章 命火初现", plotProblem.ChapterTitle);

        var character = summary.ModuleResults.Single(result => result.ModuleName == "CharacterConsistency");
        Assert.Equal("失败", character.Result);
        Assert.Equal("主角动机与上一章冲突", character.IssueDescription);
        Assert.Equal("补充拒绝交易的理由", character.FixSuggestion);
        Assert.Equal("角色", character.VerificationType);

        var characterProblems = JsonSerializer.Deserialize<List<ProblemItem>>(character.ProblemItemsJson)!;
        Assert.Equal(2, characterProblems.Count);

        var style = summary.ModuleResults.Single(result => result.ModuleName == "StyleConsistency");
        Assert.Equal("通过", style.Result);
        Assert.Equal("文风模板一致性", style.DisplayName);
        Assert.Equal("文风", style.VerificationType);
        Assert.Equal("[]", style.ProblemItemsJson);
        Assert.Contains("templateName", style.ExtendedDataJson);
    }

    [Theory]
    [InlineData("通过", "✅")]
    [InlineData("警告", "⚠️")]
    [InlineData("失败", "❌")]
    [InlineData("未校验", "⏳")]
    public void GetOverallResultIcon_matches_original_icons(string result, string expected)
    {
        Assert.Equal(expected, VolumeValidationSummaryBuilder.GetOverallResultIcon(result));
    }
}
