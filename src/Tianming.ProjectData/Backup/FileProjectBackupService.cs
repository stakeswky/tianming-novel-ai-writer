using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Backup;

public sealed class FileProjectBackupService : IProjectBackupService
{
    private static readonly HashSet<string> ExcludedDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        ".backups",
        ".staged",
        ".staging",
        ".wal",
        "bin",
        "obj",
        ".git",
        ".vs",
    };

    private readonly string _projectRoot;
    private readonly string _backupsRoot;

    public FileProjectBackupService(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new ArgumentException("项目根目录不能为空", nameof(projectRoot));

        _projectRoot = projectRoot;
        _backupsRoot = Path.Combine(projectRoot, ".backups");
        Directory.CreateDirectory(_backupsRoot);
    }

    public async Task<string> CreateBackupAsync(string note = "", CancellationToken ct = default)
    {
        var createdAt = DateTime.UtcNow;
        var backupId = createdAt.ToString("yyyyMMdd-HHmmss-fff");
        var backupDirectory = Path.Combine(_backupsRoot, backupId);
        Directory.CreateDirectory(backupDirectory);

        await Task.Run(() => CopyProjectSnapshot(_projectRoot, backupDirectory, ct), ct).ConfigureAwait(false);

        var entry = new BackupEntry
        {
            Id = backupId,
            CreatedAt = createdAt,
            Note = note,
            SizeBytes = ComputeDirectorySize(backupDirectory),
        };

        await File.WriteAllTextAsync(
            Path.Combine(backupDirectory, ".backup-meta.json"),
            JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = true }),
            ct).ConfigureAwait(false);

        return backupId;
    }

    public async Task<IReadOnlyList<BackupEntry>> ListBackupsAsync(CancellationToken ct = default)
    {
        var entries = new List<BackupEntry>();
        if (!Directory.Exists(_backupsRoot))
            return entries;

        foreach (var backupDirectory in Directory.EnumerateDirectories(_backupsRoot))
        {
            ct.ThrowIfCancellationRequested();

            var metaPath = Path.Combine(backupDirectory, ".backup-meta.json");
            if (!File.Exists(metaPath))
                continue;

            try
            {
                var json = await File.ReadAllTextAsync(metaPath, ct).ConfigureAwait(false);
                var entry = JsonSerializer.Deserialize<BackupEntry>(json);
                if (entry != null)
                    entries.Add(entry);
            }
            catch (JsonException)
            {
            }
        }

        return entries
            .OrderByDescending(entry => entry.CreatedAt)
            .ToList();
    }

    public async Task<bool> RestoreAsync(string backupId, CancellationToken ct = default)
    {
        var backupDirectory = Path.Combine(_backupsRoot, backupId);
        if (!Directory.Exists(backupDirectory))
            return false;

        await Task.Run(() =>
        {
            DeleteCurrentProjectContents(ct);
            CopyBackupToProject(backupDirectory, _projectRoot, ct);

            var restoredMetaPath = Path.Combine(_projectRoot, ".backup-meta.json");
            if (File.Exists(restoredMetaPath))
                File.Delete(restoredMetaPath);
        }, ct).ConfigureAwait(false);

        return true;
    }

    public Task<bool> DeleteBackupAsync(string backupId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var backupDirectory = Path.Combine(_backupsRoot, backupId);
        if (!Directory.Exists(backupDirectory))
            return Task.FromResult(false);

        Directory.Delete(backupDirectory, recursive: true);
        return Task.FromResult(true);
    }

    private void CopyProjectSnapshot(string source, string destination, CancellationToken ct)
    {
        CopyTopLevelFiles(source, destination, ct);

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();

            var name = Path.GetFileName(directory);
            if (ExcludedDirectories.Contains(name))
                continue;

            CopyDirectoryRecursive(directory, Path.Combine(destination, name), ct, filterExcludedDirectories: true);
        }
    }

    private void DeleteCurrentProjectContents(CancellationToken ct)
    {
        foreach (var directory in Directory.EnumerateDirectories(_projectRoot, "*", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();

            var name = Path.GetFileName(directory);
            if (string.Equals(name, ".backups", StringComparison.OrdinalIgnoreCase))
                continue;

            Directory.Delete(directory, recursive: true);
        }

        foreach (var file in Directory.EnumerateFiles(_projectRoot, "*", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            File.Delete(file);
        }
    }

    private static void CopyBackupToProject(string source, string destination, CancellationToken ct)
    {
        CopyTopLevelFiles(source, destination, ct);

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            CopyDirectoryRecursive(directory, Path.Combine(destination, Path.GetFileName(directory)), ct, filterExcludedDirectories: false);
        }
    }

    private static void CopyTopLevelFiles(string source, string destination, CancellationToken ct)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }
    }

    private static void CopyDirectoryRecursive(
        string source,
        string destination,
        CancellationToken ct,
        bool filterExcludedDirectories)
    {
        Directory.CreateDirectory(destination);

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        }

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.TopDirectoryOnly))
        {
            ct.ThrowIfCancellationRequested();

            var name = Path.GetFileName(directory);
            if (filterExcludedDirectories && ExcludedDirectories.Contains(name))
                continue;

            CopyDirectoryRecursive(directory, Path.Combine(destination, name), ct, filterExcludedDirectories);
        }
    }

    private static long ComputeDirectorySize(string directory)
    {
        long total = 0;
        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            total += new FileInfo(file).Length;

        return total;
    }
}
