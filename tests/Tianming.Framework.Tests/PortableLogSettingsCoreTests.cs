using TM.Framework.Logging;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableLogSettingsCoreTests
{
    [Fact]
    public void Format_core_generates_text_json_and_xml_previews()
    {
        var core = new PortableLogFormatCore(() => new DateTime(2026, 5, 10, 9, 30, 0, 123));

        var text = core.GeneratePreview(new PortableLogFormatPreviewOptions
        {
            FormatTemplate = "[{timestamp}] [{level}] [{caller}] {message}",
            TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff",
            OutputFormat = PortableLogOutputFormatType.Text
        });
        var json = core.GeneratePreview(new PortableLogFormatPreviewOptions { OutputFormat = PortableLogOutputFormatType.Json });
        var xml = core.GeneratePreview(new PortableLogFormatPreviewOptions { OutputFormat = PortableLogOutputFormatType.Xml });

        Assert.Equal("[2026-05-10 09:30:00.123] [INFO] [LogFormatViewModel.GeneratePreview] 这是一条示例日志消息", text);
        Assert.Contains("\"timestamp\": \"2026-05-10 09:30:00.123\"", json);
        Assert.Contains("<level>INFO</level>", xml);
    }

    [Fact]
    public void Format_core_validates_unknown_fields_unbalanced_braces_and_large_templates()
    {
        var core = new PortableLogFormatCore();

        var results = core.ValidateTemplate("{timestamp} {unknown} {message", customFields: ["tenant"]);

        Assert.Contains(results, result => result.Severity == PortableLogValidationSeverity.Error && result.Message == "未知字段: {unknown}");
        Assert.Contains(results, result => result.Severity == PortableLogValidationSeverity.Error && result.Message == "大括号不匹配");

        var manyFields = core.ValidateTemplate("{timestamp}{level}{message}{caller}{threadid}{processid}{exception}{tenant}{tenant}{tenant}{tenant}", ["tenant"]);
        Assert.Contains(manyFields, result => result.Severity == PortableLogValidationSeverity.Warning && result.Message == "字段数量较多，可能影响性能");
    }

    [Fact]
    public void Level_core_applies_original_presets_and_records_history()
    {
        var core = new PortableLogLevelCore(() => new DateTime(2026, 5, 10, 9, 30, 0), () => "writer");
        var settings = new PortableLogLevelSettings { GlobalLevel = LogSeverity.Trace, MinimumLevel = LogSeverity.Trace };

        var record = core.ApplyPreset(settings, "Production");

        Assert.Equal(LogSeverity.Info, settings.GlobalLevel);
        Assert.Equal(LogSeverity.Warning, settings.MinimumLevel);
        Assert.Equal("预设:生产环境", record.Target);
        Assert.Equal(LogSeverity.Trace, record.OldLevel);
        Assert.Equal(LogSeverity.Info, record.NewLevel);
        Assert.Equal("应用生产环境预设", record.Reason);
        Assert.Equal("writer", record.User);
    }

    [Fact]
    public void Level_core_builds_statistics_with_original_colors()
    {
        var stats = new PortableLogLevelStatistics();
        stats.Increment(LogSeverity.Error);
        stats.Increment(LogSeverity.Error);
        stats.Increment(LogSeverity.Info);

        var items = PortableLogLevelCore.BuildStatistics(stats);

        var error = items.Single(item => item.Level == LogSeverity.Error);
        Assert.Equal(2, error.Count);
        Assert.Equal(66.67, error.Percentage, precision: 2);
        Assert.Equal("#FF0000", error.Color);
        var fatal = items.Single(item => item.Level == LogSeverity.Fatal);
        Assert.Equal(0, fatal.Count);
    }

    [Fact]
    public void Rotation_core_builds_storage_status_prediction_and_cleanup_recommendations()
    {
        var now = new DateTime(2026, 5, 10, 9, 30, 0);
        var core = new PortableLogRotationCore(() => now);
        var settings = new PortableLogRotationSettings { MaxFileSizeMB = 10, MaxRetainCount = 2, MaxRetainDays = 7, CompressAfterDays = 1 };

        var storage = core.BuildStorageSpaceInfo("/", totalBytes: 1000, freeBytes: 50, logsSizeBytes: 200, warningThresholdPercentage: 80, criticalThresholdPercentage: 90);
        var prediction = core.PredictNextRotation(settings, currentLogsTotalSizeMB: 42, []);
        var recommendations = core.GenerateCleanupRecommendations(
            settings,
            [
                new PortableLogFileSnapshot("old.log", now.AddDays(-8), 10 * 1024 * 1024, ".log"),
                new PortableLogFileSnapshot("recent.log", now.AddHours(-2), 5 * 1024 * 1024, ".log"),
                new PortableLogFileSnapshot("middle.log", now.AddDays(-2), 7 * 1024 * 1024, ".log")
            ],
            storage);

        Assert.Equal(PortableLogStorageStatus.Critical, storage.Status);
        Assert.Equal("存储空间严重不足（已使用95.0%）！", storage.StatusMessage);
        Assert.Equal(now.AddDays(1), prediction.PredictedNextRotation);
        Assert.Equal("数据不足，基于配置估算", prediction.RecommendedAction);
        Assert.Equal("紧急清理空间", recommendations[0].Action);
        Assert.Contains(recommendations, r => r.Action == "删除过期日志" && r.FilesToDelete == 1);
        Assert.Contains(recommendations, r => r.Action == "减少日志文件数量" && r.FilesToDelete == 1);
    }
}
