using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class FileValidationSummaryStoreTests
{
    [Fact]
    public void SaveVolumeValidation_creates_and_overwrites_volume_summary()
    {
        using var workspace = new TempDirectory();
        var store = new FileValidationSummaryStore(
            Path.Combine(workspace.Path, "data"),
            [
                new ValidationSummaryCategory { Id = "vol-1", Name = "第1卷 青山", Order = 1 }
            ]);

        store.SaveVolumeValidation(1, new ValidationSummaryData
        {
            Id = "first-id",
            OverallResult = "警告",
            ModuleResults =
            {
                new ModuleValidationResult { ModuleName = "PlotConsistency", Result = "警告" }
            }
        });
        store.SaveVolumeValidation(1, new ValidationSummaryData
        {
            OverallResult = "通过",
            ModuleResults =
            {
                new ModuleValidationResult { ModuleName = "PlotConsistency", Result = "通过" }
            }
        });

        var reloaded = new FileValidationSummaryStore(
            Path.Combine(workspace.Path, "data"),
            [
                new ValidationSummaryCategory { Id = "vol-1", Name = "第1卷 青山", Order = 1 }
            ]);
        var summary = Assert.Single(reloaded.GetAllData());

        Assert.Equal("first-id", summary.Id);
        Assert.Equal("第1卷 青山校验", summary.Name);
        Assert.Equal("第1卷 青山", summary.Category);
        Assert.Equal("vol-1", summary.CategoryId);
        Assert.Equal("通过", summary.OverallResult);
        Assert.Equal(1, summary.TargetVolumeNumber);
    }

    [Fact]
    public void Add_update_delete_and_lookup_summary_data()
    {
        using var workspace = new TempDirectory();
        var store = new FileValidationSummaryStore(Path.Combine(workspace.Path, "data"));

        store.AddData(new ValidationSummaryData
        {
            Id = "summary-1",
            Name = "第一卷校验",
            TargetVolumeNumber = 1,
            OverallResult = "警告"
        });
        store.UpdateData(new ValidationSummaryData
        {
            Id = "summary-1",
            Name = "第一卷校验",
            TargetVolumeNumber = 1,
            OverallResult = "失败"
        });

        var reloaded = new FileValidationSummaryStore(Path.Combine(workspace.Path, "data"));
        Assert.Equal("失败", reloaded.GetDataById("summary-1")?.OverallResult);
        Assert.Equal("summary-1", reloaded.GetDataByVolumeNumber(1)?.Id);

        reloaded.DeleteData("summary-1");
        Assert.Empty(new FileValidationSummaryStore(Path.Combine(workspace.Path, "data")).GetAllData());
    }

    [Theory]
    [InlineData("第12卷 风起", 12)]
    [InlineData("第3卷", 3)]
    [InlineData("卷三", -1)]
    public void ParseVolumeNumber_matches_original_category_format(string categoryName, int expected)
    {
        var store = new FileValidationSummaryStore(Path.Combine(Path.GetTempPath(), $"unused-{Guid.NewGuid():N}"));

        Assert.Equal(expected, store.ParseVolumeNumber(categoryName));
    }

    [Fact]
    public void Constructor_ignores_corrupt_summary_json()
    {
        using var workspace = new TempDirectory();
        var dataDir = Path.Combine(workspace.Path, "data");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "bad.json"), "{ not-json");

        var store = new FileValidationSummaryStore(dataDir);

        Assert.Empty(store.GetAllData());
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-validation-summary-{Guid.NewGuid():N}");

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
