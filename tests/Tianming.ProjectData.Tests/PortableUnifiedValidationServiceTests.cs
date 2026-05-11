using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Interfaces;
using TM.Services.Modules.ProjectData.Models.Generated;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;
using TM.Services.Modules.ProjectData.Models.Validation;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class PortableUnifiedValidationServiceTests
{
    [Fact]
    public async Task ValidateChapterAsync_returns_result_and_persists_chapter_report()
    {
        ValidationReport? savedReport = null;
        var service = new PortableUnifiedValidationService(
            (chapterId, _) => Task.FromResult(new ChapterValidationResult
            {
                ChapterId = chapterId,
                ChapterTitle = "第一章 试炼",
                VolumeNumber = 1,
                ChapterNumber = 1,
                VolumeName = "第一卷",
                OverallResult = "警告",
                IssuesByModule =
                {
                    ["PlotConsistency"] =
                    [
                        new ValidationIssue
                        {
                            Type = "ValidationIssue",
                            Severity = "Warning",
                            Message = "伏笔过早",
                            Suggestion = "后移伏笔"
                        }
                    ]
                }
            }),
            _ => throw new InvalidOperationException("volume should not run"),
            report =>
            {
                savedReport = report;
                return Task.CompletedTask;
            });

        var result = await service.ValidateChapterAsync("vol1_ch1", CancellationToken.None);

        Assert.Equal("vol1_ch1", result.ChapterId);
        Assert.Equal("警告", result.OverallResult);
        Assert.NotNull(savedReport);
        Assert.Equal("vol1_ch1", savedReport.ChapterId);
        Assert.Equal(ValidationResult.Warning, savedReport.Result);
        Assert.Contains(savedReport.Items, item => item.ValidationType == "PlotConsistency" && item.Result == ValidationItemResult.Warning);
    }

    [Fact]
    public async Task ValidateVolumeAsync_returns_result_and_persists_volume_summary_when_present()
    {
        (int VolumeNumber, ValidationSummaryData Summary)? savedSummary = null;
        var service = new PortableUnifiedValidationService(
            (_, _) => throw new InvalidOperationException("chapter should not run"),
            volumeNumber => Task.FromResult(new VolumeValidationProcessOutput
            {
                Result = new VolumeValidationResult
                {
                    VolumeNumber = volumeNumber,
                    VolumeName = "第一卷",
                    ChapterResults =
                    {
                        new ChapterValidationResult { ChapterId = "vol1_ch1", OverallResult = "通过" }
                    }
                },
                SampledChapters =
                [
                    new ChapterInfo { Id = "vol1_ch1", VolumeNumber = 1, ChapterNumber = 1 }
                ],
                Summary = new ValidationSummaryData
                {
                    TargetVolumeNumber = volumeNumber,
                    TargetVolumeName = "第一卷",
                    OverallResult = "通过",
                    SampledChapterIds = ["vol1_ch1"]
                }
            }),
            saveVolumeSummary: (volumeNumber, summary) =>
            {
                savedSummary = (volumeNumber, summary);
            });

        var result = await service.ValidateVolumeAsync(1, CancellationToken.None);

        Assert.Equal(1, result.VolumeNumber);
        Assert.Equal("第一卷", result.VolumeName);
        Assert.Equal(1, result.TotalChapters);
        Assert.NotNull(savedSummary);
        Assert.Equal(1, savedSummary.Value.VolumeNumber);
        Assert.Equal("通过", savedSummary.Value.Summary.OverallResult);
        Assert.Equal(["vol1_ch1"], savedSummary.Value.Summary.SampledChapterIds);
    }

    [Fact]
    public async Task ValidateVolumeAsync_does_not_persist_summary_when_volume_has_no_summary()
    {
        var saveCalls = 0;
        var service = new PortableUnifiedValidationService(
            (_, _) => throw new InvalidOperationException("chapter should not run"),
            volumeNumber => Task.FromResult(new VolumeValidationProcessOutput
            {
                Result = new VolumeValidationResult { VolumeNumber = volumeNumber, VolumeName = "空卷" },
                Summary = null
            }),
            saveVolumeSummary: (_, _) => saveCalls++);

        var result = await service.ValidateVolumeAsync(3, CancellationToken.None);

        Assert.Equal(3, result.VolumeNumber);
        Assert.Equal("空卷", result.VolumeName);
        Assert.Equal(0, saveCalls);
    }

    [Fact]
    public async Task NeedsRepublishAsync_delegates_to_configured_check()
    {
        var service = new PortableUnifiedValidationService(
            (_, _) => throw new InvalidOperationException("chapter should not run"),
            _ => throw new InvalidOperationException("volume should not run"),
            needsRepublish: () => Task.FromResult(true));

        Assert.True(await service.NeedsRepublishAsync());
    }
}
