using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TM.Services.Modules.ProjectData.Backup;
using TM.Services.Modules.ProjectData.Packaging;
using TM.Services.Modules.ProjectData.Packaging.Preflight;
using Tianming.Desktop.Avalonia.Infrastructure;

namespace Tianming.Desktop.Avalonia.ViewModels.Packaging;

public sealed partial class PackagingViewModel : ObservableObject
{
    private readonly IPreflightChecker _preflight;
    private readonly IBookExporter _exporter;
    private readonly IProjectBackupService _backup;
    private readonly ICurrentProjectService _project;
    private readonly AppPaths _appPaths;

    public PackagingViewModel(
        IPreflightChecker preflight,
        IBookExporter exporter,
        IProjectBackupService backup,
        ICurrentProjectService project,
        AppPaths appPaths)
    {
        _preflight = preflight;
        _exporter = exporter;
        _backup = backup;
        _project = project;
        _appPaths = appPaths;
    }

    public ObservableCollection<PreflightIssueVm> PreflightIssues { get; } = new();

    public ObservableCollection<BackupEntry> Backups { get; } = new();

    [ObservableProperty]
    private string? _exportStatus;

    [ObservableProperty]
    private string? _backupNote;

    [ObservableProperty]
    private bool _isPreflightPass;

    [RelayCommand]
    private async Task RunPreflightAsync()
    {
        var report = await _preflight.CheckAsync().ConfigureAwait(false);
        ApplyPreflightReport(report);
    }

    [RelayCommand]
    private async Task ExportZipAsync()
    {
        ExportStatus = "预检中…";
        var report = await _preflight.CheckAsync().ConfigureAwait(false);
        ApplyPreflightReport(report);
        if (!report.IsPass)
        {
            ExportStatus = "预检未通过，已阻止导出。";
            return;
        }

        ExportStatus = "导出中…";
        var output = Path.Combine(
            _appPaths.AppSupportDirectory,
            "Exports",
            $"book-{System.DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await _exporter.ExportAsync(_project.ProjectRoot, output).ConfigureAwait(false);
        ExportStatus = $"已导出：{output}";
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        await _backup.CreateBackupAsync(BackupNote ?? string.Empty).ConfigureAwait(false);
        BackupNote = string.Empty;
        await RefreshBackupsAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RefreshBackupsAsync()
    {
        Backups.Clear();
        var backups = await _backup.ListBackupsAsync().ConfigureAwait(false);
        foreach (var backup in backups)
            Backups.Add(backup);
    }

    [RelayCommand]
    private async Task RestoreBackupAsync(string? backupId)
    {
        if (string.IsNullOrWhiteSpace(backupId))
            return;

        await _backup.RestoreAsync(backupId).ConfigureAwait(false);
        await RefreshBackupsAsync().ConfigureAwait(false);
        await RunPreflightAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task DeleteBackupAsync(string? backupId)
    {
        if (string.IsNullOrWhiteSpace(backupId))
            return;

        await _backup.DeleteBackupAsync(backupId).ConfigureAwait(false);
        await RefreshBackupsAsync().ConfigureAwait(false);
    }

    private void ApplyPreflightReport(PreflightReport report)
    {
        PreflightIssues.Clear();
        IsPreflightPass = report.IsPass;

        foreach (var issue in report.Issues)
        {
            PreflightIssues.Add(new PreflightIssueVm
            {
                Code = issue.Code,
                Severity = issue.Severity.ToString(),
                Message = issue.Message,
            });
        }
    }
}

public sealed partial class PreflightIssueVm : ObservableObject
{
    [ObservableProperty]
    private string _code = string.Empty;

    [ObservableProperty]
    private string _severity = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;
}
