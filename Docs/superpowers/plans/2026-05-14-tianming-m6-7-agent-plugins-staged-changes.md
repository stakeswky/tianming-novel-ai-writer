# M6.7 Agent 插件写工程能力 + Staged Changes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Agent 模式从只读升级为可写：新增 3 个写工具（`WorkspaceEditTool` / `DataEditTool` / `ContentEditTool`），但写入必须经过 staged changes 流程——工具调用返回"待批准 staging ID"而非完成结果，用户在 ToolCallCard 上点"批准"才真正落盘，"拒绝"则丢弃。

**Architecture:** 新增 `IStagedChangeStore` 接口 + `FileStagedChangeStore` 实现（`<project>/.staged/`）。3 个写工具调 `IStagedChangeStore.Stage(...)` 返回 ID。新增 `IStagedChangeApprover` 服务负责 approve（apply 改动到真实文件）和 reject（删 staging）。`ConversationPanelViewModel` 把现有 `ToolCallCard.ApproveCommand` / `RejectCommand` 接 `IStagedChangeApprover.ApproveAsync` / `RejectAsync`。

**Tech Stack:** .NET 8 + `System.Text.Json` + 复用 `ChapterContentStore` / `ModuleDataAdapter` + xUnit。零新依赖。

**Branch:** `m6-7-agent-staged-changes`（基于 main）。

**前置条件：** Round 2/3 + M6.2-M6.6 已合并入 main 并基线绿。

---

## Task 0：基线 + worktree

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
git worktree add /Users/jimmy/Downloads/tianming-m6-7 -b m6-7-agent-staged-changes main
cd /Users/jimmy/Downloads/tianming-m6-7
```

---

## Task 1：StagedChange 数据模型 + 类型枚举

**Files:**
- Create: `src/Tianming.ProjectData/StagedChanges/StagedChange.cs`
- Test: `tests/Tianming.ProjectData.Tests/StagedChanges/StagedChangeTests.cs`

- [ ] **Step 1.1：测试**

```csharp
using System.Text.Json;
using TM.Services.Modules.ProjectData.StagedChanges;
using Xunit;

namespace Tianming.ProjectData.Tests.StagedChanges;

public class StagedChangeTests
{
    [Fact]
    public void StagedChange_round_trips()
    {
        var c = new StagedChange
        {
            Id = "stg-abc",
            ChangeType = StagedChangeType.ContentEdit,
            TargetId = "ch-005",
            OldContentSnippet = "old text",
            NewContentSnippet = "new text",
            Reason = "AI suggested rewrite",
        };
        var json = JsonSerializer.Serialize(c);
        var back = JsonSerializer.Deserialize<StagedChange>(json);
        Assert.NotNull(back);
        Assert.Equal(StagedChangeType.ContentEdit, back!.ChangeType);
        Assert.Equal("ch-005", back.TargetId);
    }

    [Fact]
    public void Three_change_types_defined()
    {
        Assert.True(System.Enum.IsDefined(typeof(StagedChangeType), StagedChangeType.WorkspaceEdit));
        Assert.True(System.Enum.IsDefined(typeof(StagedChangeType), StagedChangeType.DataEdit));
        Assert.True(System.Enum.IsDefined(typeof(StagedChangeType), StagedChangeType.ContentEdit));
    }
}
```

- [ ] **Step 1.2：实现**

```csharp
using System;
using System.Text.Json.Serialization;

namespace TM.Services.Modules.ProjectData.StagedChanges
{
    public enum StagedChangeType
    {
        WorkspaceEdit,  // 项目文件（README / settings 等）
        DataEdit,       // 设计模块条目（角色 / 世界观等）
        ContentEdit,    // 章节正文
    }

    public sealed class StagedChange
    {
        [JsonPropertyName("Id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("ChangeType")] public StagedChangeType ChangeType { get; set; }
        [JsonPropertyName("TargetId")] public string TargetId { get; set; } = string.Empty;
        [JsonPropertyName("OldContentSnippet")] public string OldContentSnippet { get; set; } = string.Empty;
        [JsonPropertyName("NewContentSnippet")] public string NewContentSnippet { get; set; } = string.Empty;
        [JsonPropertyName("PayloadJson")] public string PayloadJson { get; set; } = string.Empty;
        [JsonPropertyName("Reason")] public string Reason { get; set; } = string.Empty;
        [JsonPropertyName("CreatedAt")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
```

- [ ] **Step 1.3：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter StagedChangeTests --nologo -v minimal
git add src/Tianming.ProjectData/StagedChanges/StagedChange.cs \
        tests/Tianming.ProjectData.Tests/StagedChanges/StagedChangeTests.cs
git commit -m "feat(staged): M6.7.1 StagedChange + 3 类型枚举"
```

---

## Task 2：IStagedChangeStore + FileStagedChangeStore

**Files:**
- Create: `src/Tianming.ProjectData/StagedChanges/IStagedChangeStore.cs`
- Create: `src/Tianming.ProjectData/StagedChanges/FileStagedChangeStore.cs`
- Test: `tests/Tianming.ProjectData.Tests/StagedChanges/FileStagedChangeStoreTests.cs`

- [ ] **Step 2.1：测试**

```csharp
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.StagedChanges;
using Xunit;

namespace Tianming.ProjectData.Tests.StagedChanges;

public class FileStagedChangeStoreTests
{
    [Fact]
    public async Task Stage_assigns_id_and_returns_it()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-stg-{System.Guid.NewGuid():N}");
        var store = new FileStagedChangeStore(dir);
        var id = await store.StageAsync(new StagedChange
        {
            ChangeType = StagedChangeType.ContentEdit,
            TargetId = "ch-001",
            NewContentSnippet = "new content",
        });
        Assert.False(string.IsNullOrEmpty(id));
        Assert.StartsWith("stg-", id);
    }

    [Fact]
    public async Task Get_returns_staged_change_by_id()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-stg-{System.Guid.NewGuid():N}");
        var store = new FileStagedChangeStore(dir);
        var id = await store.StageAsync(new StagedChange { TargetId = "ch-001" });
        var back = await store.GetAsync(id);
        Assert.NotNull(back);
        Assert.Equal("ch-001", back!.TargetId);
    }

    [Fact]
    public async Task ListPending_returns_all_staged_changes()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-stg-{System.Guid.NewGuid():N}");
        var store = new FileStagedChangeStore(dir);
        await store.StageAsync(new StagedChange { TargetId = "ch-001" });
        await store.StageAsync(new StagedChange { TargetId = "ch-002" });
        var pending = await store.ListPendingAsync();
        Assert.Equal(2, pending.Count);
    }

    [Fact]
    public async Task Remove_clears_staged_change()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-stg-{System.Guid.NewGuid():N}");
        var store = new FileStagedChangeStore(dir);
        var id = await store.StageAsync(new StagedChange { TargetId = "ch-001" });
        await store.RemoveAsync(id);
        Assert.Null(await store.GetAsync(id));
    }
}
```

- [ ] **Step 2.2：接口 + 实现**

```csharp
// IStagedChangeStore.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.StagedChanges
{
    public interface IStagedChangeStore
    {
        Task<string> StageAsync(StagedChange change, CancellationToken ct = default);
        Task<StagedChange?> GetAsync(string id, CancellationToken ct = default);
        Task<IReadOnlyList<StagedChange>> ListPendingAsync(CancellationToken ct = default);
        Task RemoveAsync(string id, CancellationToken ct = default);
    }
}
```

```csharp
// FileStagedChangeStore.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.StagedChanges
{
    public sealed class FileStagedChangeStore : IStagedChangeStore
    {
        private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        private readonly string _stagedDir;
        private readonly SemaphoreSlim _lock = new(1, 1);

        public FileStagedChangeStore(string projectRoot)
        {
            _stagedDir = Path.Combine(projectRoot, ".staged");
            Directory.CreateDirectory(_stagedDir);
        }

        public async Task<string> StageAsync(StagedChange change, CancellationToken ct = default)
        {
            await _lock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (string.IsNullOrEmpty(change.Id))
                    change.Id = $"stg-{Guid.NewGuid():N}".Substring(0, 16);
                var path = Path.Combine(_stagedDir, $"{change.Id}.json");
                var json = JsonSerializer.Serialize(change, JsonOpts);
                var tmp = path + ".tmp";
                await File.WriteAllTextAsync(tmp, json, ct).ConfigureAwait(false);
                File.Move(tmp, path, overwrite: true);
                return change.Id;
            }
            finally { _lock.Release(); }
        }

        public async Task<StagedChange?> GetAsync(string id, CancellationToken ct = default)
        {
            var path = Path.Combine(_stagedDir, $"{id}.json");
            if (!File.Exists(path)) return null;
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<StagedChange>(json, JsonOpts);
        }

        public async Task<IReadOnlyList<StagedChange>> ListPendingAsync(CancellationToken ct = default)
        {
            var list = new List<StagedChange>();
            foreach (var file in Directory.GetFiles(_stagedDir, "*.json"))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                    var change = JsonSerializer.Deserialize<StagedChange>(json, JsonOpts);
                    if (change != null) list.Add(change);
                }
                catch (JsonException) { /* skip corrupt */ }
            }
            return list;
        }

        public Task RemoveAsync(string id, CancellationToken ct = default)
        {
            var path = Path.Combine(_stagedDir, $"{id}.json");
            if (File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }
    }
}
```

- [ ] **Step 2.3：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter FileStagedChangeStoreTests --nologo -v minimal
git add src/Tianming.ProjectData/StagedChanges/IStagedChangeStore.cs \
        src/Tianming.ProjectData/StagedChanges/FileStagedChangeStore.cs \
        tests/Tianming.ProjectData.Tests/StagedChanges/FileStagedChangeStoreTests.cs
git commit -m "feat(staged): M6.7.2 IStagedChangeStore + FileStagedChangeStore"
```

---

## Task 3：3 个写工具（ContentEditTool / DataEditTool / WorkspaceEditTool）

> 每个工具 ctor 注入 `IStagedChangeStore`，`InvokeAsync` 仅调 `StageAsync` 不真写。

**Files:**
- Create: `src/Tianming.AI/SemanticKernel/Conversation/Tools/Write/ContentEditTool.cs`
- Create: `src/Tianming.AI/SemanticKernel/Conversation/Tools/Write/DataEditTool.cs`
- Create: `src/Tianming.AI/SemanticKernel/Conversation/Tools/Write/WorkspaceEditTool.cs`
- Test: 3 个对应 test 文件

### Task 3a：ContentEditTool

- [ ] **Step 3a.1：测试 + 实现**

```csharp
// ContentEditTool.cs
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.StagedChanges;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation.Tools.Write
{
    public sealed class ContentEditTool : IConversationTool
    {
        private readonly IStagedChangeStore _store;

        public ContentEditTool(IStagedChangeStore store)
        {
            _store = store;
        }

        public string Name => "content_edit";
        public string Description => "提议章节正文修改。参数：chapterId（章节 ID），newContent（替换全文）或 patchSnippet（局部替换片段），reason（修改理由）。提议会进入待审核队列，需用户批准。";
        public string ParameterSchemaJson => """
        {
          "type": "object",
          "properties": {
            "chapterId": {"type": "string"},
            "newContent": {"type": "string"},
            "patchSnippet": {"type": "string"},
            "oldSnippet": {"type": "string"},
            "reason": {"type": "string"}
          },
          "required": ["chapterId", "reason"]
        }
        """;

        public async Task<string> InvokeAsync(IReadOnlyDictionary<string, object?> args, CancellationToken ct)
        {
            if (!args.TryGetValue("chapterId", out var chapterIdObj) || chapterIdObj is not string chapterId)
                return "错误：缺少 chapterId 参数。";
            var newContent = args.TryGetValue("newContent", out var nc) ? nc as string : null;
            var patch = args.TryGetValue("patchSnippet", out var ps) ? ps as string : null;
            var oldSnippet = args.TryGetValue("oldSnippet", out var os) ? os as string : null;
            var reason = args.TryGetValue("reason", out var r) ? r as string : "(no reason)";

            var change = new StagedChange
            {
                ChangeType = StagedChangeType.ContentEdit,
                TargetId = chapterId,
                OldContentSnippet = oldSnippet ?? string.Empty,
                NewContentSnippet = patch ?? newContent ?? string.Empty,
                Reason = reason ?? string.Empty,
            };
            var id = await _store.StageAsync(change, ct);
            return $"已提议修改章节 {chapterId}（待审核：{id}）。请用户在 ToolCallCard 上批准或拒绝。";
        }
    }
}
```

测试（最小覆盖）：

```csharp
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Tools.Write;
using TM.Services.Modules.ProjectData.StagedChanges;
using Xunit;

namespace Tianming.AI.Tests.Conversation.Tools.Write;

public class ContentEditToolTests
{
    [Fact]
    public async Task Invoke_stages_change_with_chapter_id()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-cetool-{System.Guid.NewGuid():N}");
        var store = new FileStagedChangeStore(dir);
        var tool = new ContentEditTool(store);
        var result = await tool.InvokeAsync(new Dictionary<string, object?>
        {
            ["chapterId"] = "ch-001",
            ["newContent"] = "new text",
            ["reason"] = "rewrite for clarity",
        }, default);
        Assert.Contains("ch-001", result);
        Assert.Contains("待审核", result);
        var pending = await store.ListPendingAsync();
        Assert.Single(pending);
    }
}
```

- [ ] **Step 3a.2：commit**

```bash
dotnet test tests/Tianming.AI.Tests/Tianming.AI.Tests.csproj --filter ContentEditToolTests --nologo -v minimal
git add src/Tianming.AI/SemanticKernel/Conversation/Tools/Write/ContentEditTool.cs \
        tests/Tianming.AI.Tests/Conversation/Tools/Write/ContentEditToolTests.cs
git commit -m "feat(agent): M6.7.3a ContentEditTool 写章节正文（staged）"
```

### Task 3b：DataEditTool

类似 ContentEditTool，参数：`category` / `dataId` / `dataJson` / `reason`，ChangeType = DataEdit。
commit message: `feat(agent): M6.7.3b DataEditTool 写设计数据（staged）`

### Task 3c：WorkspaceEditTool

参数：`relativePath` / `newContent` / `reason`，ChangeType = WorkspaceEdit。
commit message: `feat(agent): M6.7.3c WorkspaceEditTool 写工作区文件（staged）`

---

## Task 4：StagedChangeApprover 服务

**Files:**
- Create: `src/Tianming.ProjectData/StagedChanges/IStagedChangeApprover.cs`
- Create: `src/Tianming.ProjectData/StagedChanges/StagedChangeApprover.cs`
- Test: `tests/Tianming.ProjectData.Tests/StagedChanges/StagedChangeApproverTests.cs`

- [ ] **Step 4.1：接口 + 实现**

```csharp
// IStagedChangeApprover.cs
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.StagedChanges
{
    public interface IStagedChangeApprover
    {
        Task<bool> ApproveAsync(string changeId, CancellationToken ct = default);
        Task<bool> RejectAsync(string changeId, CancellationToken ct = default);
    }
}
```

```csharp
// StagedChangeApprover.cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.StagedChanges
{
    public sealed class StagedChangeApprover : IStagedChangeApprover
    {
        public delegate Task ContentApplyHandler(string chapterId, string newContent, CancellationToken ct);
        public delegate Task DataApplyHandler(string category, string dataId, string dataJson, CancellationToken ct);
        public delegate Task WorkspaceApplyHandler(string relativePath, string newContent, CancellationToken ct);

        private readonly IStagedChangeStore _store;
        private readonly ContentApplyHandler _content;
        private readonly DataApplyHandler _data;
        private readonly WorkspaceApplyHandler _workspace;

        public StagedChangeApprover(
            IStagedChangeStore store,
            ContentApplyHandler content,
            DataApplyHandler data,
            WorkspaceApplyHandler workspace)
        {
            _store = store;
            _content = content;
            _data = data;
            _workspace = workspace;
        }

        public async Task<bool> ApproveAsync(string changeId, CancellationToken ct = default)
        {
            var change = await _store.GetAsync(changeId, ct).ConfigureAwait(false);
            if (change == null) return false;

            switch (change.ChangeType)
            {
                case StagedChangeType.ContentEdit:
                    await _content(change.TargetId, change.NewContentSnippet, ct).ConfigureAwait(false);
                    break;
                case StagedChangeType.DataEdit:
                    var parts = change.TargetId.Split(':');
                    if (parts.Length != 2) return false;
                    await _data(parts[0], parts[1], change.PayloadJson, ct).ConfigureAwait(false);
                    break;
                case StagedChangeType.WorkspaceEdit:
                    await _workspace(change.TargetId, change.NewContentSnippet, ct).ConfigureAwait(false);
                    break;
            }
            await _store.RemoveAsync(changeId, ct).ConfigureAwait(false);
            return true;
        }

        public async Task<bool> RejectAsync(string changeId, CancellationToken ct = default)
        {
            var change = await _store.GetAsync(changeId, ct).ConfigureAwait(false);
            if (change == null) return false;
            await _store.RemoveAsync(changeId, ct).ConfigureAwait(false);
            return true;
        }
    }
}
```

- [ ] **Step 4.2：测试 + commit**

```csharp
// Tests: Approver_dispatches_to_correct_handler / Reject_removes_without_applying
```

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter StagedChangeApproverTests --nologo -v minimal
git add src/Tianming.ProjectData/StagedChanges/IStagedChangeApprover.cs \
        src/Tianming.ProjectData/StagedChanges/StagedChangeApprover.cs \
        tests/Tianming.ProjectData.Tests/StagedChanges/StagedChangeApproverTests.cs
git commit -m "feat(staged): M6.7.4 StagedChangeApprover (Approve/Reject 委托式)"
```

---

## Task 5：ToolCallCard 命令处理 + ConversationPanelViewModel 接入

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ConversationPanelViewModel.cs`
- Modify: `src/Tianming.Desktop.Avalonia/Controls/ToolCallCardVm.cs`（如存在；否则在 ConversationPanelViewModel 内）

- [ ] **Step 5.1：在 ConversationPanelViewModel 加 Approver 注入 + 命令**

```csharp
private readonly IStagedChangeApprover? _approver;

public ConversationPanelViewModel(
    IConversationOrchestrator orchestrator,
    IFileSessionStore sessionStore,
    IDispatcherScheduler scheduler,
    IStagedChangeApprover? approver = null,
    bool seedSamples = false)
{
    /* 原 */
    _approver = approver;
}

[RelayCommand]
private async Task ApproveStagedAsync(string? stagedId)
{
    if (_approver == null || string.IsNullOrEmpty(stagedId)) return;
    await _approver.ApproveAsync(stagedId);
}

[RelayCommand]
private async Task RejectStagedAsync(string? stagedId)
{
    if (_approver == null || string.IsNullOrEmpty(stagedId)) return;
    await _approver.RejectAsync(stagedId);
}
```

加 using：
```csharp
using TM.Services.Modules.ProjectData.StagedChanges;
```

- [ ] **Step 5.2：测试 + commit**

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter ConversationPanelViewModelTests --nologo -v minimal
git add src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ConversationPanelViewModel.cs
git commit -m "feat(agent): M6.7.5 ConversationPanelViewModel 接入 IStagedChangeApprover"
```

---

## Task 6：DI 注册

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`

- [ ] **Step 6.1：注册 store + tools + approver**

```csharp
// M6.7 Agent 写工具 + Staged Changes
s.AddSingleton<IStagedChangeStore>(sp =>
    new FileStagedChangeStore(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));

s.AddSingleton<IConversationTool>(sp =>
    new ContentEditTool(sp.GetRequiredService<IStagedChangeStore>()));
s.AddSingleton<IConversationTool>(sp =>
    new DataEditTool(sp.GetRequiredService<IStagedChangeStore>()));
s.AddSingleton<IConversationTool>(sp =>
    new WorkspaceEditTool(sp.GetRequiredService<IStagedChangeStore>()));

s.AddSingleton<IStagedChangeApprover>(sp => new StagedChangeApprover(
    sp.GetRequiredService<IStagedChangeStore>(),
    content: async (chapterId, newContent, ct) =>
    {
        // 接现有 ChapterContentStore；如未在 DI 暴露，可注入 helper
        await System.Threading.Tasks.Task.CompletedTask;
    },
    data: async (category, dataId, dataJson, ct) =>
    {
        // 接现有 ModuleDataAdapter（按 category 分发）
        await System.Threading.Tasks.Task.CompletedTask;
    },
    workspace: async (relativePath, newContent, ct) =>
    {
        // 直接 File.WriteAllText 到 ProjectRoot/relativePath
        await System.Threading.Tasks.Task.CompletedTask;
    }));
```

加 using：
```csharp
using TM.Services.Framework.AI.SemanticKernel.Conversation.Tools.Write;
using TM.Services.Modules.ProjectData.StagedChanges;
```

- [ ] **Step 6.2：build + 全量 test**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v minimal
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
```

- [ ] **Step 6.3：commit**

```bash
git add src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs
git commit -m "feat(agent): M6.7.6 DI 注册 staged + 3 写工具 + approver"
```

---

## M6.7 Gate 验收

| 项 | 标准 |
|---|---|
| StagedChange 模型 | 3 类型 + JSON round-trip |
| FileStagedChangeStore | Stage/Get/ListPending/Remove + atomic write |
| 3 写工具 | ContentEdit / DataEdit / WorkspaceEdit 各 ≥1 测试 |
| StagedChangeApprover | Approve 按 ChangeType 分发 + Remove；Reject 仅 Remove |
| ConversationPanelViewModel | ApproveStaged / RejectStaged 命令 |
| DI | AppHost.Build() 能 resolve 6 个新类型 |
| 全量 test | 新增 ≥10 条，dotnet test 全过 |
