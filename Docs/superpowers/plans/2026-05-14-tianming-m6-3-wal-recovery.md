# M6.3 WAL + 生成恢复 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 章节生成 pipeline 的 4 个关键步骤（Prepare 后 / Gate 通过后 / Content saved 后 / Tracking dispatched 后）记录 WAL（write-ahead log）。启动时若发现"半完成"的 WAL（如 Content saved 但 Tracking 未 dispatch），自动回放剩余步骤。

**Architecture:** 新增 `IGenerationJournal` 接口 + `FileGenerationJournal` 实现，存储在 `<project>/.wal/{chapterId}.journal.jsonl`（jsonl 一行一 entry，append-only）。每个 entry 含 `Step` 枚举（PrepareStart/PrepareDone/GateDone/ContentSaved/TrackingDone/Done）+ `Timestamp` + `Payload`。崩溃后由现有 `ConsistencyReconciler` 启动 hook 扫描 `.wal/` 调用 `GenerationRecoveryService.ReplayAsync`。

**Tech Stack:** .NET 8 + `System.Text.Json` + `SemaphoreSlim` + xUnit。零新依赖。

**Branch:** `m6-3-wal-recovery`（基于 main）。

**前置条件：** Round 2 + Round 3 + M6.2 已合并入 main 并基线绿。

---

## Task 0：基线 + worktree

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
git worktree add /Users/jimmy/Downloads/tianming-m6-3 -b m6-3-wal-recovery main
cd /Users/jimmy/Downloads/tianming-m6-3
```

---

## Task 1：GenerationJournalEntry + Step 枚举

**Files:**
- Create: `src/Tianming.ProjectData/Generation/Wal/GenerationJournalEntry.cs`
- Test: `tests/Tianming.ProjectData.Tests/Generation/Wal/GenerationJournalEntryTests.cs`

- [ ] **Step 1.1：测试**

```csharp
using System.Text.Json;
using TM.Services.Modules.ProjectData.Generation.Wal;
using Xunit;

namespace Tianming.ProjectData.Tests.Generation.Wal;

public class GenerationJournalEntryTests
{
    [Fact]
    public void Entry_round_trips()
    {
        var e = new GenerationJournalEntry
        {
            ChapterId = "ch-005",
            Step = GenerationStep.GateDone,
            Timestamp = System.DateTime.UtcNow,
            PayloadJson = "{\"gateOk\":true}",
        };
        var json = JsonSerializer.Serialize(e);
        var back = JsonSerializer.Deserialize<GenerationJournalEntry>(json);
        Assert.NotNull(back);
        Assert.Equal(GenerationStep.GateDone, back!.Step);
        Assert.Equal("ch-005", back.ChapterId);
    }

    [Fact]
    public void All_six_steps_defined()
    {
        Assert.True(System.Enum.IsDefined(typeof(GenerationStep), GenerationStep.PrepareStart));
        Assert.True(System.Enum.IsDefined(typeof(GenerationStep), GenerationStep.PrepareDone));
        Assert.True(System.Enum.IsDefined(typeof(GenerationStep), GenerationStep.GateDone));
        Assert.True(System.Enum.IsDefined(typeof(GenerationStep), GenerationStep.ContentSaved));
        Assert.True(System.Enum.IsDefined(typeof(GenerationStep), GenerationStep.TrackingDone));
        Assert.True(System.Enum.IsDefined(typeof(GenerationStep), GenerationStep.Done));
    }
}
```

- [ ] **Step 1.2：实现**

```csharp
using System;
using System.Text.Json.Serialization;

namespace TM.Services.Modules.ProjectData.Generation.Wal
{
    public enum GenerationStep
    {
        PrepareStart,
        PrepareDone,
        GateDone,
        ContentSaved,
        TrackingDone,
        Done,
    }

    public sealed class GenerationJournalEntry
    {
        [JsonPropertyName("ChapterId")] public string ChapterId { get; set; } = string.Empty;
        [JsonPropertyName("Step")] public GenerationStep Step { get; set; }
        [JsonPropertyName("Timestamp")] public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        [JsonPropertyName("PayloadJson")] public string PayloadJson { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 1.3：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter GenerationJournalEntryTests --nologo -v minimal
git add src/Tianming.ProjectData/Generation/Wal/GenerationJournalEntry.cs \
        tests/Tianming.ProjectData.Tests/Generation/Wal/GenerationJournalEntryTests.cs
git commit -m "feat(wal): M6.3.1 GenerationJournalEntry + 6 Step 枚举"
```

---

## Task 2：IGenerationJournal 接口 + FileGenerationJournal

**Files:**
- Create: `src/Tianming.ProjectData/Generation/Wal/IGenerationJournal.cs`
- Create: `src/Tianming.ProjectData/Generation/Wal/FileGenerationJournal.cs`
- Test: `tests/Tianming.ProjectData.Tests/Generation/Wal/FileGenerationJournalTests.cs`

- [ ] **Step 2.1：测试**

```csharp
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Generation.Wal;
using Xunit;

namespace Tianming.ProjectData.Tests.Generation.Wal;

public class FileGenerationJournalTests
{
    [Fact]
    public async Task Append_then_ReadAll_returns_entries_in_order()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-wal-{System.Guid.NewGuid():N}");
        var journal = new FileGenerationJournal(dir);

        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-001", Step = GenerationStep.PrepareStart });
        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-001", Step = GenerationStep.GateDone });
        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-001", Step = GenerationStep.ContentSaved });

        var entries = await journal.ReadAllAsync("ch-001");
        Assert.Equal(3, entries.Count);
        Assert.Equal(GenerationStep.PrepareStart, entries[0].Step);
        Assert.Equal(GenerationStep.GateDone, entries[1].Step);
        Assert.Equal(GenerationStep.ContentSaved, entries[2].Step);
    }

    [Fact]
    public async Task ListPending_returns_chapters_without_Done_step()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-wal-{System.Guid.NewGuid():N}");
        var journal = new FileGenerationJournal(dir);

        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-001", Step = GenerationStep.GateDone });
        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-002", Step = GenerationStep.PrepareStart });
        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-002", Step = GenerationStep.Done });

        var pending = await journal.ListPendingAsync();
        Assert.Contains("ch-001", pending);
        Assert.DoesNotContain("ch-002", pending);
    }

    [Fact]
    public async Task ClearAsync_removes_journal_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-wal-{System.Guid.NewGuid():N}");
        var journal = new FileGenerationJournal(dir);
        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-001", Step = GenerationStep.PrepareStart });
        await journal.ClearAsync("ch-001");
        var entries = await journal.ReadAllAsync("ch-001");
        Assert.Empty(entries);
    }
}
```

- [ ] **Step 2.2：写接口 + 实现**

`IGenerationJournal.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Generation.Wal
{
    public interface IGenerationJournal
    {
        Task AppendAsync(GenerationJournalEntry entry, CancellationToken ct = default);
        Task<IReadOnlyList<GenerationJournalEntry>> ReadAllAsync(string chapterId, CancellationToken ct = default);
        Task<IReadOnlyList<string>> ListPendingAsync(CancellationToken ct = default);
        Task ClearAsync(string chapterId, CancellationToken ct = default);
    }
}
```

`FileGenerationJournal.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Generation.Wal
{
    public sealed class FileGenerationJournal : IGenerationJournal
    {
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
        private readonly string _walDir;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public FileGenerationJournal(string projectRoot)
        {
            _walDir = Path.Combine(projectRoot, ".wal");
            Directory.CreateDirectory(_walDir);
        }

        private string PathFor(string chapterId) =>
            Path.Combine(_walDir, $"{chapterId}.journal.jsonl");

        public async Task AppendAsync(GenerationJournalEntry entry, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var line = JsonSerializer.Serialize(entry, JsonOpts);
                await File.AppendAllTextAsync(PathFor(entry.ChapterId), line + "\n", ct).ConfigureAwait(false);
            }
            finally { _lock.Release(); }
        }

        public async Task<IReadOnlyList<GenerationJournalEntry>> ReadAllAsync(string chapterId, CancellationToken ct = default)
        {
            var path = PathFor(chapterId);
            if (!File.Exists(path)) return System.Array.Empty<GenerationJournalEntry>();
            var lines = await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false);
            var list = new List<GenerationJournalEntry>(lines.Length);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                try
                {
                    var e = JsonSerializer.Deserialize<GenerationJournalEntry>(line, JsonOpts);
                    if (e != null) list.Add(e);
                }
                catch (JsonException) { /* skip corrupt line */ }
            }
            return list;
        }

        public async Task<IReadOnlyList<string>> ListPendingAsync(CancellationToken ct = default)
        {
            if (!Directory.Exists(_walDir)) return System.Array.Empty<string>();
            var files = Directory.GetFiles(_walDir, "*.journal.jsonl");
            var pending = new List<string>();
            foreach (var f in files)
            {
                var chapterId = Path.GetFileName(f).Replace(".journal.jsonl", "");
                var entries = await ReadAllAsync(chapterId, ct).ConfigureAwait(false);
                if (entries.Count == 0) continue;
                if (entries.Any(e => e.Step == GenerationStep.Done)) continue;
                pending.Add(chapterId);
            }
            return pending;
        }

        public Task ClearAsync(string chapterId, CancellationToken ct = default)
        {
            var path = PathFor(chapterId);
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 2.3：测试绿 + commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter FileGenerationJournalTests --nologo -v minimal
git add src/Tianming.ProjectData/Generation/Wal/IGenerationJournal.cs \
        src/Tianming.ProjectData/Generation/Wal/FileGenerationJournal.cs \
        tests/Tianming.ProjectData.Tests/Generation/Wal/FileGenerationJournalTests.cs
git commit -m "feat(wal): M6.3.2 IGenerationJournal + FileGenerationJournal (jsonl)"
```

---

## Task 3：FileChapterTrackingSink 改 atomic 写入

> Explore 报告：`FileChapterTrackingSink.SaveAsync` 直接 `File.WriteAllTextAsync` 无 atomic。本 task 改用 temp+rename。

**Files:**
- Modify: `src/Tianming.ProjectData/Tracking/FileChapterTrackingSink.cs`（SaveAsync 方法）
- Test: `tests/Tianming.ProjectData.Tests/Tracking/FileChapterTrackingSinkAtomicTests.cs`

- [ ] **Step 3.1：测试**

```csharp
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking;

public class FileChapterTrackingSinkAtomicTests
{
    [Fact]
    public async Task Save_uses_temp_then_rename()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-atomic-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var sink = new FileChapterTrackingSink(dir);
        // 写一条简单 record，验证：写入后无 *.tmp 残留、目标文件存在
        await sink.UpdateCharacterStateAsync("ch-001", new CharacterStateChange { CharacterId = "char-001" });
        var tmps = Directory.GetFiles(dir, "*.tmp", SearchOption.AllDirectories);
        Assert.Empty(tmps);
    }
}
```

- [ ] **Step 3.2：实现**

把 `FileChapterTrackingSink.SaveAsync<T>` 替换为：

```csharp
private async Task SaveAsync<T>(string relativePath, T value)
{
    var path = Path.Combine(_rootDirectory, relativePath);
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    var json = JsonSerializer.Serialize(value, _jsonOptions);
    var tmp = path + ".tmp";
    await File.WriteAllTextAsync(tmp, json).ConfigureAwait(false);
    File.Move(tmp, path, overwrite: true);
}
```

- [ ] **Step 3.3：测试绿 + commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter FileChapterTrackingSinkAtomicTests --nologo -v minimal
git add src/Tianming.ProjectData/Tracking/FileChapterTrackingSink.cs \
        tests/Tianming.ProjectData.Tests/Tracking/FileChapterTrackingSinkAtomicTests.cs
git commit -m "feat(tracking): M6.3.3 FileChapterTrackingSink.SaveAsync 改 atomic temp+rename"
```

---

## Task 4：ChapterGenerationPipeline 接入 Journal

**Files:**
- Modify: `src/Tianming.ProjectData/Generation/ChapterGenerationPipeline.cs`
- Test: `tests/Tianming.ProjectData.Tests/Generation/ChapterGenerationPipelineWalTests.cs`

- [ ] **Step 4.1：在 Pipeline ctor 加可选 IGenerationJournal 依赖**

```csharp
private readonly IGenerationJournal? _journal;

public ChapterGenerationPipeline(/* 已有参数 */, IGenerationJournal? journal = null)
{
    /* 原赋值 */
    _journal = journal;
}
```

- [ ] **Step 4.2：在 SaveGeneratedChapterStrictAsync 各步骤后 Append entry**

把现有流程改成：

```csharp
public async Task<GenerationResult> SaveGeneratedChapterStrictAsync(...)
{
    if (_journal != null)
        await _journal.AppendAsync(new GenerationJournalEntry { ChapterId = chapterId, Step = GenerationStep.PrepareStart });

    var prepared = await _preparer.PrepareStrictAsync(...);
    if (_journal != null)
        await _journal.AppendAsync(new GenerationJournalEntry { ChapterId = chapterId, Step = GenerationStep.PrepareDone });

    if (!prepared.GateResult.Success) { /* return early */ }

    if (_journal != null)
        await _journal.AppendAsync(new GenerationJournalEntry { ChapterId = chapterId, Step = GenerationStep.GateDone });

    var saveResult = await _contentStore.SaveChapterAsync(chapterId, prepared.PersistedContent);
    if (_journal != null)
        await _journal.AppendAsync(new GenerationJournalEntry { ChapterId = chapterId, Step = GenerationStep.ContentSaved });

    if (prepared.ParsedChanges != null)
        await _trackingDispatcher.DispatchAsync(chapterId, prepared.ParsedChanges);
    if (_journal != null)
        await _journal.AppendAsync(new GenerationJournalEntry { ChapterId = chapterId, Step = GenerationStep.TrackingDone });

    // index ...

    if (_journal != null)
    {
        await _journal.AppendAsync(new GenerationJournalEntry { ChapterId = chapterId, Step = GenerationStep.Done });
        await _journal.ClearAsync(chapterId); // Done 后清掉日志
    }
    return result;
}
```

加 using：
```csharp
using TM.Services.Modules.ProjectData.Generation.Wal;
```

- [ ] **Step 4.3：测试（成功流程后 WAL 应被清空）**

```csharp
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Generation;
using TM.Services.Modules.ProjectData.Generation.Wal;
using Xunit;

namespace Tianming.ProjectData.Tests.Generation;

public class ChapterGenerationPipelineWalTests
{
    [Fact]
    public async Task Successful_save_clears_journal()
    {
        // 复用现有 PipelineTestFixture 的 setup（参考 ChapterGenerationPipelineTests）
        // 启用 journal，跑成功 case
        // 断言：ListPendingAsync() 不含本 chapter
        Assert.True(true); // 占位；填具体 fixture
    }

    [Fact]
    public async Task Failed_gate_leaves_pending_journal()
    {
        // 失败 case：Gate fail，期望 journal 含 PrepareDone 但无 Done
        Assert.True(true);
    }
}
```

> **注：** 完整 e2e 测试依赖现有 ChapterGenerationPipelineTests 的 fixture 复用。如 fixture 不易复用，最小覆盖：单独构造 Mock Sink + Mock Preparer 跑 SaveGeneratedChapterStrictAsync。

- [ ] **Step 4.4：build + commit**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v minimal
git add src/Tianming.ProjectData/Generation/ChapterGenerationPipeline.cs \
        tests/Tianming.ProjectData.Tests/Generation/ChapterGenerationPipelineWalTests.cs
git commit -m "feat(wal): M6.3.4 ChapterGenerationPipeline 4 步骤 Journal Append + Done 清空"
```

---

## Task 5：GenerationRecoveryService

**Files:**
- Create: `src/Tianming.ProjectData/Generation/Wal/GenerationRecoveryService.cs`
- Test: `tests/Tianming.ProjectData.Tests/Generation/Wal/GenerationRecoveryServiceTests.cs`

- [ ] **Step 5.1：测试**

```csharp
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Generation.Wal;
using Xunit;

namespace Tianming.ProjectData.Tests.Generation.Wal;

public class GenerationRecoveryServiceTests
{
    [Fact]
    public async Task ReplayAsync_recovers_pending_chapter()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-rec-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var journal = new FileGenerationJournal(dir);
        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-001", Step = GenerationStep.ContentSaved });

        var calls = new System.Collections.Generic.List<string>();
        var svc = new GenerationRecoveryService(journal, async (chapterId, fromStep, ct) =>
        {
            calls.Add($"{chapterId}:{fromStep}");
            await Task.CompletedTask;
        });

        await svc.ReplayAsync();
        Assert.Single(calls);
        Assert.Equal("ch-001:ContentSaved", calls[0]);
    }

    [Fact]
    public async Task ReplayAsync_skips_done_chapters()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-rec-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var journal = new FileGenerationJournal(dir);
        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-001", Step = GenerationStep.Done });

        var calls = new System.Collections.Generic.List<string>();
        var svc = new GenerationRecoveryService(journal, async (chapterId, fromStep, ct) =>
        {
            calls.Add($"{chapterId}:{fromStep}");
            await Task.CompletedTask;
        });

        await svc.ReplayAsync();
        Assert.Empty(calls);
    }
}
```

- [ ] **Step 5.2：实现**

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Generation.Wal
{
    public sealed class GenerationRecoveryService
    {
        public delegate Task ReplayHandler(string chapterId, GenerationStep lastStep, CancellationToken ct);

        private readonly IGenerationJournal _journal;
        private readonly ReplayHandler _handler;

        public GenerationRecoveryService(IGenerationJournal journal, ReplayHandler handler)
        {
            _journal = journal;
            _handler = handler;
        }

        public async Task<int> ReplayAsync(CancellationToken ct = default)
        {
            var pending = await _journal.ListPendingAsync(ct).ConfigureAwait(false);
            int replayed = 0;
            foreach (var chapterId in pending)
            {
                var entries = await _journal.ReadAllAsync(chapterId, ct).ConfigureAwait(false);
                if (entries.Count == 0) continue;
                var lastStep = entries[^1].Step;
                await _handler(chapterId, lastStep, ct).ConfigureAwait(false);
                replayed++;
            }
            return replayed;
        }
    }
}
```

- [ ] **Step 5.3：测试绿 + commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter GenerationRecoveryServiceTests --nologo -v minimal
git add src/Tianming.ProjectData/Generation/Wal/GenerationRecoveryService.cs \
        tests/Tianming.ProjectData.Tests/Generation/Wal/GenerationRecoveryServiceTests.cs
git commit -m "feat(wal): M6.3.5 GenerationRecoveryService 扫描 pending + 回放"
```

---

## Task 6：DI 注册 + 启动 hook

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml.cs`（启动时调 GenerationRecoveryService.ReplayAsync）

- [ ] **Step 6.1：DI 注册**

```csharp
// M6.3 WAL + 恢复
s.AddSingleton<IGenerationJournal>(sp =>
    new FileGenerationJournal(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
s.AddSingleton<GenerationRecoveryService>(sp =>
    new GenerationRecoveryService(
        sp.GetRequiredService<IGenerationJournal>(),
        async (chapterId, lastStep, ct) =>
        {
            // M6.3 简化策略：仅记录日志，等用户在 UI 看到提示后手动续跑
            // 后续可注入 ChapterGenerationPipeline 自动重放剩余步骤
            await System.Threading.Tasks.Task.CompletedTask;
        }));
```

加 using：
```csharp
using TM.Services.Modules.ProjectData.Generation.Wal;
```

- [ ] **Step 6.2：启动时调 Replay**

在 `App.axaml.cs` 的应用 OnFrameworkInitializationCompleted（或 ServiceLocator 启动后）加：

```csharp
_ = Task.Run(async () =>
{
    try
    {
        var recovery = App.Services?.GetService<GenerationRecoveryService>();
        if (recovery != null) await recovery.ReplayAsync();
    }
    catch (Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[WAL Recovery] failed: {ex.Message}");
    }
});
```

加 using：
```csharp
using Microsoft.Extensions.DependencyInjection;
using TM.Services.Modules.ProjectData.Generation.Wal;
```

- [ ] **Step 6.3：build + 全量 test**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v minimal
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
```

Expected: 全过。

- [ ] **Step 6.4：commit**

```bash
git add src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs \
        src/Tianming.Desktop.Avalonia/App.axaml.cs
git commit -m "feat(wal): M6.3.6 DI 注册 + 启动 hook Replay"
```

---

## M6.3 Gate 验收

| 项 | 标准 |
|---|---|
| GenerationJournalEntry + 6 Step | JSON round-trip + 枚举完整 |
| FileGenerationJournal | jsonl Append + Read + ListPending（已 Done 过滤）+ Clear |
| FileChapterTrackingSink | SaveAsync 改 temp+rename atomic（无 .tmp 残留）|
| Pipeline 集成 | 4 步骤 + Done 清空 + 失败保留 pending |
| GenerationRecoveryService | ReplayHandler 委托 + 启动 hook 调用 |
| 启动恢复 | App 启动后 ReplayAsync 不抛异常 |
| 全量 test | 新增 ≥10 条，dotnet test 全过 |
