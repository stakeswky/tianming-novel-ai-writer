using System.Text.Json;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;
using UnifiedChapterValidationResult = TM.Services.Modules.ProjectData.Interfaces.ChapterValidationResult;
using UnifiedValidationIssue = TM.Services.Modules.ProjectData.Interfaces.ValidationIssue;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ChapterValidationResultParserTests
{
    [Fact]
    public void ApplyAIContent_maps_problem_items_and_fallback_issues_by_module()
    {
        var result = new UnifiedChapterValidationResult { ChapterId = "vol1_ch9" };
        var modules = ValidationRules.AllModuleNames.Select(moduleName => PassModule(moduleName)).ToList();
        modules[Array.IndexOf(ValidationRules.AllModuleNames, "PlotConsistency")] = new
        {
            moduleName = "PlotConsistency",
            result = "警告",
            problemItems = new[]
            {
                new
                {
                    reason = "ForeshadowingTiming",
                    summary = "伏笔回收早于角色发现线索",
                    suggestion = "把揭示移动到下一节"
                }
            }
        };
        modules[Array.IndexOf(ValidationRules.AllModuleNames, "CharacterConsistency")] = new
        {
            moduleName = "CharacterConsistency",
            result = "失败",
            issueDescription = "主角动机与上一章承诺冲突",
            fixSuggestion = "补一段拒绝交易的内心理由"
        };
        modules[Array.IndexOf(ValidationRules.AllModuleNames, "BlueprintConsistency")] = new
        {
            moduleName = "BlueprintConsistency",
            result = "未校验"
        };

        ChapterValidationResultParser.ApplyAIContent(result, WrapModuleResults(modules));

        var plotIssue = Assert.Single(result.IssuesByModule["PlotConsistency"]);
        Assert.Equal("ForeshadowingTiming", plotIssue.Type);
        Assert.Equal("Warning", plotIssue.Severity);
        Assert.Equal("伏笔回收早于角色发现线索", plotIssue.Message);
        Assert.Equal("把揭示移动到下一节", plotIssue.Suggestion);

        var characterIssue = Assert.Single(result.IssuesByModule["CharacterConsistency"]);
        Assert.Equal("ValidationIssue", characterIssue.Type);
        Assert.Equal("Error", characterIssue.Severity);
        Assert.Equal("主角动机与上一章承诺冲突", characterIssue.Message);
        Assert.Equal("补一段拒绝交易的内心理由", characterIssue.Suggestion);

        var blueprintIssue = Assert.Single(result.IssuesByModule["BlueprintConsistency"]);
        Assert.Equal("UnvalidatedRule", blueprintIssue.Type);
        Assert.Equal("Warning", blueprintIssue.Severity);
        Assert.Equal("规则未校验：章节蓝图一致性", blueprintIssue.Message);
        Assert.Equal("补齐对应数据后再执行校验", blueprintIssue.Suggestion);
        Assert.False(result.IssuesByModule.ContainsKey("StyleConsistency"));
    }

    [Fact]
    public void ApplyAIContent_records_protocol_errors_and_keeps_parseable_modules()
    {
        var result = new UnifiedChapterValidationResult { ChapterId = "vol2_ch3" };
        var modules = new object[]
        {
            PassModule("StyleConsistency"),
            PassModule("UnknownModule")
        };

        ChapterValidationResultParser.ApplyAIContent(result, WrapModuleResults(modules));

        Assert.False(result.IssuesByModule.ContainsKey("StyleConsistency"));
        var protocolMessages = result.IssuesByModule["System"].Select(issue => issue.Message).ToArray();
        Assert.Contains(protocolMessages, message => message.Contains($"moduleResults应为{ValidationRules.TotalRuleCount}项，实际为2"));
        Assert.Contains(protocolMessages, message => message.Contains("未知的moduleName: UnknownModule"));
        Assert.Contains(protocolMessages, message => message.Contains("缺失模块:"));
        Assert.All(result.IssuesByModule["System"], issue =>
        {
            Assert.Equal("ProtocolError", issue.Type);
            Assert.Equal("Warning", issue.Severity);
        });
    }

    [Theory]
    [InlineData("not-json", "AI返回内容中未找到有效JSON")]
    [InlineData("{\"overallResult\":\"通过\"}", "AI返回JSON中未找到moduleResults字段")]
    public void ApplyAIContent_records_protocol_error_for_invalid_payloads(string content, string expectedMessage)
    {
        var result = new UnifiedChapterValidationResult { ChapterId = "vol3_ch1" };

        ChapterValidationResultParser.ApplyAIContent(result, content);

        var issue = Assert.Single(result.IssuesByModule["System"]);
        Assert.Equal("ProtocolError", issue.Type);
        Assert.Equal("Warning", issue.Severity);
        Assert.Contains(expectedMessage, issue.Message);
    }

    [Fact]
    public void DetermineOverallResult_matches_original_error_warning_pass_order()
    {
        var result = new UnifiedChapterValidationResult();

        Assert.Equal("通过", ChapterValidationResultParser.DetermineOverallResult(result));

        result.IssuesByModule["PlotConsistency"] =
        [
            new UnifiedValidationIssue { Severity = "Info", Message = "仅提示" }
        ];
        Assert.Equal("警告", ChapterValidationResultParser.DetermineOverallResult(result));

        result.IssuesByModule["PlotConsistency"][0].Severity = "Warning";
        Assert.Equal("警告", ChapterValidationResultParser.DetermineOverallResult(result));

        result.IssuesByModule["PlotConsistency"][0].Severity = "Error";
        Assert.Equal("失败", ChapterValidationResultParser.DetermineOverallResult(result));
    }

    private static object PassModule(string moduleName)
    {
        return new { moduleName, result = "通过" };
    }

    private static string WrapModuleResults(IEnumerable<object> modules)
    {
        return "AI返回如下：\n" + JsonSerializer.Serialize(new { moduleResults = modules }) + "\n请查收";
    }
}
