using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Guides;
using UnifiedChapterValidationResult = TM.Services.Modules.ProjectData.Interfaces.ChapterValidationResult;
using UnifiedValidationIssue = TM.Services.Modules.ProjectData.Interfaces.ValidationIssue;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ChapterValidationAIWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_builds_prompt_from_context_sources_and_parses_ai_response()
    {
        string? capturedPrompt = null;
        IReadOnlyList<UnifiedValidationIssue>? capturedKnownIssues = null;
        var workflow = new ChapterValidationAIWorkflow(
            (result, content, knownIssues, _) =>
            {
                capturedKnownIssues = knownIssues;
                Assert.Equal("vol1_ch7", result.ChapterId);
                Assert.Contains("命火燃起", content);
                return Task.FromResult(new ChapterValidationPromptSources
                {
                    DesignContextSource = new ChapterValidationDesignContextSource
                    {
                        ContextIds = new ContextIdCollection { TemplateIds = ["tpl-1"] },
                        Templates = [new PromptTemplateContext { Id = "tpl-1", Name = "东方玄幻模板", Genre = "玄幻" }]
                    },
                    PromptContextSource = new ChapterValidationPromptContextSource
                    {
                        ContextIds = new ContextIdCollection { VolumeOutline = "outline-1" },
                        Outlines = [new PromptOutlineContext { Id = "outline-1", OneLineOutline = "少年点燃命火" }]
                    }
                });
            },
            (prompt, _) =>
            {
                capturedPrompt = prompt;
                return Task.FromResult(ChapterAIValidationResponse.Success(SingleModuleIssue("PlotConsistency", "警告", "伏笔过早")));
            });
        var result = new UnifiedChapterValidationResult
        {
            ChapterId = "vol1_ch7",
            ChapterTitle = "第七章 命火",
            VolumeNumber = 1,
            ChapterNumber = 7,
            VolumeName = "第一卷",
            IssuesByModule =
            {
                ["StructuralConsistency"] =
                [
                    new UnifiedValidationIssue { Type = "GateRule", Severity = "Warning", Message = "规则层已确认问题" }
                ]
            }
        };

        await workflow.ExecuteAsync(result, "# 第七章 命火\n命火燃起。", CancellationToken.None);

        Assert.Equal("规则层已确认问题", Assert.Single(capturedKnownIssues!).Message);
        Assert.Contains("<section name=\"创作模板（文风约束）\">", capturedPrompt);
        Assert.Contains("东方玄幻模板: 类型=玄幻", capturedPrompt);
        Assert.Contains("<section name=\"全书大纲\">", capturedPrompt);
        Assert.Contains("一句话大纲=少年点燃命火", capturedPrompt);
        Assert.Contains("<已确认结构性问题>", capturedPrompt);
        Assert.Equal("伏笔过早", Assert.Single(result.IssuesByModule["PlotConsistency"]).Message);
    }

    [Fact]
    public async Task ExecuteAsync_records_ai_validation_exception_when_context_source_loader_fails()
    {
        var workflow = new ChapterValidationAIWorkflow(
            (_, _, _, _) => throw new InvalidOperationException("上下文源缺失"),
            (_, _) => throw new InvalidOperationException("generator should not run"));
        var result = new UnifiedChapterValidationResult { ChapterId = "vol1_ch8" };

        await workflow.ExecuteAsync(result, "正文", CancellationToken.None);

        var issue = Assert.Single(result.IssuesByModule["System"]);
        Assert.Equal("AIValidationException", issue.Type);
        Assert.Equal("Warning", issue.Severity);
        Assert.Equal("AI校验异常：上下文源缺失，未执行校验。", issue.Message);
    }

    [Fact]
    public async Task ExecuteAsync_accepts_empty_context_sources_and_still_sends_validation_prompt()
    {
        string? capturedPrompt = null;
        var workflow = new ChapterValidationAIWorkflow(
            (_, _, _, _) => Task.FromResult(new ChapterValidationPromptSources()),
            (prompt, _) =>
            {
                capturedPrompt = prompt;
                return Task.FromResult(ChapterAIValidationResponse.Failure("timeout"));
            });

        await workflow.ExecuteAsync(
            new UnifiedChapterValidationResult
            {
                ChapterId = "1_1",
                ChapterTitle = "第一章",
                VolumeNumber = 1,
                ChapterNumber = 1,
                VolumeName = "第一卷"
            },
            "正文",
            CancellationToken.None);

        Assert.Contains("<缺失数据说明>", capturedPrompt);
        Assert.Contains("- StyleConsistency（文风模板一致性）", capturedPrompt);
    }

    private static string SingleModuleIssue(string moduleName, string result, string summary)
    {
        var modules = TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary.ValidationRules.AllModuleNames
            .Select(name => name == moduleName
                ? (object)new
                {
                    moduleName = name,
                    result,
                    problemItems = new[]
                    {
                        new { reason = "Workflow", summary, suggestion = "调整" }
                    }
                }
                : new
                {
                    moduleName = name,
                    result = "通过",
                    problemItems = Array.Empty<object>()
                })
            .ToArray();

        return System.Text.Json.JsonSerializer.Serialize(new { moduleResults = modules });
    }
}
