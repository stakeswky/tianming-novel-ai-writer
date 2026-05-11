using System.Text.Json;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;
using UnifiedChapterValidationResult = TM.Services.Modules.ProjectData.Interfaces.ChapterValidationResult;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class BatchChapterValidationResultParserTests
{
    [Fact]
    public void ApplyAIContent_maps_array_items_to_results_by_order()
    {
        var results = new[]
        {
            new UnifiedChapterValidationResult { ChapterId = "vol1_ch1" },
            new UnifiedChapterValidationResult { ChapterId = "vol1_ch2" }
        };
        var payload = WrapArray(
            new { moduleResults = FullModules(("PlotConsistency", "警告", "节奏偏慢")) },
            new { moduleResults = FullModules(("CharacterConsistency", "失败", "角色动机冲突")) });

        BatchChapterValidationResultParser.ApplyAIContent(results, payload);

        var plotIssue = Assert.Single(results[0].IssuesByModule["PlotConsistency"]);
        Assert.Equal("Warning", plotIssue.Severity);
        Assert.Equal("节奏偏慢", plotIssue.Message);
        Assert.False(results[0].IssuesByModule.ContainsKey("CharacterConsistency"));

        var characterIssue = Assert.Single(results[1].IssuesByModule["CharacterConsistency"]);
        Assert.Equal("Error", characterIssue.Severity);
        Assert.Equal("角色动机冲突", characterIssue.Message);
        Assert.False(results[1].IssuesByModule.ContainsKey("PlotConsistency"));
    }

    [Fact]
    public void ApplyAIContent_records_per_result_errors_for_missing_items_or_module_results()
    {
        var results = new[]
        {
            new UnifiedChapterValidationResult { ChapterId = "vol2_ch1" },
            new UnifiedChapterValidationResult { ChapterId = "vol2_ch2" },
            new UnifiedChapterValidationResult { ChapterId = "vol2_ch3" }
        };
        var payload = WrapArray(
            new { moduleResults = FullModules(("PlotConsistency", "警告", "伏笔过早")) },
            new { overallResult = "通过" });

        BatchChapterValidationResultParser.ApplyAIContent(results, payload);

        Assert.True(results[0].IssuesByModule.ContainsKey("PlotConsistency"));
        Assert.Contains("批量校验结果缺少moduleResults", Assert.Single(results[1].IssuesByModule["System"]).Message);
        Assert.Contains("批量校验AI返回数组长度不足", Assert.Single(results[2].IssuesByModule["System"]).Message);
    }

    [Fact]
    public void ApplyAIContent_records_format_error_on_every_result_when_array_missing()
    {
        var results = new[]
        {
            new UnifiedChapterValidationResult { ChapterId = "vol3_ch1" },
            new UnifiedChapterValidationResult { ChapterId = "vol3_ch2" }
        };

        BatchChapterValidationResultParser.ApplyAIContent(results, "没有 JSON 数组");

        Assert.All(results, result =>
        {
            var issue = Assert.Single(result.IssuesByModule["System"]);
            Assert.Equal("ProtocolError", issue.Type);
            Assert.Equal("Warning", issue.Severity);
            Assert.Contains("批量校验AI返回格式错误", issue.Message);
        });
    }

    [Fact]
    public void ApplyAIContent_records_parse_failure_on_every_result()
    {
        var results = new[]
        {
            new UnifiedChapterValidationResult { ChapterId = "vol4_ch1" },
            new UnifiedChapterValidationResult { ChapterId = "vol4_ch2" }
        };

        BatchChapterValidationResultParser.ApplyAIContent(results, "[not-json]");

        Assert.All(results, result =>
        {
            var issue = Assert.Single(result.IssuesByModule["System"]);
            Assert.Contains("批量解析失败:", issue.Message);
        });
    }

    private static object[] FullModules((string ModuleName, string Result, string Summary) issueModule)
    {
        var modules = new List<object>();
        foreach (var moduleName in ValidationRules.AllModuleNames)
        {
            modules.Add(moduleName == issueModule.ModuleName
                ? (object)new
                {
                    moduleName,
                    result = issueModule.Result,
                    problemItems = new[]
                    {
                        new
                        {
                            reason = "BatchCheck",
                            summary = issueModule.Summary,
                            suggestion = "调整对应设定"
                        }
                    }
                }
                : new
                {
                    moduleName,
                    result = "通过",
                    problemItems = Array.Empty<object>()
                });
        }

        return modules.ToArray();
    }

    private static string WrapArray(params object[] entries)
    {
        return "AI批量返回：\n" + JsonSerializer.Serialize(entries) + "\n结束";
    }
}
