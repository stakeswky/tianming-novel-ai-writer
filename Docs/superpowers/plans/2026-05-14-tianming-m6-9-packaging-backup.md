# M6.9 打包预检 + 备份 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 完成的书可一键导出为 ZIP（含章节 MD + 设计 JSON + 元数据 + manifest）。整个项目可做"快照备份"（全量复制到 `<project>/.backups/{timestamp}/`）和"恢复"（rollback 到任一快照）。打包前做预检（章节完整性 + 数据一致性）。

**Architecture:** 新增 `IBookExporter` 接口 + `ZipBookExporter` 实现（用 `System.IO.Compression.ZipFile.CreateFromDirectory` 或手工 `ZipArchive`，按 `IFileSelector` 过滤）。新增 `IProjectBackupService` 做全量快照（递归复制项目目录到 `.backups/{utc}/`，排除 `.staging/.wal/.backups/bin/obj`）。新增 `IPreflightChecker` 做导出前检查（缺章/未生成章/有 staged change 等）。UI 入口：`PackagingPage` 替换占位，含"导出 ZIP"/"全量备份"/"历史快照列表"3 个 section。

**Tech Stack:** .NET 8 + `System.IO.Compression` + 复用 `FilePackageManifestStore` / `ChapterContentStore` + xUnit。零新依赖。

**Branch:** `m6-9-packaging-backup`（基于 main）。

**前置条件：** Round 2/3 + M6.2-M6.8 已合并入 main 并基线绿。

---

## Task 0：基线 + worktree

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
git worktree add /Users/jimmy/Downloads/tianming-m6-9 -b m6-9-packaging-backup main
cd /Users/jimmy/Downloads/tianming-m6-9
```

---

## Task 1：IPreflightChecker + DefaultPreflightChecker

**Files:**
- Create: `src/Tianming.ProjectData/Packaging/Preflight/IPreflightChecker.cs`
- Create: `src/Tianming.ProjectData/Packaging/Preflight/DefaultPreflightChecker.cs`
- Test: `tests/Tianming.ProjectData.Tests/Packaging/Preflight/DefaultPreflightCheckerTests.cs`

- [ ] **Step 1.1：测试**

```csharp
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Packaging.Preflight;
using Xunit;

namespace Tianming.ProjectData.Tests.Packaging.Preflight;

public class DefaultPreflightCheckerTests
{
    [Fact]
    public async Task Warns_when_no_chapters_generated()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-pre-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var checker = new DefaultPreflightChecker(root);
        var report = await checker.CheckAsync();
        Assert.Contains(report.Issues, i => i.Code == "NoChapters");
    }

    [Fact]
    public async Task Warns_when_staged_changes_exist()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-pre-{System.Guid.NewGuid():N}");
        var stagedDir = Path.Combine(root, ".staged");
        Directory.CreateDirectory(stagedDir);
        await File.WriteAllTextAsync(Path.Combine(stagedDir, "stg-001.json"), "{}");
        var checker = new DefaultPreflightChecker(root);
        var report = await checker.CheckAsync();
        Assert.Contains(report.Issues, i => i.Code == "StagedChangesPending");
    }

    [Fact]
    public async Task Passes_when_chapters_exist_and_no_staging()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-pre-{System.Guid.NewGuid():N}");
        var chaptersDir = Path.Combine(root, "Generate", "Chapters");
        Directory.CreateDirectory(chaptersDir);
        await File.WriteAllTextAsync(Path.Combine(chaptersDir, "ch-001.md"), "content");
        var checker = new DefaultPreflightChecker(root);
        var report = await checker.CheckAsync();
        Assert.True(report.IsPass);
    }
}
```

- [ ] **Step 1.2：实现**

```csharp
// IPreflightChecker.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Packaging.Preflight
{
    public interface IPreflightChecker
    {
        Task<PreflightReport> CheckAsync(CancellationToken ct = default);
    }

    public sealed class PreflightReport
    {
        public bool IsPass => Issues.Count == 0 || !Issues.Any(i => i.Severity == PreflightSeverity.Error);
        public List<PreflightIssue> Issues { get; } = new();
    }

    public sealed class PreflightIssue
    {
        public string Code { get; set; } = string.Empty;
        public PreflightSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public enum PreflightSeverity { Warning, Error }
}
```

```csharp
// DefaultPreflightChecker.cs
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Packaging.Preflight
{
    public sealed class DefaultPreflightChecker : IPreflightChecker
    {
        private readonly string _projectRoot;

        public DefaultPreflightChecker(string projectRoot)
        {
            _projectRoot = projectRoot;
        }

        public Task<PreflightReport> CheckAsync(CancellationToken ct = default)
        {
            var report = new PreflightReport();

            // 1) 章节存在
            var chaptersDir = Path.Combine(_projectRoot, "Generate", "Chapters");
            var chapters = Directory.Exists(chaptersDir)
                ? Directory.GetFiles(chaptersDir, "*.md", SearchOption.AllDirectories)
                : System.Array.Empty<string>();
            if (chapters.Length == 0)
            {
                report.Issues.Add(new PreflightIssue
                {
                    Code = "NoChapters",
                    Severity = PreflightSeverity.Error,
                    Message = "项目下没有任何已生成的章节（Generate/Chapters/*.md）。",
                });
            }

            // 2) 待审核 staged changes
            var stagedDir = Path.Combine(_projectRoot, ".staged");
            if (Directory.Exists(stagedDir))
            {
                var stagedCount = Directory.GetFiles(stagedDir, "*.json").Length;
                if (stagedCount > 0)
                {
                    report.Issues.Add(new PreflightIssue
                    {
                        Code = "StagedChangesPending",
                        Severity = PreflightSeverity.Warning,
                        Message = $"有 {stagedCount} 个待审核改动未处理（.staged/）。",
                    });
                }
            }

            // 3) Design 目录存在
            var designDir = Path.Combine(_projectRoot, "Design");
            if (!Directory.Exists(designDir))
            {
                report.Issues.Add(new PreflightIssue
                {
                    Code = "NoDesign",
                    Severity = PreflightSeverity.Warning,
                    Message = "Design 目录不存在；导出的 ZIP 将不含设计数据。",
                });
            }

            return Task.FromResult(report);
        }
    }
}
```

- [ ] **Step 1.3：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter DefaultPreflightCheckerTests --nologo -v minimal
git add src/Tianming.ProjectData/Packaging/Preflight/IPreflightChecker.cs \
        src/Tianming.ProjectData/Packaging/Preflight/DefaultPreflightChecker.cs \
        tests/Tianming.ProjectData.Tests/Packaging/Preflight/DefaultPreflightCheckerTests.cs
git commit -m "feat(packaging): M6.9.1 IPreflightChecker + 3 类预检（NoChapters / Staged / NoDesign）"
```

---

## Task 2：IBookExporter + ZipBookExporter

**Files:**
- Create: `src/Tianming.ProjectData/Packaging/IBookExporter.cs`
- Create: `src/Tianming.ProjectData/Packaging/ZipBookExporter.cs`
- Test: `tests/Tianming.ProjectData.Tests/Packaging/ZipBookExporterTests.cs`

- [ ] **Step 2.1：测试**

```csharp
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Packaging;
using Xunit;

namespace Tianming.ProjectData.Tests.Packaging;

public class ZipBookExporterTests
{
    [Fact]
    public async Task Export_creates_zip_containing_chapters_and_design()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-exp-{System.Guid.NewGuid():N}");
        var chaptersDir = Path.Combine(root, "Generate", "Chapters");
        var designDir = Path.Combine(root, "Design", "Elements", "Characters");
        Directory.CreateDirectory(chaptersDir);
        Directory.CreateDirectory(designDir);
        await File.WriteAllTextAsync(Path.Combine(chaptersDir, "ch-001.md"), "Chapter 1");
        await File.WriteAllTextAsync(Path.Combine(designDir, "char-001.json"), "{\"Name\":\"x\"}");

        var output = Path.Combine(Path.GetTempPath(), $"tm-out-{System.Guid.NewGuid():N}.zip");
        var exporter = new ZipBookExporter();
        await exporter.ExportAsync(root, output);

        Assert.True(File.Exists(output));
        using var zip = ZipFile.OpenRead(output);
        Assert.Contains(zip.Entries, e => e.FullName.EndsWith("ch-001.md"));
        Assert.Contains(zip.Entries, e => e.FullName.EndsWith("char-001.json"));
        // 不含 .staged/.wal
        Assert.DoesNotContain(zip.Entries, e => e.FullName.Contains(".staged/"));
    }

    [Fact]
    public async Task Export_excludes_blacklisted_directories()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-exp-{System.Guid.NewGuid():N}");
        var wal = Path.Combine(root, ".wal");
        var staged = Path.Combine(root, ".staged");
        var bin = Path.Combine(root, "bin");
        Directory.CreateDirectory(wal);
        Directory.CreateDirectory(staged);
        Directory.CreateDirectory(bin);
        await File.WriteAllTextAsync(Path.Combine(wal, "x.jsonl"), "{}");
        await File.WriteAllTextAsync(Path.Combine(staged, "x.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(bin, "x.dll"), "x");

        var output = Path.Combine(Path.GetTempPath(), $"tm-out-{System.Guid.NewGuid():N}.zip");
        var exporter = new ZipBookExporter();
        await exporter.ExportAsync(root, output);

        using var zip = ZipFile.OpenRead(output);
        Assert.DoesNotContain(zip.Entries, e => e.FullName.Contains(".wal/"));
        Assert.DoesNotContain(zip.Entries, e => e.FullName.Contains(".staged/"));
        Assert.DoesNotContain(zip.Entries, e => e.FullName.Contains("bin/"));
    }
}
```

- [ ] **Step 2.2：实现**

```csharp
// IBookExporter.cs
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Packaging
{
    public interface IBookExporter
    {
        Task ExportAsync(string projectRoot, string outputZipPath, CancellationToken ct = default);
    }
}
```

```csharp
// ZipBookExporter.cs
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Packaging
{
    public sealed class ZipBookExporter : IBookExporter
    {
        private static readonly HashSet<string> ExcludedDirs = new()
        {
            ".staged", ".wal", ".backups", "bin", "obj", ".git", ".vs",
        };

        public Task ExportAsync(string projectRoot, string outputZipPath, CancellationToken ct = default)
        {
            if (File.Exists(outputZipPath)) File.Delete(outputZipPath);
            using var stream = File.Create(outputZipPath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

            foreach (var file in EnumerateFiles(projectRoot))
            {
                ct.ThrowIfCancellationRequested();
                var relative = Path.GetRelativePath(projectRoot, file).Replace('\\', '/');
                archive.CreateEntryFromFile(file, relative, CompressionLevel.Optimal);
            }
            return Task.CompletedTask;
        }

        private IEnumerable<string> EnumerateFiles(string root)
        {
            foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(dir);
                if (ExcludedDirs.Contains(name)) continue;
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    var parts = Path.GetRelativePath(root, f).Split(Path.DirectorySeparatorChar);
                    if (parts.Any(p => ExcludedDirs.Contains(p))) continue;
                    yield return f;
                }
            }
            // 项目根下的散文件（manifest.json 等）
            foreach (var f in Directory.EnumerateFiles(root, "*", SearchOption.TopDirectoryOnly))
                yield return f;
        }
    }
}
```

- [ ] **Step 2.3：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter ZipBookExporterTests --nologo -v minimal
git add src/Tianming.ProjectData/Packaging/IBookExporter.cs \
        src/Tianming.ProjectData/Packaging/ZipBookExporter.cs \
        tests/Tianming.ProjectData.Tests/Packaging/ZipBookExporterTests.cs
git commit -m "feat(packaging): M6.9.2 ZipBookExporter + 黑名单目录排除"
```

---

## Task 3：IProjectBackupService + FileProjectBackupService

**Files:**
- Create: `src/Tianming.ProjectData/Backup/IProjectBackupService.cs`
- Create: `src/Tianming.ProjectData/Backup/FileProjectBackupService.cs`
- Test: `tests/Tianming.ProjectData.Tests/Backup/FileProjectBackupServiceTests.cs`

- [ ] **Step 3.1：测试**

```csharp
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
        var root = Path.Combine(Path.GetTempPath(), $"tm-bk-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "Generate"));
        await File.WriteAllTextAsync(Path.Combine(root, "Generate", "ch-001.md"), "Chapter 1");
        var svc = new FileProjectBackupService(root);

        var backupId = await svc.CreateBackupAsync("manual snapshot");

        Assert.False(string.IsNullOrEmpty(backupId));
        var backupDir = Path.Combine(root, ".backups", backupId);
        Assert.True(Directory.Exists(backupDir));
        Assert.True(File.Exists(Path.Combine(backupDir, "Generate", "ch-001.md")));
    }

    [Fact]
    public async Task ListBackups_returns_all_backups_ordered_desc()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-bk-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var svc = new FileProjectBackupService(root);
        await svc.CreateBackupAsync("first");
        await Task.Delay(20);
        await svc.CreateBackupAsync("second");
        var list = await svc.ListBackupsAsync();
        Assert.Equal(2, list.Count);
        Assert.True(list[0].CreatedAt >= list[1].CreatedAt);
    }

    [Fact]
    public async Task Restore_replaces_current_project_with_backup()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-bk-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "Generate"));
        await File.WriteAllTextAsync(Path.Combine(root, "Generate", "ch-001.md"), "original");
        var svc = new FileProjectBackupService(root);
        var backupId = await svc.CreateBackupAsync("snapshot");

        // 修改原文件
        await File.WriteAllTextAsync(Path.Combine(root, "Generate", "ch-001.md"), "modified");

        await svc.RestoreAsync(backupId);

        var content = await File.ReadAllTextAsync(Path.Combine(root, "Generate", "ch-001.md"));
        Assert.Equal("original", content);
    }
}
```

- [ ] **Step 3.2：实现**

```csharp
// IProjectBackupService.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Backup
{
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
}
```

```csharp
// FileProjectBackupService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Backup
{
    public sealed class FileProjectBackupService : IProjectBackupService
    {
        private static readonly HashSet<string> ExcludedDirs = new()
        {
            ".backups", "bin", "obj", ".git",
        };

        private readonly string _projectRoot;
        private readonly string _backupsDir;

        public FileProjectBackupService(string projectRoot)
        {
            _projectRoot = projectRoot;
            _backupsDir = Path.Combine(projectRoot, ".backups");
            Directory.CreateDirectory(_backupsDir);
        }

        public async Task<string> CreateBackupAsync(string note = "", CancellationToken ct = default)
        {
            var id = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            var dest = Path.Combine(_backupsDir, id);
            Directory.CreateDirectory(dest);

            await Task.Run(() => CopyDirectoryFiltered(_projectRoot, dest), ct).ConfigureAwait(false);

            var metadata = new BackupEntry
            {
                Id = id,
                CreatedAt = DateTime.UtcNow,
                Note = note,
                SizeBytes = DirectorySize(dest),
            };
            await File.WriteAllTextAsync(
                Path.Combine(dest, ".backup-meta.json"),
                JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true }),
                ct).ConfigureAwait(false);

            return id;
        }

        public async Task<IReadOnlyList<BackupEntry>> ListBackupsAsync(CancellationToken ct = default)
        {
            var list = new List<BackupEntry>();
            if (!Directory.Exists(_backupsDir)) return list;
            foreach (var dir in Directory.GetDirectories(_backupsDir))
            {
                var meta = Path.Combine(dir, ".backup-meta.json");
                if (!File.Exists(meta)) continue;
                try
                {
                    var json = await File.ReadAllTextAsync(meta, ct).ConfigureAwait(false);
                    var entry = JsonSerializer.Deserialize<BackupEntry>(json);
                    if (entry != null) list.Add(entry);
                }
                catch (JsonException) { /* skip corrupt */ }
            }
            return list.OrderByDescending(b => b.CreatedAt).ToList();
        }

        public async Task<bool> RestoreAsync(string backupId, CancellationToken ct = default)
        {
            var src = Path.Combine(_backupsDir, backupId);
            if (!Directory.Exists(src)) return false;

            // 先把当前项目除 .backups 外的内容清掉，再把备份内容拷回
            await Task.Run(() =>
            {
                foreach (var dir in Directory.EnumerateDirectories(_projectRoot, "*", SearchOption.TopDirectoryOnly))
                {
                    var name = Path.GetFileName(dir);
                    if (ExcludedDirs.Contains(name)) continue;
                    Directory.Delete(dir, recursive: true);
                }
                foreach (var f in Directory.EnumerateFiles(_projectRoot, "*", SearchOption.TopDirectoryOnly))
                    File.Delete(f);
                CopyDirectory(src, _projectRoot);
                // 删除恢复后的 meta 文件
                var meta = Path.Combine(_projectRoot, ".backup-meta.json");
                if (File.Exists(meta)) File.Delete(meta);
            }, ct).ConfigureAwait(false);

            return true;
        }

        public Task<bool> DeleteBackupAsync(string backupId, CancellationToken ct = default)
        {
            var dir = Path.Combine(_backupsDir, backupId);
            if (!Directory.Exists(dir)) return Task.FromResult(false);
            Directory.Delete(dir, recursive: true);
            return Task.FromResult(true);
        }

        private void CopyDirectoryFiltered(string src, string dst)
        {
            foreach (var dir in Directory.EnumerateDirectories(src, "*", SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(dir);
                if (ExcludedDirs.Contains(name)) continue;
                var target = Path.Combine(dst, name);
                Directory.CreateDirectory(target);
                CopyDirectory(dir, target);
            }
            foreach (var f in Directory.EnumerateFiles(src, "*", SearchOption.TopDirectoryOnly))
                File.Copy(f, Path.Combine(dst, Path.GetFileName(f)), overwrite: true);
        }

        private static void CopyDirectory(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var d in Directory.GetDirectories(src, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(d.Replace(src, dst));
            foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
                File.Copy(f, f.Replace(src, dst), overwrite: true);
        }

        private static long DirectorySize(string dir)
        {
            long total = 0;
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                total += new FileInfo(f).Length;
            return total;
        }
    }
}
```

- [ ] **Step 3.3：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter FileProjectBackupServiceTests --nologo -v minimal
git add src/Tianming.ProjectData/Backup/IProjectBackupService.cs \
        src/Tianming.ProjectData/Backup/FileProjectBackupService.cs \
        tests/Tianming.ProjectData.Tests/Backup/FileProjectBackupServiceTests.cs
git commit -m "feat(backup): M6.9.3 IProjectBackupService 全量快照 + Restore"
```

---

## Task 4：PackagingViewModel + PackagingPage

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Packaging/PackagingViewModel.cs`
- Create: `src/Tianming.Desktop.Avalonia/Views/Packaging/PackagingPage.axaml` + `.axaml.cs`
- Modify: `src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs`（加 Packaging）
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/Shell/LeftNavViewModel.cs`（启用工具组"打包"）
- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`（DI + PageRegistry）
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

- [ ] **Step 4.1：ViewModel**

```csharp
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

public partial class PackagingViewModel : ObservableObject
{
    private readonly IPreflightChecker _preflight;
    private readonly IBookExporter _exporter;
    private readonly IProjectBackupService _backup;
    private readonly ICurrentProjectService _project;
    private readonly AppPaths _appPaths;

    public ObservableCollection<PreflightIssueVm> PreflightIssues { get; } = new();
    public ObservableCollection<BackupEntry> Backups { get; } = new();

    [ObservableProperty] private string? _exportStatus;
    [ObservableProperty] private string? _backupNote;
    [ObservableProperty] private bool _isPreflightPass;

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

    [RelayCommand]
    private async Task RunPreflightAsync()
    {
        PreflightIssues.Clear();
        var report = await _preflight.CheckAsync();
        IsPreflightPass = report.IsPass;
        foreach (var i in report.Issues)
            PreflightIssues.Add(new PreflightIssueVm { Code = i.Code, Severity = i.Severity.ToString(), Message = i.Message });
    }

    [RelayCommand]
    private async Task ExportZipAsync()
    {
        ExportStatus = "导出中…";
        var output = Path.Combine(_appPaths.AppSupportDirectory, "Exports", $"book-{System.DateTime.UtcNow:yyyyMMdd-HHmmss}.zip");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await _exporter.ExportAsync(_project.ProjectRoot, output);
        ExportStatus = $"已导出：{output}";
    }

    [RelayCommand]
    private async Task CreateBackupAsync()
    {
        var id = await _backup.CreateBackupAsync(BackupNote ?? string.Empty);
        BackupNote = string.Empty;
        await RefreshBackupsAsync();
    }

    [RelayCommand]
    private async Task RefreshBackupsAsync()
    {
        Backups.Clear();
        var list = await _backup.ListBackupsAsync();
        foreach (var b in list) Backups.Add(b);
    }

    [RelayCommand]
    private async Task RestoreBackupAsync(string? backupId)
    {
        if (string.IsNullOrEmpty(backupId)) return;
        await _backup.RestoreAsync(backupId);
        await RefreshBackupsAsync();
    }

    [RelayCommand]
    private async Task DeleteBackupAsync(string? backupId)
    {
        if (string.IsNullOrEmpty(backupId)) return;
        await _backup.DeleteBackupAsync(backupId);
        await RefreshBackupsAsync();
    }
}

public partial class PreflightIssueVm : ObservableObject
{
    [ObservableProperty] private string _code = string.Empty;
    [ObservableProperty] private string _severity = string.Empty;
    [ObservableProperty] private string _message = string.Empty;
}
```

- [ ] **Step 4.2：View**

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Tianming.Desktop.Avalonia.ViewModels.Packaging"
             xmlns:bk="using:TM.Services.Modules.ProjectData.Backup"
             x:Class="Tianming.Desktop.Avalonia.Views.Packaging.PackagingPage"
             x:DataType="vm:PackagingViewModel">
  <ScrollViewer>
    <StackPanel Margin="20" Spacing="16">

      <!-- 预检 -->
      <Border Background="{DynamicResource SurfacePanelBrush}" Padding="16">
        <DockPanel>
          <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8">
            <TextBlock Text="① 预检" FontSize="{DynamicResource FontSizeH3}" FontWeight="SemiBold"/>
            <Button Classes="ghost" Content="跑预检" Command="{Binding RunPreflightCommand}"/>
          </StackPanel>
          <ItemsControl ItemsSource="{Binding PreflightIssues}" Margin="0,8,0,0">
            <ItemsControl.ItemTemplate>
              <DataTemplate DataType="vm:PreflightIssueVm">
                <TextBlock Text="{Binding Message}" Foreground="{DynamicResource TextSecondaryBrush}"/>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
        </DockPanel>
      </Border>

      <!-- 导出 -->
      <Border Background="{DynamicResource SurfacePanelBrush}" Padding="16">
        <DockPanel>
          <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8">
            <TextBlock Text="② 导出 ZIP" FontSize="{DynamicResource FontSizeH3}" FontWeight="SemiBold"/>
            <Button Classes="primary" Content="导出全书 ZIP" Command="{Binding ExportZipCommand}"/>
          </StackPanel>
          <TextBlock Text="{Binding ExportStatus}" Margin="0,8,0,0"/>
        </DockPanel>
      </Border>

      <!-- 备份 -->
      <Border Background="{DynamicResource SurfacePanelBrush}" Padding="16">
        <DockPanel>
          <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8">
            <TextBlock Text="③ 备份 + 恢复" FontSize="{DynamicResource FontSizeH3}" FontWeight="SemiBold"/>
            <TextBox Text="{Binding BackupNote, Mode=TwoWay}" Watermark="备注（可选）" Width="240"/>
            <Button Classes="primary" Content="创建快照" Command="{Binding CreateBackupCommand}"/>
            <Button Classes="ghost" Content="刷新" Command="{Binding RefreshBackupsCommand}"/>
          </StackPanel>
          <ItemsControl ItemsSource="{Binding Backups}" Margin="0,8,0,0">
            <ItemsControl.ItemTemplate>
              <DataTemplate DataType="bk:BackupEntry">
                <Border Padding="8" Margin="0,4" Background="{DynamicResource SurfaceCanvasBrush}"
                        CornerRadius="{DynamicResource RadiusSm}">
                  <Grid ColumnDefinitions="*, Auto, Auto">
                    <StackPanel Grid.Column="0">
                      <TextBlock Text="{Binding Id}" FontWeight="SemiBold"/>
                      <TextBlock Text="{Binding Note}" Foreground="{DynamicResource TextTertiaryBrush}"/>
                    </StackPanel>
                    <Button Grid.Column="1" Classes="ghost" Content="恢复"
                            Command="{Binding $parent[ItemsControl].DataContext.RestoreBackupCommand}"
                            CommandParameter="{Binding Id}"/>
                    <Button Grid.Column="2" Classes="ghost" Content="删除" Margin="6,0,0,0"
                            Command="{Binding $parent[ItemsControl].DataContext.DeleteBackupCommand}"
                            CommandParameter="{Binding Id}"/>
                  </Grid>
                </Border>
              </DataTemplate>
            </ItemsControl.ItemTemplate>
          </ItemsControl>
        </DockPanel>
      </Border>

    </StackPanel>
  </ScrollViewer>
</UserControl>
```

`.axaml.cs`:
```csharp
using Avalonia.Controls;
namespace Tianming.Desktop.Avalonia.Views.Packaging;
public partial class PackagingPage : UserControl
{
    public PackagingPage() => InitializeComponent();
}
```

- [ ] **Step 4.3：PageKeys + LeftNav 启用 + DI + DataTemplate**

`PageKeys.cs`:
```csharp
public static readonly PageKey Packaging = new("packaging");
```

`LeftNavViewModel.cs` 工具组中"打包"项改成：
```csharp
new(PageKeys.Packaging, "打包", "package"),
```

`AvaloniaShellServiceCollectionExtensions.cs`:

```csharp
// M6.9 打包 + 备份
s.AddSingleton<IPreflightChecker>(sp =>
    new DefaultPreflightChecker(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
s.AddSingleton<IBookExporter, ZipBookExporter>();
s.AddSingleton<IProjectBackupService>(sp =>
    new FileProjectBackupService(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
s.AddTransient<PackagingViewModel>();
reg.Register<PackagingViewModel, PackagingPage>(PageKeys.Packaging);
```

加 using：
```csharp
using TM.Services.Modules.ProjectData.Backup;
using TM.Services.Modules.ProjectData.Packaging;
using TM.Services.Modules.ProjectData.Packaging.Preflight;
using Tianming.Desktop.Avalonia.ViewModels.Packaging;
using Tianming.Desktop.Avalonia.Views.Packaging;
```

`App.axaml`:
```xml
xmlns:vmpkg="using:Tianming.Desktop.Avalonia.ViewModels.Packaging"
xmlns:vpkg="using:Tianming.Desktop.Avalonia.Views.Packaging"
...
<DataTemplate DataType="vmpkg:PackagingViewModel">
  <vpkg:PackagingPage/>
</DataTemplate>
```

- [ ] **Step 4.4：build + 全量 test**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v minimal
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
```

Expected: 全过。

- [ ] **Step 4.5：commit**

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Packaging/PackagingViewModel.cs \
        src/Tianming.Desktop.Avalonia/Views/Packaging/PackagingPage.axaml \
        src/Tianming.Desktop.Avalonia/Views/Packaging/PackagingPage.axaml.cs \
        src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs \
        src/Tianming.Desktop.Avalonia/ViewModels/Shell/LeftNavViewModel.cs \
        src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs \
        src/Tianming.Desktop.Avalonia/App.axaml
git commit -m "feat(packaging): M6.9.4 PackagingPage + LeftNav 启用 + DI"
```

---

## M6.9 Gate 验收

| 项 | 标准 |
|---|---|
| IPreflightChecker | 3 类 issue（NoChapters / StagedPending / NoDesign）+ IsPass 判定 |
| ZipBookExporter | ZIP 含章节 + 设计，排除 .staged/.wal/.backups/bin/obj |
| FileProjectBackupService | Create / List / Restore / Delete + .backup-meta.json |
| PackagingViewModel | 5 个命令（Preflight / Export / CreateBackup / Restore / Delete）|
| PackagingPage | 3 section（预检 / 导出 / 备份）+ 备份列表 |
| LeftNav 启用 | "打包" IsEnabled=true 可进入 |
| DI | AppHost.Build() 能 resolve 3 个新 service + ViewModel |
| 全量 test | 新增 ≥10 条，dotnet test 全过 |
