using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;
using TM.Services.Modules.ProjectData.Models.Validation;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class FileValidationPersistenceAdapterTests
{
    [Fact]
    public async Task SaveChapterReportAsync_persists_report_through_file_report_store()
    {
        using var workspace = new TempDirectory();
        var adapter = new FileValidationPersistenceAdapter(
            new FileValidationReportStore(Path.Combine(workspace.Path, "reports")),
            new FileValidationSummaryStore(Path.Combine(workspace.Path, "summaries")));

        await adapter.SaveChapterReportAsync(new ValidationReport
        {
            Id = "report-1",
            ChapterId = "vol1_ch1",
            ChapterTitle = "第一章",
            ValidatedTime = new DateTime(2026, 5, 10, 8, 0, 0),
            Result = ValidationResult.Warning
        });

        var reloaded = new FileValidationReportStore(Path.Combine(workspace.Path, "reports"));
        var latest = await reloaded.GetLatestReportAsync("vol1_ch1");

        Assert.NotNull(latest);
        Assert.Equal("report-1", latest.Id);
        Assert.Equal(ValidationResult.Warning, latest.Result);
    }

    [Fact]
    public void SaveVolumeSummary_persists_summary_through_file_summary_store()
    {
        using var workspace = new TempDirectory();
        var adapter = new FileValidationPersistenceAdapter(
            new FileValidationReportStore(Path.Combine(workspace.Path, "reports")),
            new FileValidationSummaryStore(
                Path.Combine(workspace.Path, "summaries"),
                [
                    new ValidationSummaryCategory { Id = "vol-2", Name = "第2卷 试炼", Order = 2 }
                ]));

        adapter.SaveVolumeSummary(2, new ValidationSummaryData
        {
            Id = "summary-1",
            OverallResult = "通过",
            SampledChapterIds = ["vol2_ch1"]
        });

        var reloaded = new FileValidationSummaryStore(
            Path.Combine(workspace.Path, "summaries"),
            [
                new ValidationSummaryCategory { Id = "vol-2", Name = "第2卷 试炼", Order = 2 }
            ]);
        var summary = reloaded.GetDataByVolumeNumber(2);

        Assert.NotNull(summary);
        Assert.Equal("summary-1", summary.Id);
        Assert.Equal("第2卷 试炼", summary.TargetVolumeName);
        Assert.Equal("第2卷 试炼校验", summary.Name);
        Assert.Equal("vol-2", summary.CategoryId);
        Assert.Equal(["vol2_ch1"], summary.SampledChapterIds);
    }

    [Fact]
    public async Task Injected_into_portable_service_persists_chapter_report_and_volume_summary()
    {
        using var workspace = new TempDirectory();
        var reportStore = new FileValidationReportStore(Path.Combine(workspace.Path, "reports"));
        var summaryStore = new FileValidationSummaryStore(Path.Combine(workspace.Path, "summaries"));
        var adapter = new FileValidationPersistenceAdapter(reportStore, summaryStore);
        var service = new PortableUnifiedValidationService(
            (chapterId, _) => Task.FromResult(new TM.Services.Modules.ProjectData.Interfaces.ChapterValidationResult
            {
                ChapterId = chapterId,
                ChapterTitle = "第一章",
                VolumeNumber = 1,
                ChapterNumber = 1,
                VolumeName = "第一卷",
                OverallResult = "通过"
            }),
            volumeNumber => Task.FromResult(new VolumeValidationProcessOutput
            {
                Result = new TM.Services.Modules.ProjectData.Interfaces.VolumeValidationResult
                {
                    VolumeNumber = volumeNumber,
                    VolumeName = "第一卷"
                },
                Summary = new ValidationSummaryData
                {
                    Id = "summary-service",
                    OverallResult = "通过"
                }
            }),
            adapter.SaveChapterReportAsync,
            adapter.SaveVolumeSummary);

        await service.ValidateChapterAsync("vol1_ch1");
        await service.ValidateVolumeAsync(1);

        Assert.NotNull(await reportStore.GetLatestReportAsync("vol1_ch1"));
        Assert.Equal("summary-service", summaryStore.GetDataByVolumeNumber(1)?.Id);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-validation-adapter-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
