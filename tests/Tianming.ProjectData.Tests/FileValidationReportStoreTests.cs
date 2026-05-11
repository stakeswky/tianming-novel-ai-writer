using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Validation;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class FileValidationReportStoreTests
{
    [Fact]
    public async Task Save_and_load_reports_by_chapter_ordered_newest_first()
    {
        using var workspace = new TempDirectory();
        var store = new FileValidationReportStore(Path.Combine(workspace.Path, "reports"));

        await store.SaveReportAsync(new ValidationReport
        {
            Id = "older",
            ChapterId = "vol1_ch1",
            ChapterTitle = "旧报告",
            ValidatedTime = new DateTime(2026, 5, 10, 9, 0, 0),
            Result = ValidationResult.Warning,
            Items =
            {
                new ValidationItem { Id = "i1", Result = ValidationItemResult.Warning, Description = "节奏偏慢" }
            }
        });
        await store.SaveReportAsync(new ValidationReport
        {
            Id = "newer",
            ChapterId = "vol1_ch1",
            ChapterTitle = "新报告",
            ValidatedTime = new DateTime(2026, 5, 10, 10, 0, 0),
            Result = ValidationResult.Pass
        });

        var reports = await store.GetReportsAsync("vol1_ch1");
        var latest = await store.GetLatestReportAsync("vol1_ch1");

        Assert.Equal(["newer", "older"], reports.Select(report => report.Id).ToArray());
        Assert.Equal("newer", latest?.Id);
        Assert.Equal(1, reports[1].WarningCount);
    }

    [Fact]
    public async Task GetAllChapterStatusAsync_returns_latest_status_per_chapter()
    {
        using var workspace = new TempDirectory();
        var store = new FileValidationReportStore(Path.Combine(workspace.Path, "reports"));
        await store.SaveReportAsync(new ValidationReport
        {
            Id = "c1-old",
            ChapterId = "vol1_ch1",
            ValidatedTime = new DateTime(2026, 5, 10, 9, 0, 0),
            Result = ValidationResult.Error
        });
        await store.SaveReportAsync(new ValidationReport
        {
            Id = "c1-new",
            ChapterId = "vol1_ch1",
            ValidatedTime = new DateTime(2026, 5, 10, 10, 0, 0),
            Result = ValidationResult.Pass
        });
        await store.SaveReportAsync(new ValidationReport
        {
            Id = "c2",
            ChapterId = "vol1_ch2",
            ValidatedTime = new DateTime(2026, 5, 10, 11, 0, 0),
            Result = ValidationResult.Warning
        });

        var status = await store.GetAllChapterStatusAsync();

        Assert.Equal(ValidationResult.Pass, status["vol1_ch1"]);
        Assert.Equal(ValidationResult.Warning, status["vol1_ch2"]);
    }

    [Fact]
    public async Task DeleteReportAsync_removes_matching_report_from_nested_chapter_directory()
    {
        using var workspace = new TempDirectory();
        var store = new FileValidationReportStore(Path.Combine(workspace.Path, "reports"));
        await store.SaveReportAsync(new ValidationReport
        {
            Id = "delete-me",
            ChapterId = "vol1_ch3",
            ValidatedTime = new DateTime(2026, 5, 10, 12, 0, 0),
            Result = ValidationResult.Error
        });

        await store.DeleteReportAsync("delete-me");

        Assert.Empty(await store.GetReportsAsync("vol1_ch3"));
        Assert.Empty(await store.GetAllChapterStatusAsync());
    }

    [Fact]
    public async Task Invalid_report_json_is_ignored_when_loading()
    {
        using var workspace = new TempDirectory();
        var reportsRoot = Path.Combine(workspace.Path, "reports");
        var chapterDir = Path.Combine(reportsRoot, "vol1_ch4");
        Directory.CreateDirectory(chapterDir);
        await File.WriteAllTextAsync(Path.Combine(chapterDir, "bad.json"), "{ not-json");
        var store = new FileValidationReportStore(reportsRoot);

        Assert.Empty(await store.GetReportsAsync("vol1_ch4"));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-validation-report-{Guid.NewGuid():N}");

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
