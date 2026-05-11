using TM.Services.Modules.ProjectData.Implementations;
using UnifiedChapterValidationResult = TM.Services.Modules.ProjectData.Interfaces.ChapterValidationResult;
using UnifiedValidationIssue = TM.Services.Modules.ProjectData.Interfaces.ValidationIssue;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ChapterAIValidationExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_builds_prompt_with_known_structural_issues_and_parses_successful_ai_content()
    {
        IReadOnlyList<UnifiedValidationIssue>? capturedKnownIssues = null;
        var executor = new ChapterAIValidationExecutor(
            (result, content, knownIssues, _) =>
            {
                capturedKnownIssues = knownIssues;
                Assert.Equal("vol1_ch1", result.ChapterId);
                Assert.Equal("正文", content);
                return Task.FromResult("prompt");
            },
            (prompt, _) =>
            {
                Assert.Equal("prompt", prompt);
                return Task.FromResult(ChapterAIValidationResponse.Success(BatchChapterValidationResultParserTestsPayload.SingleModuleIssue("PlotConsistency", "警告", "节奏偏慢")));
            });
        var result = new UnifiedChapterValidationResult
        {
            ChapterId = "vol1_ch1",
            IssuesByModule =
            {
                ["StructuralConsistency"] =
                [
                    new UnifiedValidationIssue { Type = "MissingEntity", Severity = "Warning", Message = "实体缺失" }
                ]
            }
        };

        await executor.ExecuteAsync(result, "正文", CancellationToken.None);

        Assert.Equal("实体缺失", Assert.Single(capturedKnownIssues!).Message);
        var plotIssue = Assert.Single(result.IssuesByModule["PlotConsistency"]);
        Assert.Equal("Warning", plotIssue.Severity);
        Assert.Equal("节奏偏慢", plotIssue.Message);
    }

    [Fact]
    public async Task ExecuteAsync_records_warning_when_ai_response_fails_or_has_no_content()
    {
        var executor = new ChapterAIValidationExecutor(
            (_, _, _, _) => Task.FromResult("prompt"),
            (_, _) => Task.FromResult(ChapterAIValidationResponse.Failure("timeout")));
        var result = new UnifiedChapterValidationResult { ChapterId = "vol1_ch2" };

        await executor.ExecuteAsync(result, "正文", CancellationToken.None);

        var issue = Assert.Single(result.IssuesByModule["System"]);
        Assert.Equal("AIValidationFailed", issue.Type);
        Assert.Equal("Warning", issue.Severity);
        Assert.Equal("AI校验失败，未执行校验。", issue.Message);
    }

    [Fact]
    public async Task ExecuteAsync_records_warning_when_prompt_or_ai_delegate_throws()
    {
        var executor = new ChapterAIValidationExecutor(
            (_, _, _, _) => throw new InvalidOperationException("上下文缺失"),
            (_, _) => throw new InvalidOperationException("should not run"));
        var result = new UnifiedChapterValidationResult { ChapterId = "vol1_ch3" };

        await executor.ExecuteAsync(result, "正文", CancellationToken.None);

        var issue = Assert.Single(result.IssuesByModule["System"]);
        Assert.Equal("AIValidationException", issue.Type);
        Assert.Equal("Warning", issue.Severity);
        Assert.Equal("AI校验异常：上下文缺失，未执行校验。", issue.Message);
    }

    [Fact]
    public async Task ExecuteAsync_honors_cancellation_before_prompt_build()
    {
        var executor = new ChapterAIValidationExecutor(
            (_, _, _, _) => Task.FromResult("prompt"),
            (_, _) => Task.FromResult(ChapterAIValidationResponse.Failure("cancelled")));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(new UnifiedChapterValidationResult(), "正文", cts.Token));
    }

    private static class BatchChapterValidationResultParserTestsPayload
    {
        public static string SingleModuleIssue(string moduleName, string result, string summary)
        {
            var modules = TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary.ValidationRules.AllModuleNames
                .Select(name => name == moduleName
                    ? (object)new
                    {
                        moduleName = name,
                        result,
                        problemItems = new[]
                        {
                            new { reason = "Executor", summary, suggestion = "调整" }
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
}
