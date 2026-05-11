using TM.Framework.Logging;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableLogMaintenanceTests
{
    [Fact]
    public void Cleanup_deletes_oldest_logs_when_strategy_is_by_count()
    {
        using var workspace = new TempDirectory();
        var logDir = Path.Combine(workspace.Path, "Logs");
        Directory.CreateDirectory(logDir);
        var oldest = CreateLog(logDir, "2026-05-08_天命.log", new DateTime(2026, 5, 8));
        var middle = CreateLog(logDir, "2026-05-09_天命.log", new DateTime(2026, 5, 9));
        var newest = CreateLog(logDir, "2026-05-10_天命.log", new DateTime(2026, 5, 10));
        var maintenance = new PortableLogMaintenance(new PortableLogMaintenanceOptions
        {
            StorageRoot = workspace.Path,
            LogDirectory = "Logs",
            Strategy = LogCleanupStrategy.ByCount,
            MaxRetainCount = 2,
            Clock = () => new DateTime(2026, 5, 11)
        });

        var result = maintenance.Cleanup();

        Assert.Equal([oldest], result.DeletedFiles);
        Assert.False(File.Exists(oldest));
        Assert.True(File.Exists(middle));
        Assert.True(File.Exists(newest));
    }

    [Fact]
    public void Cleanup_deletes_logs_older_than_retention_days_when_strategy_is_by_time()
    {
        using var workspace = new TempDirectory();
        var logDir = Path.Combine(workspace.Path, "Logs");
        Directory.CreateDirectory(logDir);
        var oldLog = CreateLog(logDir, "old.log", new DateTime(2026, 5, 1));
        var freshLog = CreateLog(logDir, "fresh.log", new DateTime(2026, 5, 9));
        var maintenance = new PortableLogMaintenance(new PortableLogMaintenanceOptions
        {
            StorageRoot = workspace.Path,
            LogDirectory = "Logs",
            Strategy = LogCleanupStrategy.ByTime,
            MaxRetainDays = 7,
            Clock = () => new DateTime(2026, 5, 10)
        });

        var result = maintenance.Cleanup();

        Assert.Equal([oldLog], result.DeletedFiles);
        Assert.False(File.Exists(oldLog));
        Assert.True(File.Exists(freshLog));
    }

    [Fact]
    public void Cleanup_is_noop_when_disabled_or_directory_missing()
    {
        using var workspace = new TempDirectory();
        var maintenance = new PortableLogMaintenance(new PortableLogMaintenanceOptions
        {
            StorageRoot = workspace.Path,
            LogDirectory = "Missing",
            EnableAutoCleanup = false
        });

        var result = maintenance.Cleanup();

        Assert.Empty(result.DeletedFiles);
        Assert.Equal(0, result.TotalLogFilesBeforeCleanup);
    }

    private static string CreateLog(string directory, string fileName, DateTime lastWriteTime)
    {
        var path = Path.Combine(directory, fileName);
        File.WriteAllText(path, fileName);
        File.SetLastWriteTime(path, lastWriteTime);
        return path;
    }
}
