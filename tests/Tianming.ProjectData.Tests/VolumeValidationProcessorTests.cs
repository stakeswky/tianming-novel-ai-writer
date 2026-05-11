using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Generated;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;
using UnifiedChapterValidationResult = TM.Services.Modules.ProjectData.Interfaces.ChapterValidationResult;
using UnifiedValidationIssue = TM.Services.Modules.ProjectData.Interfaces.ValidationIssue;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class VolumeValidationProcessorTests
{
    [Fact]
    public async Task ProcessAsync_returns_empty_volume_result_when_volume_has_no_chapters()
    {
        var processor = new VolumeValidationProcessor(
            (volumeNumber, _) => Task.FromResult($"第{volumeNumber}卷 空卷"),
            _ => Task.FromResult<IReadOnlyList<ChapterInfo>>(
            [
                new ChapterInfo { Id = "vol1_ch1", VolumeNumber = 1, ChapterNumber = 1 }
            ]),
            (_, _, _) => throw new InvalidOperationException("batch processor should not run"));

        var output = await processor.ProcessAsync(2, CancellationToken.None);

        Assert.Equal(2, output.Result.VolumeNumber);
        Assert.Equal("第2卷 空卷", output.Result.VolumeName);
        Assert.Empty(output.Result.ChapterResults);
        Assert.Empty(output.SampledChapters);
        Assert.Null(output.Summary);
    }

    [Fact]
    public async Task ProcessAsync_samples_target_volume_batches_by_two_and_builds_summary()
    {
        var processedBatches = new List<string[]>();
        var processor = new VolumeValidationProcessor(
            (volumeNumber, _) => Task.FromResult($"第{volumeNumber}卷 试炼"),
            _ => Task.FromResult<IReadOnlyList<ChapterInfo>>(
            [
                new ChapterInfo { Id = "vol1_ch1", VolumeNumber = 1, ChapterNumber = 1 },
                new ChapterInfo { Id = "vol2_ch3", VolumeNumber = 2, ChapterNumber = 3 },
                new ChapterInfo { Id = "vol2_ch1", VolumeNumber = 2, ChapterNumber = 1 },
                new ChapterInfo { Id = "vol2_ch2", VolumeNumber = 2, ChapterNumber = 2 }
            ]),
            (batch, volumeName, _) =>
            {
                processedBatches.Add(batch.Select(chapter => chapter.Id).ToArray());
                return Task.FromResult<IReadOnlyList<UnifiedChapterValidationResult>>(batch.Select(chapter =>
                    new UnifiedChapterValidationResult
                    {
                        ChapterId = chapter.Id,
                        ChapterTitle = $"标题{chapter.ChapterNumber}",
                        VolumeNumber = chapter.VolumeNumber,
                        ChapterNumber = chapter.ChapterNumber,
                        VolumeName = volumeName,
                        OverallResult = chapter.ChapterNumber == 2 ? "警告" : "通过",
                        IssuesByModule = chapter.ChapterNumber == 2
                            ? new Dictionary<string, List<UnifiedValidationIssue>>
                            {
                                ["PlotConsistency"] =
                                [
                                    new UnifiedValidationIssue
                                    {
                                        Type = "ValidationIssue",
                                        Severity = "Warning",
                                        Message = "中段节奏偏慢",
                                        Suggestion = "压缩铺垫"
                                    }
                                ]
                            }
                            : new Dictionary<string, List<UnifiedValidationIssue>>()
                    }).ToList());
            });

        var output = await processor.ProcessAsync(2, CancellationToken.None);

        Assert.Equal([["vol2_ch1", "vol2_ch2"], ["vol2_ch3"]], processedBatches);
        Assert.Equal(["vol2_ch1", "vol2_ch2", "vol2_ch3"], output.SampledChapters.Select(chapter => chapter.Id).ToArray());
        Assert.Equal(["vol2_ch1", "vol2_ch2", "vol2_ch3"], output.Result.ChapterResults.Select(chapter => chapter.ChapterId).ToArray());
        Assert.Equal(2, output.Result.PassedChapters);
        Assert.Equal(1, output.Result.FailedChapters);
        Assert.NotNull(output.Summary);
        Assert.Equal(2, output.Summary.TargetVolumeNumber);
        Assert.Equal("第2卷 试炼", output.Summary.TargetVolumeName);
        Assert.Equal(3, output.Summary.SampledChapterCount);
        Assert.Equal(["vol2_ch1", "vol2_ch2", "vol2_ch3"], output.Summary.SampledChapterIds);
        Assert.Equal("警告", output.Summary.OverallResult);
        var plotModule = output.Summary.ModuleResults.Single(module => module.ModuleName == "PlotConsistency");
        Assert.Equal("警告", plotModule.Result);
        Assert.Contains("中段节奏偏慢", plotModule.IssueDescription);
    }

    [Fact]
    public async Task ProcessAsync_converts_batch_exception_to_error_results_for_that_batch()
    {
        var processor = new VolumeValidationProcessor(
            (_, _) => Task.FromResult("第一卷"),
            _ => Task.FromResult<IReadOnlyList<ChapterInfo>>(
            [
                new ChapterInfo { Id = "vol1_ch1", VolumeNumber = 1, ChapterNumber = 1 },
                new ChapterInfo { Id = "vol1_ch2", VolumeNumber = 1, ChapterNumber = 2 }
            ]),
            (_, _, _) => throw new InvalidOperationException("AI 服务不可用"));

        var output = await processor.ProcessAsync(1, CancellationToken.None);

        Assert.Equal(["vol1_ch1", "vol1_ch2"], output.Result.ChapterResults.Select(chapter => chapter.ChapterId).ToArray());
        Assert.All(output.Result.ChapterResults, result =>
        {
            Assert.Equal("失败", result.OverallResult);
            Assert.Equal("校验异常: AI 服务不可用", Assert.Single(result.IssuesByModule["System"]).Message);
        });
        Assert.NotNull(output.Summary);
        Assert.Equal("通过", output.Summary.ModuleResults.Single(module => module.ModuleName == "PlotConsistency").Result);
    }
}
