using TM.Framework.Logging;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableLogServiceTests
{
    [Theory]
    [InlineData("[Project] 初始化完成", LogSeverity.Debug, "Project", "初始化完成")]
    [InlineData("[AI] 生成失败", LogSeverity.Error, "AI", "生成失败")]
    [InlineData("Fatal crash", LogSeverity.Fatal, null, "Fatal crash")]
    [InlineData("GET /api -> 200", LogSeverity.Debug, null, "GET /api -> 200")]
    [InlineData("Trace detail", LogSeverity.Trace, null, "Trace detail")]
    public void ParseEntry_extracts_module_and_guesses_level(
        string message,
        LogSeverity expectedLevel,
        string? expectedModule,
        string expectedMessage)
    {
        var entry = PortableLogService.ParseEntry(message);

        Assert.Equal(expectedLevel, entry.Level);
        Assert.Equal(expectedModule, entry.Module);
        Assert.Equal(expectedMessage, entry.Message);
    }

    [Fact]
    public void ShouldWrite_applies_global_minimum_and_module_thresholds()
    {
        var settings = new PortableLogLevelSettings
        {
            GlobalLevel = LogSeverity.Info,
            MinimumLevel = LogSeverity.Warning,
            ModuleLevels = { ["AI"] = LogSeverity.Debug }
        };

        Assert.False(PortableLogService.ShouldWrite(LogSeverity.Info, null, settings));
        Assert.True(PortableLogService.ShouldWrite(LogSeverity.Warning, null, settings));
        Assert.False(PortableLogService.ShouldWrite(LogSeverity.Debug, "AI", settings));
        Assert.True(PortableLogService.ShouldWrite(LogSeverity.Warning, "AI", settings));
    }

    [Fact]
    public void Format_supports_timestamp_level_module_thread_process_and_exception()
    {
        var service = new PortableLogService(new PortableLogOptions
        {
            Clock = () => new DateTime(2026, 5, 10, 9, 8, 7, 123),
            FormatSettings = new PortableLogFormatSettings
            {
                FormatTemplate = "{timestamp:HH:mm:ss}|{level:short}|{caller}|{threadid}|{processid}|{message}|{exception}"
            }
        });
        var entry = new PortableLogEntry(LogSeverity.Warning, "AI", "警告内容", new InvalidOperationException("boom"));

        var formatted = service.Format(entry);

        Assert.Contains("09:08:07|WRN|AI|", formatted);
        Assert.Contains("|警告内容|System.InvalidOperationException: boom", formatted);
    }

    [Fact]
    public async Task LogAsync_writes_filtered_formatted_lines_to_daily_file()
    {
        using var workspace = new TempDirectory();
        var service = new PortableLogService(new PortableLogOptions
        {
            StorageRoot = workspace.Path,
            Clock = () => new DateTime(2026, 5, 10, 9, 0, 0),
            LevelSettings = new PortableLogLevelSettings { GlobalLevel = LogSeverity.Warning },
            OutputSettings = new PortableLogOutputSettings
            {
                EnableFileOutput = true,
                FileOutputPath = "Logs/application.log",
                FileNamingPattern = "{date}_{appname}.log"
            },
            FormatSettings = new PortableLogFormatSettings
            {
                FormatTemplate = "[{level:short}] [{caller}] {message}"
            }
        });

        Assert.False(await service.LogAsync("[UI] 初始化完成"));
        Assert.True(await service.LogAsync("[AI] 生成失败"));

        var logPath = Path.Combine(workspace.Path, "Logs", "2026-05-10_天命.log");
        var content = await File.ReadAllTextAsync(logPath);

        Assert.DoesNotContain("初始化完成", content);
        Assert.Contains("[ERR] [AI] 生成失败", content);
    }
}
