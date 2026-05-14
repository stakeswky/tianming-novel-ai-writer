using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Backup;
using TM.Services.Modules.ProjectData.Packaging;
using TM.Services.Modules.ProjectData.Packaging.Preflight;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.ViewModels.Packaging;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Packaging;

public class PackagingViewModelTests
{
    [Fact]
    public async Task ExportZipCommand_blocks_export_when_preflight_has_error()
    {
        using var workspace = new TempDirectory();
        var report = new PreflightReport();
        report.Issues.Add(new PreflightIssue
        {
            Code = "NoChapters",
            Severity = PreflightSeverity.Error,
            Message = "missing",
        });
        var preflight = new StubPreflightChecker(report);
        var exporter = new RecordingBookExporter();
        var viewModel = new PackagingViewModel(
            preflight,
            exporter,
            new StubBackupService(),
            new StubCurrentProjectService(workspace.Path),
            new AppPaths(workspace.Path));

        await viewModel.ExportZipCommand.ExecuteAsync(null);

        Assert.Equal(0, exporter.CallCount);
        Assert.Equal("预检未通过，已阻止导出。", viewModel.ExportStatus);
        Assert.False(viewModel.IsPreflightPass);
        Assert.Contains(viewModel.PreflightIssues, issue => issue.Code == "NoChapters");
    }

    [Fact]
    public async Task CreateBackupCommand_refreshes_backup_list_and_clears_note()
    {
        using var workspace = new TempDirectory();
        var backup = new StubBackupService
        {
            Backups =
            [
                new BackupEntry { Id = "bk-001" },
            ],
        };
        var viewModel = new PackagingViewModel(
            new StubPreflightChecker(new PreflightReport()),
            new RecordingBookExporter(),
            backup,
            new StubCurrentProjectService(workspace.Path),
            new AppPaths(workspace.Path))
        {
            BackupNote = "manual snapshot",
        };

        await viewModel.CreateBackupCommand.ExecuteAsync(null);

        Assert.Equal(string.Empty, viewModel.BackupNote);
        Assert.Single(viewModel.Backups);
        Assert.Equal("manual snapshot", backup.LastCreateNote);
    }

    private sealed class StubPreflightChecker(PreflightReport report) : IPreflightChecker
    {
        public Task<PreflightReport> CheckAsync(CancellationToken ct = default) => Task.FromResult(report);
    }

    private sealed class RecordingBookExporter : IBookExporter
    {
        public int CallCount { get; private set; }

        public Task ExportAsync(string projectRoot, string outputZipPath, CancellationToken ct = default)
        {
            CallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class StubBackupService : IProjectBackupService
    {
        public IReadOnlyList<BackupEntry> Backups { get; set; } = [];

        public string? LastCreateNote { get; private set; }

        public Task<string> CreateBackupAsync(string note = "", CancellationToken ct = default)
        {
            LastCreateNote = note;
            return Task.FromResult("bk-001");
        }

        public Task<IReadOnlyList<BackupEntry>> ListBackupsAsync(CancellationToken ct = default)
            => Task.FromResult(Backups);

        public Task<bool> RestoreAsync(string backupId, CancellationToken ct = default)
            => Task.FromResult(true);

        public Task<bool> DeleteBackupAsync(string backupId, CancellationToken ct = default)
            => Task.FromResult(true);
    }

    private sealed class StubCurrentProjectService(string projectRoot) : ICurrentProjectService
    {
        public string ProjectRoot { get; } = projectRoot;
    }

    private sealed class TempDirectory : System.IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tm-pkg-vm-{System.Guid.NewGuid():N}");

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
