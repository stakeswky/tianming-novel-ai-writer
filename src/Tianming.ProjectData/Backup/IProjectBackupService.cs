using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Backup;

public interface IProjectBackupService
{
    Task<string> CreateBackupAsync(string note = "", CancellationToken ct = default);

    Task<IReadOnlyList<BackupEntry>> ListBackupsAsync(CancellationToken ct = default);

    Task<bool> RestoreAsync(string backupId, CancellationToken ct = default);

    Task<bool> DeleteBackupAsync(string backupId, CancellationToken ct = default);
}

public sealed class BackupEntry
{
    public string Id { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public string Note { get; set; } = string.Empty;

    public long SizeBytes { get; set; }
}
