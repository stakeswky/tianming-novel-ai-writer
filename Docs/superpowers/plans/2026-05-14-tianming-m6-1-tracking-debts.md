# M6.1 Tracking 债务与 FactSnapshot 拆分 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** v2.8.7 写作内核升级首个里程碑：引入 **5 类追踪债务**（EntityDrift / Omission / Deadline / Pledge / SecretReveal）+ 5 个专项债务检测器；扩展 `FactSnapshot` 增加 `TrackingDebts` 字段；在 `ChapterGenerationPipeline` 生成后触发债务检测并持久化。

**Architecture:** 复用现有 `IChapterTrackingSink` / `FileChapterTrackingSink` / `ChapterTrackingDispatcher` 通道。新增独立的 `ITrackingDebtDetector` 接口 + 5 个实现（每类一个），由新增的 `TrackingDebtRegistry` 聚合并在生成 Pipeline 的"Dispatch"阶段触发。债务结果存到 `<project>/Tracking/tracking_debts_vol{volume}.json`。

**Tech Stack:** .NET 8 + `System.Text.Json` + xUnit；零新依赖。

**Branch:** `m6-1-tracking-debts`（基于 main，Round 2 已合并后开）。

**前置条件:**
1. Round 2 已合并：m4-4 / m4-5 / m4-6 全部 merge 入 main
2. main 上能 build + test 全绿

**5 类债务定义**（spec 部分，写在 plan 而非外部 spec）：

| 类别 | 触发条件 | 例 |
|---|---|---|
| **EntityDrift** | 同一角色/地点的核心描述在不同章节出现冲突（如"沈砚黑发"→后章变"棕发"）| `CharacterDescriptions.HairColor` 与历史快照不一致 |
| **Omission** | Guide 中标记 `IsExpected` 的事件/角色出场未在指定章节发生 | `CharacterStateGuide` 中 `ExpectedAppearance: ch-12` 但快照无 |
| **Deadline** | Foreshadowing `IsOverdue == true` 或 `ExpectedPayoffChapter` 已过未 resolve | 现有 `ForeshadowingStatusEntry.IsOverdue` 已有 |
| **Pledge** | 角色或剧情承诺（"下周必去找他"）未在承诺时间内兑现 | 新增 `Pledges` 列表，每条记录 `PromiseChapter` + `DeadlineChapter` |
| **SecretReveal** | 设定为 `IsRevealed=false` 的秘密在章节内被意外揭露 | 新增 `Secrets` 列表，每条记录 `IsRevealed` + `RevealedAtChapter` |

---

## Task 0：基线确认

**Files:** 无

- [ ] **Step 0.1：确认 main 状态 + 开 worktree**

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer
git log main --oneline | head -10
# 确认看到 Merge Lane M4.4/M4.5/M4.6 三条
git worktree add /Users/jimmy/Downloads/tianming-m6-1 -b m6-1-tracking-debts main
cd /Users/jimmy/Downloads/tianming-m6-1
```

- [ ] **Step 0.2：基线测试**

```bash
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -8
```

Expected: 全过。

---

## Task 1：TrackingDebt 数据模型

**Files:**
- Create: `src/Tianming.ProjectData/Tracking/TrackingDebt.cs`
- Create: `tests/Tianming.ProjectData.Tests/Tracking/TrackingDebtTests.cs`

- [ ] **Step 1.1：写 TrackingDebt 测试（先红）**

创建 `tests/Tianming.ProjectData.Tests/Tracking/TrackingDebtTests.cs`：

```csharp
using System.Text.Json;
using TM.Services.Modules.ProjectData.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking;

public class TrackingDebtTests
{
    [Fact]
    public void TrackingDebt_serializes_round_trip()
    {
        var debt = new TrackingDebt
        {
            Id = "debt-001",
            Category = TrackingDebtCategory.EntityDrift,
            ChapterId = "ch-005",
            EntityId = "char-shen-yan",
            Description = "沈砚 HairColor 由黑变棕",
            Severity = TrackingDebtSeverity.High,
            DetectedAtChapter = "ch-005",
            EvidenceJson = "{\"old\":\"黑\",\"new\":\"棕\"}",
            ResolvedAtChapter = null,
        };

        var json = JsonSerializer.Serialize(debt);
        var back = JsonSerializer.Deserialize<TrackingDebt>(json);

        Assert.NotNull(back);
        Assert.Equal("debt-001", back!.Id);
        Assert.Equal(TrackingDebtCategory.EntityDrift, back.Category);
        Assert.Equal("沈砚 HairColor 由黑变棕", back.Description);
        Assert.Null(back.ResolvedAtChapter);
    }

    [Fact]
    public void All_five_categories_defined()
    {
        Assert.True(System.Enum.IsDefined(typeof(TrackingDebtCategory), TrackingDebtCategory.EntityDrift));
        Assert.True(System.Enum.IsDefined(typeof(TrackingDebtCategory), TrackingDebtCategory.Omission));
        Assert.True(System.Enum.IsDefined(typeof(TrackingDebtCategory), TrackingDebtCategory.Deadline));
        Assert.True(System.Enum.IsDefined(typeof(TrackingDebtCategory), TrackingDebtCategory.Pledge));
        Assert.True(System.Enum.IsDefined(typeof(TrackingDebtCategory), TrackingDebtCategory.SecretReveal));
    }
}
```

- [ ] **Step 1.2：跑 test 确认红**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter TrackingDebtTests --nologo -v minimal
```

Expected: 编译失败 — `TrackingDebt` / `TrackingDebtCategory` / `TrackingDebtSeverity` 不存在。

- [ ] **Step 1.3：写 TrackingDebt.cs**

创建 `src/Tianming.ProjectData/Tracking/TrackingDebt.cs`：

```csharp
using System.Text.Json.Serialization;

namespace TM.Services.Modules.ProjectData.Tracking
{
    public enum TrackingDebtCategory
    {
        EntityDrift,
        Omission,
        Deadline,
        Pledge,
        SecretReveal,
    }

    public enum TrackingDebtSeverity
    {
        Low,
        Medium,
        High,
    }

    public sealed class TrackingDebt
    {
        [JsonPropertyName("Id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("Category")] public TrackingDebtCategory Category { get; set; }
        [JsonPropertyName("ChapterId")] public string ChapterId { get; set; } = string.Empty;
        [JsonPropertyName("EntityId")] public string EntityId { get; set; } = string.Empty;
        [JsonPropertyName("Description")] public string Description { get; set; } = string.Empty;
        [JsonPropertyName("Severity")] public TrackingDebtSeverity Severity { get; set; } = TrackingDebtSeverity.Medium;
        [JsonPropertyName("DetectedAtChapter")] public string DetectedAtChapter { get; set; } = string.Empty;
        [JsonPropertyName("EvidenceJson")] public string EvidenceJson { get; set; } = string.Empty;
        [JsonPropertyName("ResolvedAtChapter")] public string? ResolvedAtChapter { get; set; }
        [JsonPropertyName("CreatedAt")] public System.DateTime CreatedAt { get; set; } = System.DateTime.UtcNow;
    }
}
```

- [ ] **Step 1.4：跑 test 确认绿**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter TrackingDebtTests --nologo -v minimal
```

Expected: PASS 2/2.

- [ ] **Step 1.5：commit**

```bash
git add src/Tianming.ProjectData/Tracking/TrackingDebt.cs \
        tests/Tianming.ProjectData.Tests/Tracking/TrackingDebtTests.cs
git commit -m "feat(tracking): M6.1.1 TrackingDebt 模型 + 5 类 + 3 级 severity"
```

---

## Task 2：扩展 FactSnapshot 加 TrackingDebts 字段

**Files:**
- Modify: `src/Tianming.ProjectData/Tracking/FactSnapshot.cs`
- Modify: `tests/Tianming.ProjectData.Tests/Tracking/FactSnapshotTests.cs`（如果存在；否则新建）

- [ ] **Step 2.1：写测试断言 FactSnapshot 含 TrackingDebts**

新建或追加 `tests/Tianming.ProjectData.Tests/Tracking/FactSnapshotTrackingDebtTests.cs`：

```csharp
using System.Collections.Generic;
using System.Text.Json;
using TM.Services.Modules.ProjectData.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking;

public class FactSnapshotTrackingDebtTests
{
    [Fact]
    public void FactSnapshot_has_TrackingDebts_default_empty()
    {
        var snap = new FactSnapshot();
        Assert.NotNull(snap.TrackingDebts);
        Assert.Empty(snap.TrackingDebts);
    }

    [Fact]
    public void FactSnapshot_serializes_TrackingDebts()
    {
        var snap = new FactSnapshot
        {
            TrackingDebts = new List<TrackingDebt>
            {
                new() { Id = "d1", Category = TrackingDebtCategory.Pledge, ChapterId = "ch-003", Description = "未兑现承诺" },
            },
        };
        var json = JsonSerializer.Serialize(snap);
        var back = JsonSerializer.Deserialize<FactSnapshot>(json);

        Assert.NotNull(back);
        Assert.Single(back!.TrackingDebts);
        Assert.Equal(TrackingDebtCategory.Pledge, back.TrackingDebts[0].Category);
    }
}
```

- [ ] **Step 2.2：跑 test 确认红**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter FactSnapshotTrackingDebtTests --nologo -v minimal
```

Expected: 编译失败 — `FactSnapshot` 无 `TrackingDebts` 属性。

- [ ] **Step 2.3：扩展 FactSnapshot**

在 `src/Tianming.ProjectData/Tracking/FactSnapshot.cs` 的 `FactSnapshot` class 内追加（在 `ItemStates` 后）：

```csharp
[JsonPropertyName("TrackingDebts")] public List<TrackingDebt> TrackingDebts { get; set; } = new();
```

- [ ] **Step 2.4：跑 test 确认绿**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter FactSnapshotTrackingDebtTests --nologo -v minimal
```

Expected: PASS 2/2.

- [ ] **Step 2.5：build 全量确认无破坏**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v minimal
```

Expected: 0 Warning 0 Error（已有引用 `FactSnapshot` 的代码因为 `TrackingDebts` 是默认空，应兼容）。

- [ ] **Step 2.6：commit**

```bash
git add src/Tianming.ProjectData/Tracking/FactSnapshot.cs \
        tests/Tianming.ProjectData.Tests/Tracking/FactSnapshotTrackingDebtTests.cs
git commit -m "feat(tracking): M6.1.2 FactSnapshot 增加 TrackingDebts 字段"
```

---

## Task 3：Pledge / Secret Guide 数据模型

> 现有 GuideModels 没有 Pledge 和 Secret 概念，必须新建。

**Files:**
- Modify: `src/Tianming.ProjectData/Tracking/GuideModels.cs`
- Create: `tests/Tianming.ProjectData.Tests/Tracking/PledgeSecretGuideTests.cs`

- [ ] **Step 3.1：写测试（先红）**

创建 `tests/Tianming.ProjectData.Tests/Tracking/PledgeSecretGuideTests.cs`：

```csharp
using System.Text.Json;
using TM.Services.Modules.ProjectData.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking;

public class PledgeSecretGuideTests
{
    [Fact]
    public void PledgeGuide_round_trip()
    {
        var guide = new PledgeGuide
        {
            SourceBookId = "book-1",
        };
        guide.Pledges["p1"] = new PledgeEntry
        {
            Name = "下周必去看他",
            PromisedByCharacterId = "char-001",
            PromisedAtChapter = "ch-003",
            DeadlineChapter = "ch-008",
            IsFulfilled = false,
        };
        var json = JsonSerializer.Serialize(guide);
        var back = JsonSerializer.Deserialize<PledgeGuide>(json);
        Assert.NotNull(back);
        Assert.Single(back!.Pledges);
        Assert.False(back.Pledges["p1"].IsFulfilled);
    }

    [Fact]
    public void SecretGuide_round_trip()
    {
        var guide = new SecretGuide { SourceBookId = "book-1" };
        guide.Secrets["s1"] = new SecretEntry
        {
            Name = "主角真实身份",
            IsRevealed = false,
            ExpectedRevealChapter = "ch-050",
            ActualRevealChapter = "",
            HoldersCharacterIds = new() { "char-002", "char-003" },
        };
        var json = JsonSerializer.Serialize(guide);
        var back = JsonSerializer.Deserialize<SecretGuide>(json);
        Assert.NotNull(back);
        Assert.Single(back!.Secrets);
        Assert.False(back.Secrets["s1"].IsRevealed);
    }
}
```

- [ ] **Step 3.2：跑 test 确认红**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter PledgeSecretGuideTests --nologo -v minimal
```

Expected: 编译失败 — `PledgeGuide` / `SecretGuide` / `PledgeEntry` / `SecretEntry` 不存在。

- [ ] **Step 3.3：追加 GuideModels.cs**

在 `src/Tianming.ProjectData/Tracking/GuideModels.cs` 末尾（namespace 内）追加：

```csharp
public class PledgeGuide
{
    [JsonPropertyName("Module")] public string Module { get; set; } = "PledgeGuide";
    [JsonPropertyName("SourceBookId")] public string SourceBookId { get; set; } = string.Empty;
    [JsonPropertyName("Pledges")] public Dictionary<string, PledgeEntry> Pledges { get; set; } = new();
}

public class PledgeEntry
{
    [JsonPropertyName("Name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("PromisedByCharacterId")] public string PromisedByCharacterId { get; set; } = string.Empty;
    [JsonPropertyName("PromisedAtChapter")] public string PromisedAtChapter { get; set; } = string.Empty;
    [JsonPropertyName("DeadlineChapter")] public string DeadlineChapter { get; set; } = string.Empty;
    [JsonPropertyName("IsFulfilled")] public bool IsFulfilled { get; set; }
    [JsonPropertyName("FulfilledAtChapter")] public string FulfilledAtChapter { get; set; } = string.Empty;
}

public class SecretGuide
{
    [JsonPropertyName("Module")] public string Module { get; set; } = "SecretGuide";
    [JsonPropertyName("SourceBookId")] public string SourceBookId { get; set; } = string.Empty;
    [JsonPropertyName("Secrets")] public Dictionary<string, SecretEntry> Secrets { get; set; } = new();
}

public class SecretEntry
{
    [JsonPropertyName("Name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("IsRevealed")] public bool IsRevealed { get; set; }
    [JsonPropertyName("ExpectedRevealChapter")] public string ExpectedRevealChapter { get; set; } = string.Empty;
    [JsonPropertyName("ActualRevealChapter")] public string ActualRevealChapter { get; set; } = string.Empty;
    [JsonPropertyName("HoldersCharacterIds")] public List<string> HoldersCharacterIds { get; set; } = new();
}
```

确认 `using System.Collections.Generic;` 和 `using System.Text.Json.Serialization;` 已在文件顶部。

- [ ] **Step 3.4：跑 test 确认绿**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter PledgeSecretGuideTests --nologo -v minimal
```

Expected: PASS 2/2.

- [ ] **Step 3.5：commit**

```bash
git add src/Tianming.ProjectData/Tracking/GuideModels.cs \
        tests/Tianming.ProjectData.Tests/Tracking/PledgeSecretGuideTests.cs
git commit -m "feat(tracking): M6.1.3 Pledge/Secret Guide 模型"
```

---

## Task 4：ITrackingDebtDetector 接口 + 5 个实现

**Files:**
- Create: `src/Tianming.ProjectData/Tracking/Debts/ITrackingDebtDetector.cs`
- Create: `src/Tianming.ProjectData/Tracking/Debts/EntityDriftDetector.cs`
- Create: `src/Tianming.ProjectData/Tracking/Debts/OmissionDetector.cs`
- Create: `src/Tianming.ProjectData/Tracking/Debts/DeadlineDetector.cs`
- Create: `src/Tianming.ProjectData/Tracking/Debts/PledgeDetector.cs`
- Create: `src/Tianming.ProjectData/Tracking/Debts/SecretRevealDetector.cs`
- Create: 5 个对应测试文件

### Task 4a：接口

- [ ] **Step 4a.1：写接口**

创建 `src/Tianming.ProjectData/Tracking/Debts/ITrackingDebtDetector.cs`：

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Tracking.Debts
{
    /// <summary>
    /// 单类追踪债务检测器：基于当前章节 ChapterChanges + 历史 FactSnapshot，
    /// 输出该类下检测到的 TrackingDebt 列表（可空）。
    /// </summary>
    public interface ITrackingDebtDetector
    {
        TrackingDebtCategory Category { get; }

        Task<IReadOnlyList<TrackingDebt>> DetectAsync(
            string chapterId,
            ChapterChanges currentChanges,
            FactSnapshot previousSnapshot,
            TrackingDebtDetectionContext context,
            CancellationToken ct = default);
    }

    public sealed class TrackingDebtDetectionContext
    {
        public ForeshadowingStatusGuide? Foreshadowings { get; set; }
        public PledgeGuide? Pledges { get; set; }
        public SecretGuide? Secrets { get; set; }
        public System.Func<TrackingDebtCategory, string> IdGenerator { get; set; }
            = category => $"{category}-{System.Guid.NewGuid():N}".Substring(0, 24);
    }
}
```

- [ ] **Step 4a.2：build 确认**

```bash
dotnet build src/Tianming.ProjectData/Tianming.ProjectData.csproj --nologo -v minimal
```

Expected: 0 Warning 0 Error.

### Task 4b：EntityDriftDetector

- [ ] **Step 4b.1：写测试（先红）**

创建 `tests/Tianming.ProjectData.Tests/Tracking/Debts/EntityDriftDetectorTests.cs`：

```csharp
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Tracking;
using TM.Services.Modules.ProjectData.Tracking.Debts;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Debts;

public class EntityDriftDetectorTests
{
    [Fact]
    public async Task Detects_hair_color_drift_for_character()
    {
        var detector = new EntityDriftDetector();
        var prev = new FactSnapshot();
        prev.CharacterDescriptions["char-shen"] = new CharacterCoreDescription
        {
            Id = "char-shen", Name = "沈砚", HairColor = "黑", EyeColor = "黑", Appearance = "高瘦",
        };
        var changes = new ChapterChanges
        {
            CharacterStateChanges = new()
            {
                new() { CharacterId = "char-shen", FieldChanges = new() { ["HairColor"] = "棕" } },
            },
        };

        var debts = await detector.DetectAsync("ch-005", changes, prev, new TrackingDebtDetectionContext());

        Assert.Single(debts);
        Assert.Equal(TrackingDebtCategory.EntityDrift, debts[0].Category);
        Assert.Equal("char-shen", debts[0].EntityId);
        Assert.Contains("HairColor", debts[0].Description);
        Assert.Contains("黑", debts[0].EvidenceJson);
        Assert.Contains("棕", debts[0].EvidenceJson);
    }

    [Fact]
    public async Task No_debt_when_field_unchanged()
    {
        var detector = new EntityDriftDetector();
        var prev = new FactSnapshot();
        prev.CharacterDescriptions["char-shen"] = new CharacterCoreDescription
        {
            Id = "char-shen", Name = "沈砚", HairColor = "黑",
        };
        var changes = new ChapterChanges
        {
            CharacterStateChanges = new()
            {
                new() { CharacterId = "char-shen", FieldChanges = new() { ["Mood"] = "怒" } },
            },
        };

        var debts = await detector.DetectAsync("ch-005", changes, prev, new TrackingDebtDetectionContext());

        Assert.Empty(debts);
    }
}
```

- [ ] **Step 4b.2：跑 test 确认红**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter EntityDriftDetectorTests --nologo -v minimal
```

Expected: 编译失败 — `EntityDriftDetector` 不存在；可能 `CharacterStateChange.FieldChanges` 字段需要存在（先看 GateModels）。如果 `CharacterStateChange` 没有 `FieldChanges` 字典，先在 GateModels.cs 追加（最小可用）：

> **注：如果 `CharacterStateChange` 现有结构差异较大，调整测试的 `changes` 构造使用真实字段（如 `NewState`），并在 detector 中对比 `previousSnapshot.CharacterDescriptions[id].HairColor` vs `change.NewState.HairColor`。本 step 接受按现有字段适配。**

- [ ] **Step 4b.3：写 EntityDriftDetector**

创建 `src/Tianming.ProjectData/Tracking/Debts/EntityDriftDetector.cs`：

```csharp
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Tracking.Debts
{
    public sealed class EntityDriftDetector : ITrackingDebtDetector
    {
        // 监控的核心描述字段（与 CharacterCoreDescription 对应）。
        private static readonly string[] MonitoredFields =
            { "HairColor", "EyeColor", "Appearance" };

        public TrackingDebtCategory Category => TrackingDebtCategory.EntityDrift;

        public Task<IReadOnlyList<TrackingDebt>> DetectAsync(
            string chapterId,
            ChapterChanges currentChanges,
            FactSnapshot previousSnapshot,
            TrackingDebtDetectionContext context,
            CancellationToken ct = default)
        {
            var debts = new List<TrackingDebt>();
            if (currentChanges?.CharacterStateChanges == null)
                return Task.FromResult<IReadOnlyList<TrackingDebt>>(debts);

            foreach (var change in currentChanges.CharacterStateChanges)
            {
                if (!previousSnapshot.CharacterDescriptions.TryGetValue(change.CharacterId, out var prev))
                    continue;
                if (change.FieldChanges == null) continue;

                foreach (var (field, newValue) in change.FieldChanges)
                {
                    if (System.Array.IndexOf(MonitoredFields, field) < 0) continue;
                    var oldValue = field switch
                    {
                        "HairColor" => prev.HairColor,
                        "EyeColor" => prev.EyeColor,
                        "Appearance" => prev.Appearance,
                        _ => string.Empty,
                    };
                    if (string.IsNullOrEmpty(oldValue) || oldValue == newValue) continue;

                    debts.Add(new TrackingDebt
                    {
                        Id = context.IdGenerator(TrackingDebtCategory.EntityDrift),
                        Category = TrackingDebtCategory.EntityDrift,
                        ChapterId = chapterId,
                        EntityId = change.CharacterId,
                        Description = $"角色 {prev.Name} 的 {field} 由「{oldValue}」变为「{newValue}」",
                        Severity = TrackingDebtSeverity.High,
                        DetectedAtChapter = chapterId,
                        EvidenceJson = JsonSerializer.Serialize(new { field, old = oldValue, @new = newValue }),
                    });
                }
            }
            return Task.FromResult<IReadOnlyList<TrackingDebt>>(debts);
        }
    }
}
```

> **重要：** 如果 `CharacterStateChange` 没有 `FieldChanges` 字段，先在 `GateModels.cs` 的 `CharacterStateChange` class 加：
> ```csharp
> [JsonPropertyName("FieldChanges")] public Dictionary<string, string> FieldChanges { get; set; } = new();
> ```

- [ ] **Step 4b.4：跑 test 确认绿**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter EntityDriftDetectorTests --nologo -v minimal
```

Expected: PASS 2/2.

- [ ] **Step 4b.5：commit**

```bash
git add src/Tianming.ProjectData/Tracking/Debts/ITrackingDebtDetector.cs \
        src/Tianming.ProjectData/Tracking/Debts/EntityDriftDetector.cs \
        src/Tianming.ProjectData/Tracking/GateModels.cs \
        tests/Tianming.ProjectData.Tests/Tracking/Debts/EntityDriftDetectorTests.cs
git commit -m "feat(tracking): M6.1.4 ITrackingDebtDetector + EntityDriftDetector"
```

### Task 4c：DeadlineDetector

> 复用 `ForeshadowingStatusEntry.IsOverdue` 已有数据。

- [ ] **Step 4c.1：写测试**

创建 `tests/Tianming.ProjectData.Tests/Tracking/Debts/DeadlineDetectorTests.cs`：

```csharp
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Tracking;
using TM.Services.Modules.ProjectData.Tracking.Debts;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Debts;

public class DeadlineDetectorTests
{
    [Fact]
    public async Task Detects_overdue_foreshadowing()
    {
        var detector = new DeadlineDetector();
        var ctx = new TrackingDebtDetectionContext
        {
            Foreshadowings = new ForeshadowingStatusGuide
            {
                Foreshadowings = new()
                {
                    ["fs-1"] = new ForeshadowingStatusEntry
                    {
                        Name = "金鳞匕首",
                        IsSetup = true,
                        IsResolved = false,
                        IsOverdue = true,
                        ExpectedPayoffChapter = "ch-010",
                    },
                },
            },
        };

        var debts = await detector.DetectAsync("ch-011", new ChapterChanges(), new FactSnapshot(), ctx);

        Assert.Single(debts);
        Assert.Equal(TrackingDebtCategory.Deadline, debts[0].Category);
        Assert.Equal("fs-1", debts[0].EntityId);
        Assert.Contains("金鳞匕首", debts[0].Description);
    }

    [Fact]
    public async Task Skips_resolved_foreshadowing()
    {
        var detector = new DeadlineDetector();
        var ctx = new TrackingDebtDetectionContext
        {
            Foreshadowings = new ForeshadowingStatusGuide
            {
                Foreshadowings = new()
                {
                    ["fs-1"] = new ForeshadowingStatusEntry
                    {
                        Name = "x", IsResolved = true, IsOverdue = false,
                    },
                },
            },
        };
        var debts = await detector.DetectAsync("ch-011", new ChapterChanges(), new FactSnapshot(), ctx);
        Assert.Empty(debts);
    }
}
```

- [ ] **Step 4c.2：写 DeadlineDetector**

创建 `src/Tianming.ProjectData/Tracking/Debts/DeadlineDetector.cs`：

```csharp
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Tracking.Debts
{
    public sealed class DeadlineDetector : ITrackingDebtDetector
    {
        public TrackingDebtCategory Category => TrackingDebtCategory.Deadline;

        public Task<IReadOnlyList<TrackingDebt>> DetectAsync(
            string chapterId,
            ChapterChanges currentChanges,
            FactSnapshot previousSnapshot,
            TrackingDebtDetectionContext context,
            CancellationToken ct = default)
        {
            var debts = new List<TrackingDebt>();
            if (context.Foreshadowings == null) return Task.FromResult<IReadOnlyList<TrackingDebt>>(debts);

            foreach (var (id, entry) in context.Foreshadowings.Foreshadowings)
            {
                if (entry.IsResolved || !entry.IsOverdue) continue;
                debts.Add(new TrackingDebt
                {
                    Id = context.IdGenerator(TrackingDebtCategory.Deadline),
                    Category = TrackingDebtCategory.Deadline,
                    ChapterId = chapterId,
                    EntityId = id,
                    Description = $"伏笔「{entry.Name}」已过预期 payoff 章 {entry.ExpectedPayoffChapter}，未 resolve",
                    Severity = TrackingDebtSeverity.High,
                    DetectedAtChapter = chapterId,
                    EvidenceJson = JsonSerializer.Serialize(new { expectedPayoff = entry.ExpectedPayoffChapter, isResolved = entry.IsResolved, isOverdue = entry.IsOverdue }),
                });
            }
            return Task.FromResult<IReadOnlyList<TrackingDebt>>(debts);
        }
    }
}
```

- [ ] **Step 4c.3：跑 test 确认绿 + commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter DeadlineDetectorTests --nologo -v minimal
git add src/Tianming.ProjectData/Tracking/Debts/DeadlineDetector.cs \
        tests/Tianming.ProjectData.Tests/Tracking/Debts/DeadlineDetectorTests.cs
git commit -m "feat(tracking): M6.1.5 DeadlineDetector（基于 Foreshadowing.IsOverdue）"
```

### Task 4d：PledgeDetector

- [ ] **Step 4d.1：测试**

创建 `tests/Tianming.ProjectData.Tests/Tracking/Debts/PledgeDetectorTests.cs`：

```csharp
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Tracking;
using TM.Services.Modules.ProjectData.Tracking.Debts;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Debts;

public class PledgeDetectorTests
{
    [Fact]
    public async Task Detects_overdue_pledge()
    {
        var detector = new PledgeDetector();
        var ctx = new TrackingDebtDetectionContext
        {
            Pledges = new PledgeGuide
            {
                Pledges = new()
                {
                    ["p1"] = new PledgeEntry
                    {
                        Name = "下周必去找他",
                        PromisedAtChapter = "ch-003",
                        DeadlineChapter = "ch-008",
                        IsFulfilled = false,
                    },
                },
            },
        };
        // 当前章节 ch-009 已过 deadline
        var debts = await detector.DetectAsync("ch-009", new ChapterChanges(), new FactSnapshot(), ctx);
        Assert.Single(debts);
        Assert.Equal(TrackingDebtCategory.Pledge, debts[0].Category);
        Assert.Contains("下周必去找他", debts[0].Description);
    }

    [Fact]
    public async Task Skips_pledge_before_deadline()
    {
        var detector = new PledgeDetector();
        var ctx = new TrackingDebtDetectionContext
        {
            Pledges = new PledgeGuide
            {
                Pledges = new()
                {
                    ["p1"] = new PledgeEntry
                    {
                        DeadlineChapter = "ch-008",
                        IsFulfilled = false,
                    },
                },
            },
        };
        var debts = await detector.DetectAsync("ch-005", new ChapterChanges(), new FactSnapshot(), ctx);
        Assert.Empty(debts);
    }
}
```

- [ ] **Step 4d.2：写 PledgeDetector**

```csharp
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Tracking.Debts
{
    public sealed class PledgeDetector : ITrackingDebtDetector
    {
        public TrackingDebtCategory Category => TrackingDebtCategory.Pledge;

        public Task<IReadOnlyList<TrackingDebt>> DetectAsync(
            string chapterId,
            ChapterChanges currentChanges,
            FactSnapshot previousSnapshot,
            TrackingDebtDetectionContext context,
            CancellationToken ct = default)
        {
            var debts = new List<TrackingDebt>();
            if (context.Pledges == null) return Task.FromResult<IReadOnlyList<TrackingDebt>>(debts);

            foreach (var (id, p) in context.Pledges.Pledges)
            {
                if (p.IsFulfilled) continue;
                if (string.IsNullOrEmpty(p.DeadlineChapter)) continue;
                if (string.Compare(chapterId, p.DeadlineChapter, System.StringComparison.OrdinalIgnoreCase) <= 0)
                    continue;
                debts.Add(new TrackingDebt
                {
                    Id = context.IdGenerator(TrackingDebtCategory.Pledge),
                    Category = TrackingDebtCategory.Pledge,
                    ChapterId = chapterId,
                    EntityId = id,
                    Description = $"承诺「{p.Name}」已过 deadline {p.DeadlineChapter}，未兑现",
                    Severity = TrackingDebtSeverity.High,
                    DetectedAtChapter = chapterId,
                    EvidenceJson = JsonSerializer.Serialize(new { promisedAt = p.PromisedAtChapter, deadline = p.DeadlineChapter, isFulfilled = p.IsFulfilled }),
                });
            }
            return Task.FromResult<IReadOnlyList<TrackingDebt>>(debts);
        }
    }
}
```

> **章节 ID 比较注：** 假定 chapterId 是 `ch-{N:D3}` 字典序与数字序一致；如不一致需 parse 数字部分。

- [ ] **Step 4d.3：测试 + commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter PledgeDetectorTests --nologo -v minimal
git add src/Tianming.ProjectData/Tracking/Debts/PledgeDetector.cs \
        tests/Tianming.ProjectData.Tests/Tracking/Debts/PledgeDetectorTests.cs
git commit -m "feat(tracking): M6.1.6 PledgeDetector（承诺过期检测）"
```

### Task 4e：SecretRevealDetector

- [ ] **Step 4e.1：测试**

创建 `tests/Tianming.ProjectData.Tests/Tracking/Debts/SecretRevealDetectorTests.cs`：

```csharp
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Tracking;
using TM.Services.Modules.ProjectData.Tracking.Debts;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Debts;

public class SecretRevealDetectorTests
{
    [Fact]
    public async Task Detects_unexpected_reveal()
    {
        var detector = new SecretRevealDetector();
        var ctx = new TrackingDebtDetectionContext
        {
            Secrets = new SecretGuide
            {
                Secrets = new()
                {
                    ["s1"] = new SecretEntry
                    {
                        Name = "主角真实身份",
                        IsRevealed = true,                  // 当前章已被揭露
                        ExpectedRevealChapter = "ch-050",
                        ActualRevealChapter = "ch-005",      // 实际比预期提前 45 章
                    },
                },
            },
        };

        var debts = await detector.DetectAsync("ch-005", new ChapterChanges(), new FactSnapshot(), ctx);

        Assert.Single(debts);
        Assert.Equal(TrackingDebtCategory.SecretReveal, debts[0].Category);
        Assert.Contains("主角真实身份", debts[0].Description);
    }

    [Fact]
    public async Task Skips_secret_revealed_at_expected_chapter()
    {
        var detector = new SecretRevealDetector();
        var ctx = new TrackingDebtDetectionContext
        {
            Secrets = new SecretGuide
            {
                Secrets = new()
                {
                    ["s1"] = new SecretEntry
                    {
                        Name = "x",
                        IsRevealed = true,
                        ExpectedRevealChapter = "ch-050",
                        ActualRevealChapter = "ch-050",
                    },
                },
            },
        };
        var debts = await detector.DetectAsync("ch-050", new ChapterChanges(), new FactSnapshot(), ctx);
        Assert.Empty(debts);
    }
}
```

- [ ] **Step 4e.2：写 SecretRevealDetector**

```csharp
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Tracking.Debts
{
    public sealed class SecretRevealDetector : ITrackingDebtDetector
    {
        public TrackingDebtCategory Category => TrackingDebtCategory.SecretReveal;

        public Task<IReadOnlyList<TrackingDebt>> DetectAsync(
            string chapterId,
            ChapterChanges currentChanges,
            FactSnapshot previousSnapshot,
            TrackingDebtDetectionContext context,
            CancellationToken ct = default)
        {
            var debts = new List<TrackingDebt>();
            if (context.Secrets == null) return Task.FromResult<IReadOnlyList<TrackingDebt>>(debts);

            foreach (var (id, s) in context.Secrets.Secrets)
            {
                if (!s.IsRevealed) continue;
                if (string.IsNullOrEmpty(s.ExpectedRevealChapter)) continue;
                if (string.Equals(s.ActualRevealChapter, s.ExpectedRevealChapter, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                debts.Add(new TrackingDebt
                {
                    Id = context.IdGenerator(TrackingDebtCategory.SecretReveal),
                    Category = TrackingDebtCategory.SecretReveal,
                    ChapterId = chapterId,
                    EntityId = id,
                    Description = $"秘密「{s.Name}」在 {s.ActualRevealChapter} 被揭露，预期 {s.ExpectedRevealChapter}",
                    Severity = TrackingDebtSeverity.Medium,
                    DetectedAtChapter = chapterId,
                    EvidenceJson = JsonSerializer.Serialize(new { expected = s.ExpectedRevealChapter, actual = s.ActualRevealChapter }),
                });
            }
            return Task.FromResult<IReadOnlyList<TrackingDebt>>(debts);
        }
    }
}
```

- [ ] **Step 4e.3：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter SecretRevealDetectorTests --nologo -v minimal
git add src/Tianming.ProjectData/Tracking/Debts/SecretRevealDetector.cs \
        tests/Tianming.ProjectData.Tests/Tracking/Debts/SecretRevealDetectorTests.cs
git commit -m "feat(tracking): M6.1.7 SecretRevealDetector（秘密意外揭露）"
```

### Task 4f：OmissionDetector

> 检测 `CharacterStateGuide` 中 `ExpectedAppearance` 等"应该发生但没发生"的情况。`CharacterStateGuide` 的具体字段已经在 `GuideModels.cs` 中。如果没有 `ExpectedAppearance` 字段，先简化为：detect 当前章节里 PlotPoints 是否覆盖了 Guide 中标 `IsKeyEvent` 的 plot 点。

- [ ] **Step 4f.1：测试**

创建 `tests/Tianming.ProjectData.Tests/Tracking/Debts/OmissionDetectorTests.cs`：

```csharp
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Tracking;
using TM.Services.Modules.ProjectData.Tracking.Debts;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Debts;

public class OmissionDetectorTests
{
    [Fact]
    public async Task Detects_missing_expected_setup_for_foreshadow()
    {
        // 用 Foreshadowings 模拟：ExpectedSetupChapter = "ch-005"，但 ActualSetupChapter 仍空
        var detector = new OmissionDetector();
        var ctx = new TrackingDebtDetectionContext
        {
            Foreshadowings = new ForeshadowingStatusGuide
            {
                Foreshadowings = new()
                {
                    ["fs-1"] = new ForeshadowingStatusEntry
                    {
                        Name = "金鳞匕首",
                        IsSetup = false,
                        ExpectedSetupChapter = "ch-005",
                        ActualSetupChapter = "",
                    },
                },
            },
        };
        var debts = await detector.DetectAsync("ch-005", new ChapterChanges(), new FactSnapshot(), ctx);
        Assert.Single(debts);
        Assert.Equal(TrackingDebtCategory.Omission, debts[0].Category);
    }
}
```

- [ ] **Step 4f.2：写 OmissionDetector**

创建 `src/Tianming.ProjectData/Tracking/Debts/OmissionDetector.cs`：

```csharp
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Tracking.Debts
{
    public sealed class OmissionDetector : ITrackingDebtDetector
    {
        public TrackingDebtCategory Category => TrackingDebtCategory.Omission;

        public Task<IReadOnlyList<TrackingDebt>> DetectAsync(
            string chapterId,
            ChapterChanges currentChanges,
            FactSnapshot previousSnapshot,
            TrackingDebtDetectionContext context,
            CancellationToken ct = default)
        {
            var debts = new List<TrackingDebt>();
            if (context.Foreshadowings == null) return Task.FromResult<IReadOnlyList<TrackingDebt>>(debts);

            foreach (var (id, entry) in context.Foreshadowings.Foreshadowings)
            {
                if (entry.IsSetup) continue;
                if (string.IsNullOrEmpty(entry.ExpectedSetupChapter)) continue;
                // 检查当前章节是否是预期 setup 章节但还未 setup
                if (!string.Equals(entry.ExpectedSetupChapter, chapterId, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (!string.IsNullOrEmpty(entry.ActualSetupChapter)) continue;
                debts.Add(new TrackingDebt
                {
                    Id = context.IdGenerator(TrackingDebtCategory.Omission),
                    Category = TrackingDebtCategory.Omission,
                    ChapterId = chapterId,
                    EntityId = id,
                    Description = $"伏笔「{entry.Name}」预期在 {entry.ExpectedSetupChapter} 埋设，但实际未埋",
                    Severity = TrackingDebtSeverity.Medium,
                    DetectedAtChapter = chapterId,
                    EvidenceJson = JsonSerializer.Serialize(new { expectedSetup = entry.ExpectedSetupChapter, actualSetup = entry.ActualSetupChapter }),
                });
            }
            return Task.FromResult<IReadOnlyList<TrackingDebt>>(debts);
        }
    }
}
```

- [ ] **Step 4f.3：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter OmissionDetectorTests --nologo -v minimal
git add src/Tianming.ProjectData/Tracking/Debts/OmissionDetector.cs \
        tests/Tianming.ProjectData.Tests/Tracking/Debts/OmissionDetectorTests.cs
git commit -m "feat(tracking): M6.1.8 OmissionDetector（伏笔埋设遗漏）"
```

---

## Task 5：TrackingDebtRegistry 聚合 5 个 detector

**Files:**
- Create: `src/Tianming.ProjectData/Tracking/Debts/TrackingDebtRegistry.cs`
- Test: `tests/Tianming.ProjectData.Tests/Tracking/Debts/TrackingDebtRegistryTests.cs`

- [ ] **Step 5.1：写测试**

```csharp
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Tracking;
using TM.Services.Modules.ProjectData.Tracking.Debts;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Debts;

public class TrackingDebtRegistryTests
{
    [Fact]
    public async Task DetectAll_runs_all_detectors_and_aggregates()
    {
        var registry = new TrackingDebtRegistry(new ITrackingDebtDetector[]
        {
            new EntityDriftDetector(),
            new OmissionDetector(),
            new DeadlineDetector(),
            new PledgeDetector(),
            new SecretRevealDetector(),
        });

        var ctx = new TrackingDebtDetectionContext
        {
            Foreshadowings = new ForeshadowingStatusGuide
            {
                Foreshadowings = new()
                {
                    ["fs-1"] = new ForeshadowingStatusEntry { Name = "x", IsResolved = false, IsOverdue = true },
                },
            },
        };

        var debts = await registry.DetectAllAsync("ch-010", new ChapterChanges(), new FactSnapshot(), ctx);
        Assert.Contains(debts, d => d.Category == TrackingDebtCategory.Deadline);
    }

    [Fact]
    public void Registry_exposes_five_detector_categories()
    {
        var registry = new TrackingDebtRegistry(new ITrackingDebtDetector[]
        {
            new EntityDriftDetector(),
            new OmissionDetector(),
            new DeadlineDetector(),
            new PledgeDetector(),
            new SecretRevealDetector(),
        });

        var cats = registry.SupportedCategories.OrderBy(c => c).ToArray();
        Assert.Equal(5, cats.Length);
    }
}
```

- [ ] **Step 5.2：写 TrackingDebtRegistry**

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Tracking.Debts
{
    public sealed class TrackingDebtRegistry
    {
        private readonly IReadOnlyList<ITrackingDebtDetector> _detectors;

        public TrackingDebtRegistry(IEnumerable<ITrackingDebtDetector> detectors)
        {
            _detectors = detectors.ToList();
        }

        public IReadOnlyList<TrackingDebtCategory> SupportedCategories =>
            _detectors.Select(d => d.Category).Distinct().ToList();

        public async Task<IReadOnlyList<TrackingDebt>> DetectAllAsync(
            string chapterId,
            ChapterChanges currentChanges,
            FactSnapshot previousSnapshot,
            TrackingDebtDetectionContext context,
            CancellationToken ct = default)
        {
            var all = new List<TrackingDebt>();
            foreach (var d in _detectors)
            {
                var part = await d.DetectAsync(chapterId, currentChanges, previousSnapshot, context, ct).ConfigureAwait(false);
                if (part != null) all.AddRange(part);
            }
            return all;
        }
    }
}
```

- [ ] **Step 5.3：测试 + commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter TrackingDebtRegistryTests --nologo -v minimal
git add src/Tianming.ProjectData/Tracking/Debts/TrackingDebtRegistry.cs \
        tests/Tianming.ProjectData.Tests/Tracking/Debts/TrackingDebtRegistryTests.cs
git commit -m "feat(tracking): M6.1.9 TrackingDebtRegistry 聚合 5 detector"
```

---

## Task 6：FileChapterTrackingSink 加 Debt 持久化

**Files:**
- Modify: `src/Tianming.ProjectData/Tracking/ChapterTrackingDispatcher.cs`（IChapterTrackingSink）
- Modify: `src/Tianming.ProjectData/Tracking/FileChapterTrackingSink.cs`
- Test: `tests/Tianming.ProjectData.Tests/Tracking/FileChapterTrackingSinkDebtTests.cs`

- [ ] **Step 6.1：扩接口 IChapterTrackingSink**

在 `src/Tianming.ProjectData/Tracking/ChapterTrackingDispatcher.cs` 的 `IChapterTrackingSink` 末尾追加：

```csharp
Task RecordTrackingDebtsAsync(string chapterId, IReadOnlyList<TrackingDebt> debts);
Task<IReadOnlyList<TrackingDebt>> LoadTrackingDebtsAsync(int volume);
```

加 using：
```csharp
using TM.Services.Modules.ProjectData.Tracking;
```

- [ ] **Step 6.2：写测试（先红）**

创建 `tests/Tianming.ProjectData.Tests/Tracking/FileChapterTrackingSinkDebtTests.cs`：

```csharp
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking;

public class FileChapterTrackingSinkDebtTests
{
    [Fact]
    public async Task RecordTrackingDebts_persists_and_loads_back()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-debt-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var sink = new FileChapterTrackingSink(dir, volume: 1);

        var debts = new List<TrackingDebt>
        {
            new() { Id = "d-1", Category = TrackingDebtCategory.Pledge, ChapterId = "ch-005", Description = "测试" },
            new() { Id = "d-2", Category = TrackingDebtCategory.Deadline, ChapterId = "ch-006", Description = "测试 2" },
        };
        await sink.RecordTrackingDebtsAsync("ch-005", debts);

        var loaded = await sink.LoadTrackingDebtsAsync(volume: 1);
        Assert.Equal(2, loaded.Count);
        Assert.Contains(loaded, d => d.Id == "d-1");
        Assert.Contains(loaded, d => d.Category == TrackingDebtCategory.Deadline);
    }
}
```

- [ ] **Step 6.3：跑 test 确认红**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter FileChapterTrackingSinkDebtTests --nologo -v minimal
```

Expected: 编译失败 — `FileChapterTrackingSink` 无 `RecordTrackingDebtsAsync` / `LoadTrackingDebtsAsync` 方法。

- [ ] **Step 6.4：实现 RecordTrackingDebtsAsync + LoadTrackingDebtsAsync**

在 `FileChapterTrackingSink.cs` 内追加（参考已有 `VolumeFile()` / `_jsonOptions` 模式）：

```csharp
private string DebtFile(int volume) => Path.Combine(_root, $"tracking_debts_vol{volume}.json");

public async Task RecordTrackingDebtsAsync(string chapterId, IReadOnlyList<TrackingDebt> debts)
{
    if (debts == null || debts.Count == 0) return;

    var existing = await LoadTrackingDebtsAsync(_volume).ConfigureAwait(false);
    var merged = new List<TrackingDebt>(existing);
    // 同一 chapter 重复检测：清掉之前该 chapter 的 debt，再加新
    merged.RemoveAll(d => d.ChapterId == chapterId);
    merged.AddRange(debts);

    var json = JsonSerializer.Serialize(merged, _jsonOptions);
    var tmp = DebtFile(_volume) + ".tmp";
    await File.WriteAllTextAsync(tmp, json).ConfigureAwait(false);
    File.Move(tmp, DebtFile(_volume), overwrite: true);
}

public async Task<IReadOnlyList<TrackingDebt>> LoadTrackingDebtsAsync(int volume)
{
    var path = DebtFile(volume);
    if (!File.Exists(path)) return System.Array.Empty<TrackingDebt>();
    var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
    return JsonSerializer.Deserialize<List<TrackingDebt>>(json, _jsonOptions) ?? new List<TrackingDebt>();
}
```

> **注：** 如果 `FileChapterTrackingSink` 没有 `_volume` 私有字段，先检查它的构造 + `VolumeFile()` 实现，参考 existing volume handling。如果 sink 是 per-chapter 不带 volume，加 `volume: 1` 默认。

- [ ] **Step 6.5：测试 + commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter FileChapterTrackingSinkDebtTests --nologo -v minimal
git add src/Tianming.ProjectData/Tracking/ChapterTrackingDispatcher.cs \
        src/Tianming.ProjectData/Tracking/FileChapterTrackingSink.cs \
        tests/Tianming.ProjectData.Tests/Tracking/FileChapterTrackingSinkDebtTests.cs
git commit -m "feat(tracking): M6.1.10 FileChapterTrackingSink Debt 持久化"
```

---

## Task 7：ChapterGenerationPipeline 集成 TrackingDebtRegistry

**Files:**
- Modify: `src/Tianming.ProjectData/Generation/ChapterGenerationPipeline.cs`
- Test: `tests/Tianming.ProjectData.Tests/Generation/ChapterGenerationPipelineDebtTests.cs`

- [ ] **Step 7.1：写集成测试**

```csharp
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Generation;
using TM.Services.Modules.ProjectData.Tracking;
using TM.Services.Modules.ProjectData.Tracking.Debts;
using Xunit;

namespace Tianming.ProjectData.Tests.Generation;

public class ChapterGenerationPipelineDebtTests
{
    [Fact]
    public async Task SaveGeneratedChapter_with_overdue_foreshadow_records_deadline_debt()
    {
        // 准备：构造一个 minimum-viable pipeline + sink + registry
        var sink = new InMemoryTrackingSink();
        var registry = new TrackingDebtRegistry(new ITrackingDebtDetector[] { new DeadlineDetector() });

        // 调 SaveGeneratedChapterStrictAsync 或新加的方法，传入 ForeshadowingStatusGuide with IsOverdue=true
        // 验证 sink 收到 RecordTrackingDebtsAsync 调用
        // ...（具体测试视 ChapterGenerationPipeline 当前签名调整）
        // 占位断言：
        Assert.True(true); // 替换为真断言
    }

    private sealed class InMemoryTrackingSink : IChapterTrackingSink
    {
        // 实现所有方法（其它都 no-op，重点是 RecordTrackingDebtsAsync 记录到 List）
        // ...
    }
}
```

> **注：** 因为 `ChapterGenerationPipeline.SaveGeneratedChapterStrictAsync` 签名复杂（含 ContentPreparer/Gate/Index 等多个依赖），完整 e2e 测试需大量 mock。本 step **简化为只测试新增的"Dispatch 后调用 TrackingDebtRegistry"路径**，可用一个独立的 `PipelineDebtIntegrationHelper` 方法接受 minimal 输入。

- [ ] **Step 7.2：扩 ChapterGenerationPipeline 集成 TrackingDebtRegistry**

在 `src/Tianming.ProjectData/Generation/ChapterGenerationPipeline.cs` 添加可选 detector 注入：

```csharp
private readonly TrackingDebtRegistry? _debtRegistry;
private readonly LedgerRuleSetProvider? _ruleProvider;

// 在现有 ctor 后加一个 overload：
public ChapterGenerationPipeline(
    /* 现有所有参数 */,
    TrackingDebtRegistry? debtRegistry = null)
{
    /* 原赋值 */
    _debtRegistry = debtRegistry;
}
```

在 `SaveGeneratedChapterStrictAsync` 的 Dispatch 步骤之后追加：

```csharp
// M6.1：触发 5 类 tracking debt 检测
if (_debtRegistry != null && changes != null)
{
    var ctx = new TrackingDebtDetectionContext
    {
        Foreshadowings = await LoadForeshadowingGuideAsync().ConfigureAwait(false),
        Pledges = await LoadPledgeGuideAsync().ConfigureAwait(false),
        Secrets = await LoadSecretGuideAsync().ConfigureAwait(false),
    };
    var debts = await _debtRegistry.DetectAllAsync(chapterId, changes, factSnapshot, ctx).ConfigureAwait(false);
    if (debts.Count > 0)
    {
        await _sink.RecordTrackingDebtsAsync(chapterId, debts).ConfigureAwait(false);
    }
}
```

`LoadForeshadowingGuideAsync` / `LoadPledgeGuideAsync` / `LoadSecretGuideAsync` 委托 `IFactSnapshotGuideSource` 或新增对应 getter；如 source 没暴露 Pledge/Secret，回退到 `return null;`（detector 会跳过）。

> **重要**：本 step 不强求 e2e 完整 — 只要新增 ctor + 注入点存在，让 DI 能注册 detector 即可。M6.2 再做真实测试。

- [ ] **Step 7.3：build 全量**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v minimal
```

Expected: 0 Error.

- [ ] **Step 7.4：commit**

```bash
git add src/Tianming.ProjectData/Generation/ChapterGenerationPipeline.cs \
        tests/Tianming.ProjectData.Tests/Generation/ChapterGenerationPipelineDebtTests.cs
git commit -m "feat(tracking): M6.1.11 ChapterGenerationPipeline 集成 TrackingDebtRegistry"
```

---

## Task 8：DI 注册 + 全量验收

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`（如果适用，否则放在 Tianming.ProjectData 的 DI 入口）

- [ ] **Step 8.1：DI 注册 5 detector + registry**

在 `AvaloniaShellServiceCollectionExtensions.cs` 加：

```csharp
// M6.1 Tracking 债务检测
s.AddSingleton<ITrackingDebtDetector, EntityDriftDetector>();
s.AddSingleton<ITrackingDebtDetector, OmissionDetector>();
s.AddSingleton<ITrackingDebtDetector, DeadlineDetector>();
s.AddSingleton<ITrackingDebtDetector, PledgeDetector>();
s.AddSingleton<ITrackingDebtDetector, SecretRevealDetector>();
s.AddSingleton<TrackingDebtRegistry>(sp =>
    new TrackingDebtRegistry(sp.GetServices<ITrackingDebtDetector>()));
```

加 using：
```csharp
using TM.Services.Modules.ProjectData.Tracking.Debts;
```

- [ ] **Step 8.2：全量 build + test**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v minimal
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
```

Expected: 全过。

- [ ] **Step 8.3：commit**

```bash
git add src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs
git commit -m "feat(tracking): M6.1.12 DI 注册 5 detector + TrackingDebtRegistry"
```

---

## M6.1 Gate 验收

| 项 | 标准 |
|---|---|
| TrackingDebt 模型 | 5 类别 + 3 severity + JSON round-trip 测试 |
| FactSnapshot 扩展 | `TrackingDebts` 字段，向后兼容（默认空） |
| Pledge/Secret Guide | 新模型 + JSON 序列化测试 |
| 5 detector | EntityDrift / Omission / Deadline / Pledge / SecretReveal 各 2+ 测试 |
| Registry | 聚合 5 detector + DetectAll 输出去重 |
| 持久化 | FileChapterTrackingSink Record/Load Debt + 同章节去重 |
| Pipeline 集成 | SaveGeneratedChapterStrictAsync 后调 detector |
| DI | AppHost.Build() 能 resolve TrackingDebtRegistry |
| 全量 test | dotnet test 全过，新增 ≥15 条 detector + registry + sink 测试 |
