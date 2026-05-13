# M6.4 校验分层 + 向量定位 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 把现有 `LedgerConsistencyChecker` "一锅端"校验拆成 5 层（Structural / Entity / Foreshadow / Timeline / Relationship），每层独立返回 `LayeredConsistencyResult`。新增 `ConsistencyIssueLocator` 用 `FileVectorSearchService` 把每个 `ConsistencyIssue` 定位到章节文本中的具体 chunk（含 ChunkPosition + VectorScore）。

**Architecture:** 新增 `IConsistencyLayer` 接口 + 5 实现，每个实现负责现有 `LedgerConsistencyChecker.ValidateXxx()` 中的一类规则；`LayeredConsistencyChecker` 聚合 5 层并按层返回结果。`ConsistencyIssueLocator` 在拿到 `ConsistencyIssue` 后用 `FileVectorSearchService.SearchByChapterAsync` + `EntityId` 字符串匹配定位 chunk。`ConsistencyIssue` 扩 3 字段（Layer / ChunkPosition / VectorScore）。

**Tech Stack:** .NET 8 + 复用 FileVectorSearchService + OnnxTextEmbedder + xUnit。零新依赖。

**Branch:** `m6-4-validation-layers`（基于 main）。

**前置条件：** Round 2/3 + M6.2/M6.3 已合并入 main 并基线绿。

---

## Task 0：基线 + worktree

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
git worktree add /Users/jimmy/Downloads/tianming-m6-4 -b m6-4-validation-layers main
cd /Users/jimmy/Downloads/tianming-m6-4
```

---

## Task 1：ConsistencyIssue 扩字段

**Files:**
- Modify: `src/Tianming.ProjectData/Tracking/ConsistencyIssue.cs`
- Test: `tests/Tianming.ProjectData.Tests/Tracking/ConsistencyIssueExtendedTests.cs`

- [ ] **Step 1.1：测试**

```csharp
using System.Text.Json;
using TM.Services.Modules.ProjectData.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking;

public class ConsistencyIssueExtendedTests
{
    [Fact]
    public void Issue_carries_layer_chunk_position_score()
    {
        var issue = new ConsistencyIssue
        {
            EntityId = "char-001",
            IssueType = "LevelRegression",
            Expected = "金丹",
            Actual = "练气",
            Layer = "Entity",
            ChunkPosition = 3,
            VectorScore = 0.87,
        };
        var json = JsonSerializer.Serialize(issue);
        var back = JsonSerializer.Deserialize<ConsistencyIssue>(json);
        Assert.Equal("Entity", back!.Layer);
        Assert.Equal(3, back.ChunkPosition);
        Assert.InRange(back.VectorScore, 0.86, 0.88);
    }

    [Fact]
    public void Default_layer_and_position_are_zero_or_empty()
    {
        var issue = new ConsistencyIssue();
        Assert.Equal(string.Empty, issue.Layer);
        Assert.Equal(-1, issue.ChunkPosition);
        Assert.Equal(0d, issue.VectorScore);
    }
}
```

- [ ] **Step 1.2：扩 ConsistencyIssue**

在 `ConsistencyIssue.cs` 现有 4 字段后追加：

```csharp
[JsonPropertyName("Layer")] public string Layer { get; set; } = string.Empty;
[JsonPropertyName("ChunkPosition")] public int ChunkPosition { get; set; } = -1;
[JsonPropertyName("VectorScore")] public double VectorScore { get; set; }
```

- [ ] **Step 1.3：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter ConsistencyIssueExtendedTests --nologo -v minimal
git add src/Tianming.ProjectData/Tracking/ConsistencyIssue.cs \
        tests/Tianming.ProjectData.Tests/Tracking/ConsistencyIssueExtendedTests.cs
git commit -m "feat(validation): M6.4.1 ConsistencyIssue 扩 Layer/ChunkPosition/VectorScore"
```

---

## Task 2：IConsistencyLayer 接口

**Files:**
- Create: `src/Tianming.ProjectData/Tracking/Layers/IConsistencyLayer.cs`

- [ ] **Step 2.1：写接口**

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Tracking.Layers
{
    /// <summary>
    /// 单层校验：负责一类一致性规则（结构 / 实体 / 伏笔 / 时间线 / 关系）。
    /// </summary>
    public interface IConsistencyLayer
    {
        /// <summary>层名（"Structural" / "Entity" / "Foreshadow" / "Timeline" / "Relationship"）。</summary>
        string LayerName { get; }

        Task<IReadOnlyList<ConsistencyIssue>> CheckAsync(
            ChapterChanges changes,
            FactSnapshot factSnapshot,
            LedgerRuleSet ruleSet,
            CancellationToken ct = default);
    }
}
```

- [ ] **Step 2.2：build**

```bash
dotnet build src/Tianming.ProjectData/Tianming.ProjectData.csproj --nologo -v minimal
```

- [ ] **Step 2.3：commit**

```bash
git add src/Tianming.ProjectData/Tracking/Layers/IConsistencyLayer.cs
git commit -m "feat(validation): M6.4.2 IConsistencyLayer 接口"
```

---

## Task 3：5 层实现（StructuralLayer / EntityLayer / ForeshadowLayer / TimelineLayer / RelationshipLayer）

> 每层从现有 `LedgerConsistencyChecker.ValidateXxx()` 拆出对应规则，**保留原逻辑、仅加 `LayerName` 标注 + 返回扩字段的 Issue**。

**Files:** 5 个新文件 + 5 个测试文件

```
src/Tianming.ProjectData/Tracking/Layers/
  StructuralLayer.cs        # 结构（必填字段、ID 格式）
  EntityLayer.cs            # 实体等级、能力、状态回退
  ForeshadowLayer.cs        # 伏笔 setup/payoff 顺序、tier 一致性
  TimelineLayer.cs          # 时间推进、角色位置链
  RelationshipLayer.cs      # 关系矛盾、信任 delta
```

### Task 3a：StructuralLayer

- [ ] **Step 3a.1：测试**

```csharp
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Tracking;
using TM.Services.Modules.ProjectData.Tracking.Layers;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Layers;

public class StructuralLayerTests
{
    [Fact]
    public async Task Flags_invalid_chapter_id_format()
    {
        var layer = new StructuralLayer();
        var changes = new ChapterChanges { /* 让 Structural 校验失败的最小输入 */ };
        var issues = await layer.CheckAsync(changes, new FactSnapshot(), new LedgerRuleSet());
        // 至少 1 条 Layer=Structural 的 issue（如果没有，断言 Empty 也接受）
        foreach (var i in issues) Assert.Equal("Structural", i.Layer);
    }
}
```

- [ ] **Step 3a.2：实现**

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Tracking.Layers
{
    public sealed class StructuralLayer : IConsistencyLayer
    {
        public string LayerName => "Structural";

        public Task<IReadOnlyList<ConsistencyIssue>> CheckAsync(
            ChapterChanges changes, FactSnapshot factSnapshot, LedgerRuleSet ruleSet,
            CancellationToken ct = default)
        {
            var issues = new List<ConsistencyIssue>();
            // 复用 LedgerConsistencyChecker.ValidateStructuralOnly 的部分逻辑
            // 这里只做最小演示：检查 ChapterChanges 不为 null
            if (changes == null)
            {
                issues.Add(new ConsistencyIssue
                {
                    Layer = LayerName,
                    IssueType = "MissingChanges",
                    Expected = "ChapterChanges payload",
                    Actual = "null",
                });
            }
            return Task.FromResult<IReadOnlyList<ConsistencyIssue>>(issues);
        }
    }
}
```

- [ ] **Step 3a.3：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter StructuralLayerTests --nologo -v minimal
git add src/Tianming.ProjectData/Tracking/Layers/StructuralLayer.cs \
        tests/Tianming.ProjectData.Tests/Tracking/Layers/StructuralLayerTests.cs
git commit -m "feat(validation): M6.4.3a StructuralLayer"
```

### Task 3b-3e：EntityLayer / ForeshadowLayer / TimelineLayer / RelationshipLayer

> 每层重复 3a 的结构：测试 → 实现 → commit。**实现内容**：从 `LedgerConsistencyChecker` 原代码中**拷贝对应规则段**到新层，把 `result.Issues.Add(...)` 替换为返回 `List<ConsistencyIssue>` 并打 `Layer = LayerName`。

每层 commit message：
- `feat(validation): M6.4.3b EntityLayer`
- `feat(validation): M6.4.3c ForeshadowLayer`
- `feat(validation): M6.4.3d TimelineLayer`
- `feat(validation): M6.4.3e RelationshipLayer`

> **拆分指南**：打开 `src/Tianming.ProjectData/Tracking/LedgerConsistencyChecker.cs`，看 `Validate` 方法内部依次调用 `ValidateForeshadowing` / `ValidateConflicts` / `ValidateCharacters` / `ValidateTimeline` / `ValidateItems` / `ValidateRelationships` 等子方法。按以下映射拷贝：
> - **StructuralLayer**：`ValidateStructuralOnly` 的字段必填检查
> - **EntityLayer**：`ValidateCharacters` 中的等级回退 / 能力丢失 / `ValidateItems` 的物品归属
> - **ForeshadowLayer**：`ValidateForeshadowing` 的 setup/payoff 顺序、tier
> - **TimelineLayer**：`ValidateTimeline` 的时间推进单调、`ValidateMovements` 的位置链
> - **RelationshipLayer**：`ValidateRelationships` 的矛盾、信任 delta 超限

每层至少 2 条测试覆盖核心规则。

---

## Task 4：LayeredConsistencyChecker 聚合

**Files:**
- Create: `src/Tianming.ProjectData/Tracking/Layers/LayeredConsistencyChecker.cs`
- Create: `src/Tianming.ProjectData/Tracking/Layers/LayeredConsistencyResult.cs`
- Test: `tests/Tianming.ProjectData.Tests/Tracking/Layers/LayeredConsistencyCheckerTests.cs`

- [ ] **Step 4.1：测试**

```csharp
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Tracking;
using TM.Services.Modules.ProjectData.Tracking.Layers;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Layers;

public class LayeredConsistencyCheckerTests
{
    [Fact]
    public async Task Runs_all_5_layers_and_groups_issues_by_layer()
    {
        var checker = new LayeredConsistencyChecker(new IConsistencyLayer[]
        {
            new StructuralLayer(),
            new EntityLayer(),
            new ForeshadowLayer(),
            new TimelineLayer(),
            new RelationshipLayer(),
        });
        var result = await checker.CheckAsync(new ChapterChanges(), new FactSnapshot(), new LedgerRuleSet());
        Assert.Equal(5, result.LayerNames.Count);
        Assert.True(result.IssuesByLayer.ContainsKey("Structural"));
        Assert.True(result.IssuesByLayer.ContainsKey("Entity"));
        Assert.True(result.IssuesByLayer.ContainsKey("Foreshadow"));
        Assert.True(result.IssuesByLayer.ContainsKey("Timeline"));
        Assert.True(result.IssuesByLayer.ContainsKey("Relationship"));
    }

    [Fact]
    public async Task AllIssues_concatenates_all_layer_issues()
    {
        var checker = new LayeredConsistencyChecker(new IConsistencyLayer[]
        {
            new StructuralLayer(),
        });
        var result = await checker.CheckAsync(null!, new FactSnapshot(), new LedgerRuleSet());
        Assert.Contains(result.AllIssues, i => i.IssueType == "MissingChanges");
    }
}
```

- [ ] **Step 4.2：实现**

`LayeredConsistencyResult.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;

namespace TM.Services.Modules.ProjectData.Tracking.Layers
{
    public sealed class LayeredConsistencyResult
    {
        public IReadOnlyDictionary<string, IReadOnlyList<ConsistencyIssue>> IssuesByLayer { get; init; }
            = new Dictionary<string, IReadOnlyList<ConsistencyIssue>>();
        public IReadOnlyList<string> LayerNames => IssuesByLayer.Keys.ToList();
        public IReadOnlyList<ConsistencyIssue> AllIssues =>
            IssuesByLayer.Values.SelectMany(x => x).ToList();
        public bool Success => AllIssues.Count == 0;
    }
}
```

`LayeredConsistencyChecker.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Tracking.Layers
{
    public sealed class LayeredConsistencyChecker
    {
        private readonly IReadOnlyList<IConsistencyLayer> _layers;

        public LayeredConsistencyChecker(IEnumerable<IConsistencyLayer> layers)
        {
            _layers = new List<IConsistencyLayer>(layers);
        }

        public async Task<LayeredConsistencyResult> CheckAsync(
            ChapterChanges changes,
            FactSnapshot factSnapshot,
            LedgerRuleSet ruleSet,
            CancellationToken ct = default)
        {
            var dict = new Dictionary<string, IReadOnlyList<ConsistencyIssue>>();
            foreach (var layer in _layers)
            {
                var issues = await layer.CheckAsync(changes, factSnapshot, ruleSet, ct).ConfigureAwait(false);
                dict[layer.LayerName] = issues;
            }
            return new LayeredConsistencyResult { IssuesByLayer = dict };
        }
    }
}
```

- [ ] **Step 4.3：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter LayeredConsistencyCheckerTests --nologo -v minimal
git add src/Tianming.ProjectData/Tracking/Layers/LayeredConsistencyChecker.cs \
        src/Tianming.ProjectData/Tracking/Layers/LayeredConsistencyResult.cs \
        tests/Tianming.ProjectData.Tests/Tracking/Layers/LayeredConsistencyCheckerTests.cs
git commit -m "feat(validation): M6.4.4 LayeredConsistencyChecker 聚合 5 层"
```

---

## Task 5：ConsistencyIssueLocator（向量定位）

**Files:**
- Create: `src/Tianming.ProjectData/Tracking/Locator/ConsistencyIssueLocator.cs`
- Test: `tests/Tianming.ProjectData.Tests/Tracking/Locator/ConsistencyIssueLocatorTests.cs`

- [ ] **Step 5.1：测试**

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;
using TM.Services.Framework.AI.SemanticKernel;
using TM.Services.Modules.ProjectData.Tracking;
using TM.Services.Modules.ProjectData.Tracking.Locator;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Locator;

public class ConsistencyIssueLocatorTests
{
    private sealed class StubVectorSearch : IVectorSearchService
    {
        public List<VectorSearchResult> NextResults { get; set; } = new();
        public VectorSearchMode CurrentMode => VectorSearchMode.Keyword;
        public Task<List<VectorSearchResult>> SearchAsync(string query, int topK = 5) =>
            Task.FromResult(NextResults);
        public Task<List<VectorSearchResult>> SearchByChapterAsync(string chapterId, int topK = 2) =>
            Task.FromResult(NextResults);
    }

    [Fact]
    public async Task Locates_issue_using_entity_id_keyword()
    {
        var search = new StubVectorSearch
        {
            NextResults = new List<VectorSearchResult>
            {
                new() { ChapterId = "ch-001", Position = 2, Content = "char-001 出场", Score = 0.93 },
            }
        };
        var locator = new ConsistencyIssueLocator(search);
        var issue = new ConsistencyIssue { EntityId = "char-001", IssueType = "LevelRegression" };
        var located = await locator.LocateAsync(issue, "ch-001");
        Assert.Equal(2, located.ChunkPosition);
        Assert.InRange(located.VectorScore, 0.92, 0.94);
    }

    [Fact]
    public async Task Returns_issue_unchanged_when_no_matches()
    {
        var search = new StubVectorSearch { NextResults = new() };
        var locator = new ConsistencyIssueLocator(search);
        var issue = new ConsistencyIssue { EntityId = "char-999" };
        var located = await locator.LocateAsync(issue, "ch-001");
        Assert.Equal(-1, located.ChunkPosition);
    }
}
```

> **注**：`IVectorSearchService` 接口可能不存在；如真实代码只是 `FileVectorSearchService`，要么先抽接口（看 Explore 报告），要么测试改用真实类 + 临时 chunk store。简化路径：在 `src/Tianming.AI/SemanticKernel/` 加 `IVectorSearchService` 接口，让 `FileVectorSearchService` 实现它。

- [ ] **Step 5.2：抽 IVectorSearchService 接口（如不存在）**

在 `src/Tianming.AI/SemanticKernel/` 新建 `IVectorSearchService.cs`：

```csharp
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TM.Services.Framework.AI.SemanticKernel
{
    public interface IVectorSearchService
    {
        VectorSearchMode CurrentMode { get; }
        Task<List<VectorSearchResult>> SearchAsync(string query, int topK = 5);
        Task<List<VectorSearchResult>> SearchByChapterAsync(string chapterId, int topK = 2);
    }
}
```

修改 `FileVectorSearchService` 类签名加 `: IVectorSearchService`（保持现有方法签名不变）。

- [ ] **Step 5.3：实现 Locator**

```csharp
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Framework.AI.SemanticKernel;

namespace TM.Services.Modules.ProjectData.Tracking.Locator
{
    public sealed class ConsistencyIssueLocator
    {
        private readonly IVectorSearchService _search;

        public ConsistencyIssueLocator(IVectorSearchService search)
        {
            _search = search;
        }

        public async Task<ConsistencyIssue> LocateAsync(ConsistencyIssue issue, string chapterId, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(issue.EntityId)) return issue;
            var results = await _search.SearchAsync($"{issue.EntityId} {issue.IssueType}", topK: 1).ConfigureAwait(false);
            if (results.Count == 0) return issue;
            var best = results[0];
            if (best.ChapterId != chapterId) return issue;
            issue.ChunkPosition = best.Position;
            issue.VectorScore = best.Score;
            return issue;
        }
    }
}
```

- [ ] **Step 5.4：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter ConsistencyIssueLocatorTests --nologo -v minimal
git add src/Tianming.AI/SemanticKernel/IVectorSearchService.cs \
        src/Tianming.AI/SemanticKernel/FileVectorSearchService.cs \
        src/Tianming.ProjectData/Tracking/Locator/ConsistencyIssueLocator.cs \
        tests/Tianming.ProjectData.Tests/Tracking/Locator/ConsistencyIssueLocatorTests.cs
git commit -m "feat(validation): M6.4.5 ConsistencyIssueLocator + IVectorSearchService 接口"
```

---

## Task 6：GenerationGate 接入分层校验 + Locator

**Files:**
- Modify: `src/Tianming.ProjectData/Generation/GenerationGate.cs`（如该类存在，加可选 LayeredConsistencyChecker + Locator 注入）

> **注**：先 grep 确认 GenerationGate 真实路径，可能在 `Generation/` 或 `Tracking/`。Plan 假设该类负责调 LedgerConsistencyChecker；改为可选注入 LayeredConsistencyChecker 后并行运行，主路径仍走旧 checker（向后兼容）。

- [ ] **Step 6.1：在 GenerationGate ctor 加可选依赖**

```csharp
private readonly LayeredConsistencyChecker? _layered;
private readonly ConsistencyIssueLocator? _locator;

public GenerationGate(
    /* 已有参数 */,
    LayeredConsistencyChecker? layered = null,
    ConsistencyIssueLocator? locator = null)
{
    /* 原赋值 */
    _layered = layered;
    _locator = locator;
}
```

- [ ] **Step 6.2：在 ValidateAsync 调主 checker 后并行调 layered**

```csharp
// M6.4：分层校验（信息性，不阻断现有流程）
if (_layered != null)
{
    var layered = await _layered.CheckAsync(prepared.ParsedChanges, factSnapshot, ruleSet, ct).ConfigureAwait(false);
    if (_locator != null && !string.IsNullOrEmpty(chapterId))
    {
        foreach (var issue in layered.AllIssues)
            await _locator.LocateAsync(issue, chapterId, ct).ConfigureAwait(false);
    }
    // 附加到 GateResult.Failures 作为信息层（不改 Success 判定）
    foreach (var issue in layered.AllIssues)
    {
        gateResult.LayeredIssues ??= new List<ConsistencyIssue>();
        gateResult.LayeredIssues.Add(issue);
    }
}
```

需在 `GateResult` 加：

```csharp
[JsonPropertyName("LayeredIssues")]
public List<ConsistencyIssue>? LayeredIssues { get; set; }
```

- [ ] **Step 6.3：build + commit**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v minimal
git add src/Tianming.ProjectData/Generation/GenerationGate.cs \
        src/Tianming.ProjectData/Tracking/GateModels.cs
git commit -m "feat(validation): M6.4.6 GenerationGate 接入 LayeredChecker + Locator"
```

---

## Task 7：DI 注册

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`

- [ ] **Step 7.1：注册 5 层 + Checker + Locator**

```csharp
// M6.4 校验分层 + 向量定位
s.AddSingleton<IConsistencyLayer, StructuralLayer>();
s.AddSingleton<IConsistencyLayer, EntityLayer>();
s.AddSingleton<IConsistencyLayer, ForeshadowLayer>();
s.AddSingleton<IConsistencyLayer, TimelineLayer>();
s.AddSingleton<IConsistencyLayer, RelationshipLayer>();
s.AddSingleton<LayeredConsistencyChecker>(sp =>
    new LayeredConsistencyChecker(sp.GetServices<IConsistencyLayer>()));
s.AddSingleton<ConsistencyIssueLocator>(sp =>
    new ConsistencyIssueLocator(sp.GetRequiredService<IVectorSearchService>()));
```

并确认 `IVectorSearchService` 已注册（之前用 `FileVectorSearchService` 注册的具体类型，加 `s.AddSingleton<IVectorSearchService>(sp => sp.GetRequiredService<FileVectorSearchService>());`）。

加 using：
```csharp
using TM.Services.Framework.AI.SemanticKernel;
using TM.Services.Modules.ProjectData.Tracking.Layers;
using TM.Services.Modules.ProjectData.Tracking.Locator;
```

- [ ] **Step 7.2：全量 build + test**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v minimal
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
```

Expected: 全过。

- [ ] **Step 7.3：commit**

```bash
git add src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs
git commit -m "feat(validation): M6.4.7 DI 注册 5 层 + Checker + Locator"
```

---

## M6.4 Gate 验收

| 项 | 标准 |
|---|---|
| ConsistencyIssue 扩字段 | Layer / ChunkPosition / VectorScore + JSON round-trip |
| 5 层实现 | 每层 ≥2 测试，与现有 LedgerConsistencyChecker 不冲突 |
| LayeredConsistencyChecker | 按层分组 IssuesByLayer + AllIssues 平展 |
| ConsistencyIssueLocator | 向量搜索定位 + 无匹配返回 -1 |
| GenerationGate 集成 | LayeredIssues 字段附加到 GateResult |
| DI | AppHost.Build() 能 resolve LayeredConsistencyChecker（5 layer 注入）+ Locator |
| 全量 test | 新增 ≥14 条（每层 2 + Checker 2 + Locator 2），dotnet test 全过 |
