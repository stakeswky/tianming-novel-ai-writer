using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Generated;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;
using UnifiedChapterValidationResult = TM.Services.Modules.ProjectData.Interfaces.ChapterValidationResult;
using UnifiedValidationIssue = TM.Services.Modules.ProjectData.Interfaces.ValidationIssue;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ChapterValidationBatchProcessorTests
{
    [Fact]
    public async Task ProcessAsync_returns_error_result_for_missing_chapter_content()
    {
        var processor = new ChapterValidationBatchProcessor(
            (_, _) => Task.FromResult<string?>(null),
            (_, _, _) => throw new InvalidOperationException("single validator should not run"),
            (_, _, _, _, _) => throw new InvalidOperationException("batch validator should not run"));

        var results = await processor.ProcessAsync(
            [new ChapterInfo { Id = "vol1_ch1", VolumeNumber = 1, ChapterNumber = 1 }],
            "第一卷",
            CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal("vol1_ch1", result.ChapterId);
        Assert.Equal(1, result.VolumeNumber);
        Assert.Equal(1, result.ChapterNumber);
        Assert.Equal("第一卷", result.VolumeName);
        Assert.Equal("失败", result.OverallResult);
        Assert.Equal("章节正文不存在", Assert.Single(result.IssuesByModule["System"]).Message);
    }

    [Fact]
    public async Task ProcessAsync_validates_single_pending_chapter_without_batch_ai()
    {
        var processor = new ChapterValidationBatchProcessor(
            (_, _) => Task.FromResult<string?>("# 第一章 起势\n正文"),
            (result, _, _) =>
            {
                result.IssuesByModule["PlotConsistency"] =
                [
                    new UnifiedValidationIssue { Severity = "Warning", Message = "节奏偏慢" }
                ];
                return Task.CompletedTask;
            },
            (_, _, _, _, _) => throw new InvalidOperationException("batch validator should not run"));

        var results = await processor.ProcessAsync(
            [new ChapterInfo { Id = "vol1_ch2", VolumeNumber = 1, ChapterNumber = 2 }],
            "第一卷",
            CancellationToken.None);

        var result = Assert.Single(results);
        Assert.Equal("第一章 起势", result.ChapterTitle);
        Assert.Equal("警告", result.OverallResult);
        Assert.Equal("节奏偏慢", Assert.Single(result.IssuesByModule["PlotConsistency"]).Message);
    }

    [Fact]
    public async Task ProcessAsync_parses_batch_ai_result_for_multiple_pending_chapters()
    {
        var fallbackCalls = 0;
        var processor = new ChapterValidationBatchProcessor(
            (chapterId, _) => Task.FromResult<string?>($"# {chapterId}\n正文"),
            (_, _, _) =>
            {
                fallbackCalls++;
                return Task.CompletedTask;
            },
            (_, _, _, _, _) => Task.FromResult<string?>(BatchPayload(
                ("PlotConsistency", "警告", "伏笔过早"),
                ("CharacterConsistency", "失败", "角色动机冲突"))));

        var results = await processor.ProcessAsync(
            [
                new ChapterInfo { Id = "vol1_ch1", VolumeNumber = 1, ChapterNumber = 1 },
                new ChapterInfo { Id = "vol1_ch2", VolumeNumber = 1, ChapterNumber = 2 }
            ],
            "第一卷",
            CancellationToken.None);

        Assert.Equal(0, fallbackCalls);
        Assert.Equal("警告", results[0].OverallResult);
        Assert.Equal("失败", results[1].OverallResult);
        Assert.Equal("伏笔过早", Assert.Single(results[0].IssuesByModule["PlotConsistency"]).Message);
        Assert.Equal("角色动机冲突", Assert.Single(results[1].IssuesByModule["CharacterConsistency"]).Message);
    }

    [Fact]
    public async Task ProcessAsync_falls_back_to_single_validation_when_batch_ai_is_empty()
    {
        var fallbackCalls = 0;
        var processor = new ChapterValidationBatchProcessor(
            (chapterId, _) => Task.FromResult<string?>($"## {chapterId}\n正文"),
            (result, _, _) =>
            {
                fallbackCalls++;
                result.IssuesByModule["StyleConsistency"] =
                [
                    new UnifiedValidationIssue { Severity = "Info", Message = "提示信息" }
                ];
                return Task.CompletedTask;
            },
            (_, _, _, _, _) => Task.FromResult<string?>(string.Empty));

        var results = await processor.ProcessAsync(
            [
                new ChapterInfo { Id = "vol2_ch1", VolumeNumber = 2, ChapterNumber = 1 },
                new ChapterInfo { Id = "vol2_ch2", VolumeNumber = 2, ChapterNumber = 2 }
            ],
            "第二卷",
            CancellationToken.None);

        Assert.Equal(2, fallbackCalls);
        Assert.All(results, result => Assert.Equal("警告", result.OverallResult));
    }

    private static string BatchPayload(params (string ModuleName, string Result, string Summary)[] entries)
    {
        var payloadEntries = entries.Select(entry => new
        {
            moduleResults = ValidationRules.AllModuleNames.Select(moduleName => moduleName == entry.ModuleName
                ? (object)new
                {
                    moduleName,
                    result = entry.Result,
                    problemItems = new[]
                    {
                        new { reason = "BatchCheck", summary = entry.Summary, suggestion = "" }
                    }
                }
                : new
                {
                    moduleName,
                    result = "通过",
                    problemItems = Array.Empty<object>()
                }).ToArray()
        }).ToArray();

        return System.Text.Json.JsonSerializer.Serialize(payloadEntries);
    }
}
