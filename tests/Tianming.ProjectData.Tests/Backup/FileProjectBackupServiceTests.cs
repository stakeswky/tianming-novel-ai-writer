using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Backup;
using Xunit;

namespace Tianming.ProjectData.Tests.Backup;

public class FileProjectBackupServiceTests
{
    [Fact]
    public async Task CreateBackup_copies_project_to_backups_dir()
    {
        using var workspace = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(workspace.Path, "Generated"));
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "Generated", "ch-001.md"), "Chapter 1");
        var service = new FileProjectBackupService(workspace.Path);

        var backupId = await service.CreateBackupAsync("manual snapshot");

        Assert.False(string.IsNullOrEmpty(backupId));
        var backupDir = Path.Combine(workspace.Path, ".backups", backupId);
        Assert.True(Directory.Exists(backupDir));
        Assert.True(File.Exists(Path.Combine(backupDir, "Generated", "ch-001.md")));
        Assert.True(File.Exists(Path.Combine(backupDir, ".backup-meta.json")));
    }

    [Fact]
    public async Task ListBackups_returns_all_backups_ordered_desc()
    {
        using var workspace = new TempDirectory();
        var service = new FileProjectBackupService(workspace.Path);

        await service.CreateBackupAsync("first");
        await Task.Delay(20);
        await service.CreateBackupAsync("second");

        var backups = await service.ListBackupsAsync();

        Assert.Equal(2, backups.Count);
        Assert.True(backups[0].CreatedAt >= backups[1].CreatedAt);
    }

    [Fact]
    public async Task Restore_replaces_current_project_with_backup()
    {
        using var workspace = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(workspace.Path, "Generated"));
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "Generated", "ch-001.md"), "original");
        var service = new FileProjectBackupService(workspace.Path);
        var backupId = await service.CreateBackupAsync("snapshot");

        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "Generated", "ch-001.md"), "modified");

        await service.RestoreAsync(backupId);

        var content = await File.ReadAllTextAsync(Path.Combine(workspace.Path, "Generated", "ch-001.md"));
        Assert.Equal("original", content);
    }

    [Fact]
    public async Task DeleteBackup_removes_snapshot_directory()
    {
        using var workspace = new TempDirectory();
        var service = new FileProjectBackupService(workspace.Path);
        var backupId = await service.CreateBackupAsync("snapshot");

        var deleted = await service.DeleteBackupAsync(backupId);
        var backups = await service.ListBackupsAsync();

        Assert.True(deleted);
        Assert.Empty(backups);
    }

    [Fact]
    public async Task CreateBackup_excludes_staged_changes_directory()
    {
        using var workspace = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(workspace.Path, ".staged"));
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, ".staged", "stg-001.json"), "{}");
        Directory.CreateDirectory(Path.Combine(workspace.Path, "Generated"));
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "Generated", "ch-001.md"), "Chapter 1");
        var service = new FileProjectBackupService(workspace.Path);

        var backupId = await service.CreateBackupAsync("snapshot");

        var backupDir = Path.Combine(workspace.Path, ".backups", backupId);
        Assert.True(File.Exists(Path.Combine(backupDir, "Generated", "ch-001.md")));
        Assert.False(Directory.Exists(Path.Combine(backupDir, ".staged")));
    }

    [Fact]
    public async Task CreateBackup_excludes_staging_directory()
    {
        using var workspace = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(workspace.Path, ".staging"));
        Directory.CreateDirectory(Path.Combine(workspace.Path, "Generated"));
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, ".staging", "draft.md"), "draft");
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "Generated", "ch-001.md"), "Chapter 1");
        var service = new FileProjectBackupService(workspace.Path);

        var backupId = await service.CreateBackupAsync("snapshot");

        var backupDir = Path.Combine(workspace.Path, ".backups", backupId);
        Assert.True(File.Exists(Path.Combine(backupDir, "Generated", "ch-001.md")));
        Assert.False(Directory.Exists(Path.Combine(backupDir, ".staging")));
    }

    [Fact]
    public async Task CreateBackup_excludes_wal_directory()
    {
        using var workspace = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(workspace.Path, ".wal"));
        Directory.CreateDirectory(Path.Combine(workspace.Path, "Generated"));
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, ".wal", "chapter.jsonl"), "{}");
        await File.WriteAllTextAsync(Path.Combine(workspace.Path, "Generated", "ch-001.md"), "Chapter 1");
        var service = new FileProjectBackupService(workspace.Path);

        var backupId = await service.CreateBackupAsync("snapshot");

        var backupDir = Path.Combine(workspace.Path, ".backups", backupId);
        Assert.True(File.Exists(Path.Combine(backupDir, "Generated", "ch-001.md")));
        Assert.False(Directory.Exists(Path.Combine(backupDir, ".wal")));
    }

    private sealed class TempDirectory : System.IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tm-bk-{System.Guid.NewGuid():N}");

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
