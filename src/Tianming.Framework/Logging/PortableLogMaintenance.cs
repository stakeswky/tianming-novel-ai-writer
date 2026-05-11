using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace TM.Framework.Logging;

public enum LogCleanupStrategy
{
    ByCount,
    ByTime,
    BySize
}

public sealed class PortableLogMaintenanceOptions
{
    public bool EnableAutoCleanup { get; set; } = true;
    public string StorageRoot { get; set; } = Directory.GetCurrentDirectory();
    public string LogDirectory { get; set; } = "Logs";
    public LogCleanupStrategy Strategy { get; set; } = LogCleanupStrategy.ByCount;
    public int MaxRetainCount { get; set; } = 7;
    public int MaxRetainDays { get; set; } = 7;
    public Func<DateTime> Clock { get; set; } = () => DateTime.Now;
}

public sealed class LogCleanupResult
{
    public int TotalLogFilesBeforeCleanup { get; set; }
    public List<string> DeletedFiles { get; set; } = new();
}

public sealed class PortableLogMaintenance
{
    private readonly PortableLogMaintenanceOptions _options;

    public PortableLogMaintenance(PortableLogMaintenanceOptions? options = null)
    {
        _options = options ?? new PortableLogMaintenanceOptions();
    }

    public LogCleanupResult Cleanup()
    {
        var result = new LogCleanupResult();
        if (!_options.EnableAutoCleanup)
        {
            return result;
        }

        var directory = ResolveLogDirectory();
        if (!Directory.Exists(directory))
        {
            return result;
        }

        var files = Directory.GetFiles(directory, "*.log")
            .Select(path => new FileInfo(path))
            .OrderByDescending(file => file.LastWriteTime)
            .ToList();
        result.TotalLogFilesBeforeCleanup = files.Count;

        foreach (var file in SelectFilesToDelete(files))
        {
            try
            {
                var fullName = file.FullName;
                file.Delete();
                result.DeletedFiles.Add(fullName);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        return result;
    }

    private IEnumerable<FileInfo> SelectFilesToDelete(IReadOnlyList<FileInfo> files)
    {
        var maxCount = _options.MaxRetainCount > 0 ? _options.MaxRetainCount : 7;
        var maxDays = _options.MaxRetainDays > 0 ? _options.MaxRetainDays : 7;
        var cutoff = _options.Clock().AddDays(-maxDays);

        for (var i = 0; i < files.Count; i++)
        {
            var shouldDelete = _options.Strategy switch
            {
                LogCleanupStrategy.ByCount => i >= maxCount,
                LogCleanupStrategy.ByTime => files[i].LastWriteTime < cutoff,
                LogCleanupStrategy.BySize => i >= maxCount || files[i].LastWriteTime < cutoff,
                _ => false
            };

            if (shouldDelete)
            {
                yield return files[i];
            }
        }
    }

    private string ResolveLogDirectory()
    {
        var directory = string.IsNullOrWhiteSpace(_options.LogDirectory)
            ? "Logs"
            : _options.LogDirectory;

        return Path.IsPathRooted(directory)
            ? directory
            : Path.Combine(_options.StorageRoot, directory);
    }
}
