using TM.Services.Modules.ProjectData.Implementations;
using UnifiedChapterValidationResult = TM.Services.Modules.ProjectData.Interfaces.ChapterValidationResult;
using UnifiedValidationIssue = TM.Services.Modules.ProjectData.Interfaces.ValidationIssue;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ChapterValidationProcessorTests
{
    [Fact]
    public async Task ProcessAsync_returns_error_result_when_chapter_id_cannot_be_parsed()
    {
        var processor = new ChapterValidationProcessor(
            (_, _) => throw new InvalidOperationException("content loader should not run"),
            (_, _) => throw new InvalidOperationException("volume loader should not run"),
            (_, _, _) => throw new InvalidOperationException("gate should not run"),
            (_, _, _) => throw new InvalidOperationException("AI should not run"));

        var result = await processor.ProcessAsync("bad-id", CancellationToken.None);

        Assert.Equal("bad-id", result.ChapterId);
        Assert.Equal(0, result.VolumeNumber);
        Assert.Equal(0, result.ChapterNumber);
        Assert.Equal("失败", result.OverallResult);
        var issue = Assert.Single(result.IssuesByModule["System"]);
        Assert.Equal("SystemError", issue.Type);
        Assert.Equal("Error", issue.Severity);
        Assert.Equal("无法解析章节ID", issue.Message);
    }

    [Fact]
    public async Task ProcessAsync_returns_error_result_when_chapter_content_is_missing()
    {
        var processor = new ChapterValidationProcessor(
            (_, _) => Task.FromResult<string?>(null),
            (volumeNumber, _) => Task.FromResult($"第{volumeNumber}卷 测试"),
            (_, _, _) => throw new InvalidOperationException("gate should not run"),
            (_, _, _) => throw new InvalidOperationException("AI should not run"));

        var result = await processor.ProcessAsync("vol2_ch5", CancellationToken.None);

        Assert.Equal("vol2_ch5", result.ChapterId);
        Assert.Equal(2, result.VolumeNumber);
        Assert.Equal(5, result.ChapterNumber);
        Assert.Equal("第2卷 测试", result.VolumeName);
        Assert.Equal("失败", result.OverallResult);
        Assert.Equal("章节正文不存在", Assert.Single(result.IssuesByModule["System"]).Message);
    }

    [Fact]
    public async Task ProcessAsync_runs_gate_then_ai_and_sets_overall_result()
    {
        var callOrder = new List<string>();
        UnifiedChapterValidationResult? capturedResult = null;
        string? capturedContent = null;
        var processor = new ChapterValidationProcessor(
            (_, _) => Task.FromResult<string?>("# 第五章 命火试炼\n正文"),
            (_, _) => Task.FromResult("第二卷"),
            (result, content, _) =>
            {
                callOrder.Add("gate");
                capturedResult = result;
                capturedContent = content;
                result.IssuesByModule["StructuralConsistency"] =
                [
                    new UnifiedValidationIssue { Type = "GateRule", Severity = "Warning", Message = "伏笔提前兑现" }
                ];
                return Task.CompletedTask;
            },
            (result, _, _) =>
            {
                callOrder.Add("ai");
                result.IssuesByModule["PlotConsistency"] =
                [
                    new UnifiedValidationIssue { Type = "ValidationIssue", Severity = "Error", Message = "剧情目标冲突" }
                ];
                return Task.CompletedTask;
            });

        var result = await processor.ProcessAsync("vol2_ch5", CancellationToken.None);

        Assert.Equal(["gate", "ai"], callOrder);
        Assert.Same(result, capturedResult);
        Assert.Equal("# 第五章 命火试炼\n正文", capturedContent);
        Assert.Equal("第五章 命火试炼", result.ChapterTitle);
        Assert.Equal(2, result.VolumeNumber);
        Assert.Equal(5, result.ChapterNumber);
        Assert.Equal("第二卷", result.VolumeName);
        Assert.Equal("失败", result.OverallResult);
        Assert.Equal("伏笔提前兑现", Assert.Single(result.IssuesByModule["StructuralConsistency"]).Message);
        Assert.Equal("剧情目标冲突", Assert.Single(result.IssuesByModule["PlotConsistency"]).Message);
    }

    [Fact]
    public async Task ProcessAsync_returns_passed_result_when_no_issues_are_recorded()
    {
        var processor = new ChapterValidationProcessor(
            (_, _) => Task.FromResult<string?>("## 第一章 无题\n正文"),
            (_, _) => Task.FromResult("第一卷"),
            (_, _, _) => Task.CompletedTask,
            (_, _, _) => Task.CompletedTask);

        var result = await processor.ProcessAsync("1_1", CancellationToken.None);

        Assert.Equal("第一章 无题", result.ChapterTitle);
        Assert.Equal(1, result.VolumeNumber);
        Assert.Equal(1, result.ChapterNumber);
        Assert.Equal("通过", result.OverallResult);
    }
}
