# M6.5 ContextService 拆分 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** 把分散在 ChapterValidationPromptContextResolver / FactSnapshot / DesignElementNames / ContextIdCollection 4 处的"组装 AI 上下文"逻辑拆成 4 个职责单一的 ContextService：`IDesignContextService`（设计模块查参考）/ `IGenerationContextService`（章节生成查 fact）/ `IValidationContextService`（校验查规则）/ `IPackagingContextService`（打包查全局）。每个服务封装相应的 ModuleDataAdapter 调用 + Snapshot 组装。

**Architecture:** 4 个独立接口 + 4 个文件实现，统一在 `src/Tianming.ProjectData/Context/`。每个 service 都接受 DI 注入相关 ModuleDataAdapter / FileChapterTrackingSink / FactSnapshot extractor 等已有组件，对外暴露干净的 "Build* / Get* / Snapshot*" 方法。**不重写现有 resolver/mapper**，只在新 service 内调用它们。

**Tech Stack:** .NET 8 + 复用现有 ModuleDataAdapter / PortableFactSnapshotExtractor / FileChapterTrackingSink / xUnit。零新依赖。

**Branch:** `m6-5-context-service-split`（基于 main）。

**前置条件：** Round 2/3 + M6.2/M6.3/M6.4 已合并入 main 并基线绿。

---

## Task 0：基线 + worktree

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
git worktree add /Users/jimmy/Downloads/tianming-m6-5 -b m6-5-context-service-split main
cd /Users/jimmy/Downloads/tianming-m6-5
```

---

## Task 1：IDesignContextService（设计模块查参考）

**Files:**
- Create: `src/Tianming.ProjectData/Context/IDesignContextService.cs`
- Create: `src/Tianming.ProjectData/Context/DesignContextService.cs`
- Test: `tests/Tianming.ProjectData.Tests/Context/DesignContextServiceTests.cs`

- [ ] **Step 1.1：写接口**

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Context
{
    public interface IDesignContextService
    {
        /// <summary>查指定 category 的所有 design 数据条目（角色/世界观/势力/地点/剧情/创意素材）。</summary>
        Task<IReadOnlyList<DesignReference>> ListByCategoryAsync(string category, CancellationToken ct = default);

        /// <summary>按关键字模糊搜索（跨 category）。</summary>
        Task<IReadOnlyList<DesignReference>> SearchAsync(string query, CancellationToken ct = default);

        /// <summary>取指定 ID 的设计条目。</summary>
        Task<DesignReference?> GetByIdAsync(string id, CancellationToken ct = default);
    }

    public sealed class DesignReference
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string RawJson { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 1.2：测试**

```csharp
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Context;
using Xunit;

namespace Tianming.ProjectData.Tests.Context;

public class DesignContextServiceTests
{
    [Fact]
    public async Task ListByCategory_returns_empty_for_unknown_category()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-dc-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var svc = new DesignContextService(root);
        var list = await svc.ListByCategoryAsync("UnknownCategory");
        Assert.Empty(list);
    }

    [Fact]
    public async Task Search_finds_match_in_any_category()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-dc-{System.Guid.NewGuid():N}");
        var charDir = Path.Combine(root, "Design", "Elements", "Characters");
        Directory.CreateDirectory(charDir);
        await File.WriteAllTextAsync(Path.Combine(charDir, "char-001.json"),
            "{\"Id\":\"char-001\",\"Name\":\"沈砚\",\"Summary\":\"剑客\"}");
        var svc = new DesignContextService(root);
        var results = await svc.SearchAsync("沈砚");
        Assert.NotEmpty(results);
        Assert.Contains(results, r => r.Name == "沈砚");
    }
}
```

- [ ] **Step 1.3：实现**

```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Context
{
    public sealed class DesignContextService : IDesignContextService
    {
        private static readonly IReadOnlyDictionary<string, string> CategorySubDirs = new Dictionary<string, string>
        {
            ["Characters"] = "Design/Elements/Characters",
            ["WorldRules"] = "Design/GlobalSettings/WorldRules",
            ["Factions"] = "Design/Elements/Factions",
            ["Locations"] = "Design/Elements/Locations",
            ["Plot"] = "Design/Elements/Plot",
            ["CreativeMaterials"] = "Design/Templates/CreativeMaterials",
        };

        private readonly string _projectRoot;

        public DesignContextService(string projectRoot)
        {
            _projectRoot = projectRoot;
        }

        public async Task<IReadOnlyList<DesignReference>> ListByCategoryAsync(string category, CancellationToken ct = default)
        {
            if (!CategorySubDirs.TryGetValue(category, out var sub))
                return System.Array.Empty<DesignReference>();
            var dir = Path.Combine(_projectRoot, sub);
            if (!Directory.Exists(dir)) return System.Array.Empty<DesignReference>();

            var results = new List<DesignReference>();
            foreach (var file in Directory.GetFiles(dir, "*.json", SearchOption.AllDirectories))
            {
                ct.ThrowIfCancellationRequested();
                var dr = await ParseAsync(file, category, ct).ConfigureAwait(false);
                if (dr != null) results.Add(dr);
            }
            return results;
        }

        public async Task<IReadOnlyList<DesignReference>> SearchAsync(string query, CancellationToken ct = default)
        {
            var results = new List<DesignReference>();
            foreach (var (cat, _) in CategorySubDirs)
            {
                var list = await ListByCategoryAsync(cat, ct).ConfigureAwait(false);
                results.AddRange(list.Where(r =>
                    r.Name.Contains(query, System.StringComparison.OrdinalIgnoreCase) ||
                    r.Summary.Contains(query, System.StringComparison.OrdinalIgnoreCase) ||
                    r.RawJson.Contains(query, System.StringComparison.OrdinalIgnoreCase)));
            }
            return results;
        }

        public async Task<DesignReference?> GetByIdAsync(string id, CancellationToken ct = default)
        {
            foreach (var (cat, _) in CategorySubDirs)
            {
                var list = await ListByCategoryAsync(cat, ct).ConfigureAwait(false);
                var match = list.FirstOrDefault(r => r.Id == id);
                if (match != null) return match;
            }
            return null;
        }

        private static async Task<DesignReference?> ParseAsync(string file, string category, CancellationToken ct)
        {
            try
            {
                var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                var node = JsonNode.Parse(json) as JsonObject;
                if (node == null) return null;
                return new DesignReference
                {
                    Id = node["Id"]?.ToString() ?? Path.GetFileNameWithoutExtension(file),
                    Name = node["Name"]?.ToString() ?? string.Empty,
                    Category = category,
                    Summary = node["Summary"]?.ToString() ?? node["Description"]?.ToString() ?? string.Empty,
                    RawJson = json,
                };
            }
            catch (IOException) { return null; }
            catch (JsonException) { return null; }
        }
    }
}
```

- [ ] **Step 1.4：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter DesignContextServiceTests --nologo -v minimal
git add src/Tianming.ProjectData/Context/IDesignContextService.cs \
        src/Tianming.ProjectData/Context/DesignContextService.cs \
        tests/Tianming.ProjectData.Tests/Context/DesignContextServiceTests.cs
git commit -m "feat(context): M6.5.1 IDesignContextService + 文件实现"
```

---

## Task 2：IGenerationContextService（章节生成查 fact）

**Files:**
- Create: `src/Tianming.ProjectData/Context/IGenerationContextService.cs`
- Create: `src/Tianming.ProjectData/Context/GenerationContextService.cs`
- Test: `tests/Tianming.ProjectData.Tests/Context/GenerationContextServiceTests.cs`

- [ ] **Step 2.1：接口**

```csharp
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Tracking;

namespace TM.Services.Modules.ProjectData.Context
{
    public interface IGenerationContextService
    {
        /// <summary>组装生成章节所需的完整上下文（FactSnapshot + 设计元素名 + 前 N 章摘要）。</summary>
        Task<GenerationContext> BuildAsync(string chapterId, CancellationToken ct = default);
    }

    public sealed class GenerationContext
    {
        public string ChapterId { get; set; } = string.Empty;
        public FactSnapshot FactSnapshot { get; set; } = new();
        public DesignElementNames DesignElements { get; set; } = new();
        public string PreviousChaptersSummary { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 2.2：测试**

```csharp
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Context;
using TM.Services.Modules.ProjectData.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Context;

public class GenerationContextServiceTests
{
    [Fact]
    public async Task Build_returns_context_with_chapter_id_set()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-gc-{System.Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var svc = new GenerationContextService(root,
            (chapterId, ct) => Task.FromResult(new FactSnapshot()),
            (ct) => Task.FromResult(new DesignElementNames()),
            (chapterId, ct) => Task.FromResult("Previous chapters summary text"));
        var ctx = await svc.BuildAsync("ch-005");
        Assert.Equal("ch-005", ctx.ChapterId);
        Assert.NotNull(ctx.FactSnapshot);
        Assert.NotNull(ctx.DesignElements);
        Assert.Equal("Previous chapters summary text", ctx.PreviousChaptersSummary);
    }
}
```

- [ ] **Step 2.3：实现**

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Tracking;

namespace TM.Services.Modules.ProjectData.Context
{
    public sealed class GenerationContextService : IGenerationContextService
    {
        public delegate Task<FactSnapshot> FactSnapshotProvider(string chapterId, CancellationToken ct);
        public delegate Task<DesignElementNames> DesignNamesProvider(CancellationToken ct);
        public delegate Task<string> PreviousChaptersSummaryProvider(string chapterId, CancellationToken ct);

        private readonly string _projectRoot;
        private readonly FactSnapshotProvider _factProvider;
        private readonly DesignNamesProvider _namesProvider;
        private readonly PreviousChaptersSummaryProvider _summaryProvider;

        public GenerationContextService(
            string projectRoot,
            FactSnapshotProvider factProvider,
            DesignNamesProvider namesProvider,
            PreviousChaptersSummaryProvider summaryProvider)
        {
            _projectRoot = projectRoot;
            _factProvider = factProvider;
            _namesProvider = namesProvider;
            _summaryProvider = summaryProvider;
        }

        public async Task<GenerationContext> BuildAsync(string chapterId, CancellationToken ct = default)
        {
            var fact = await _factProvider(chapterId, ct).ConfigureAwait(false);
            var names = await _namesProvider(ct).ConfigureAwait(false);
            var summary = await _summaryProvider(chapterId, ct).ConfigureAwait(false);
            return new GenerationContext
            {
                ChapterId = chapterId,
                FactSnapshot = fact,
                DesignElements = names,
                PreviousChaptersSummary = summary,
            };
        }
    }
}
```

- [ ] **Step 2.4：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter GenerationContextServiceTests --nologo -v minimal
git add src/Tianming.ProjectData/Context/IGenerationContextService.cs \
        src/Tianming.ProjectData/Context/GenerationContextService.cs \
        tests/Tianming.ProjectData.Tests/Context/GenerationContextServiceTests.cs
git commit -m "feat(context): M6.5.2 IGenerationContextService + 委托式实现"
```

---

## Task 3：IValidationContextService（校验查规则）

**Files:**
- Create: `src/Tianming.ProjectData/Context/IValidationContextService.cs`
- Create: `src/Tianming.ProjectData/Context/ValidationContextService.cs`
- Test: `tests/Tianming.ProjectData.Tests/Context/ValidationContextServiceTests.cs`

- [ ] **Step 3.1：接口**

```csharp
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Tracking;

namespace TM.Services.Modules.ProjectData.Context
{
    public interface IValidationContextService
    {
        Task<ValidationContextBundle> BuildAsync(string chapterId, CancellationToken ct = default);
    }

    public sealed class ValidationContextBundle
    {
        public string ChapterId { get; set; } = string.Empty;
        public LedgerRuleSet RuleSet { get; set; } = new();
        public FactSnapshot FactSnapshot { get; set; } = new();
    }
}
```

- [ ] **Step 3.2：测试 + 实现**

```csharp
// Tests
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Context;
using TM.Services.Modules.ProjectData.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Context;

public class ValidationContextServiceTests
{
    [Fact]
    public async Task Build_returns_bundle_with_rule_and_snapshot()
    {
        var svc = new ValidationContextService(
            (ct) => Task.FromResult(new LedgerRuleSet()),
            (chapterId, ct) => Task.FromResult(new FactSnapshot()));
        var bundle = await svc.BuildAsync("ch-001");
        Assert.Equal("ch-001", bundle.ChapterId);
        Assert.NotNull(bundle.RuleSet);
        Assert.NotNull(bundle.FactSnapshot);
    }
}
```

```csharp
// 实现 ValidationContextService.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Tracking;

namespace TM.Services.Modules.ProjectData.Context
{
    public sealed class ValidationContextService : IValidationContextService
    {
        public delegate Task<LedgerRuleSet> RuleSetProvider(CancellationToken ct);
        public delegate Task<FactSnapshot> FactSnapshotProvider(string chapterId, CancellationToken ct);

        private readonly RuleSetProvider _ruleSetProvider;
        private readonly FactSnapshotProvider _snapshotProvider;

        public ValidationContextService(RuleSetProvider ruleSetProvider, FactSnapshotProvider snapshotProvider)
        {
            _ruleSetProvider = ruleSetProvider;
            _snapshotProvider = snapshotProvider;
        }

        public async Task<ValidationContextBundle> BuildAsync(string chapterId, CancellationToken ct = default)
        {
            var rules = await _ruleSetProvider(ct).ConfigureAwait(false);
            var snap = await _snapshotProvider(chapterId, ct).ConfigureAwait(false);
            return new ValidationContextBundle { ChapterId = chapterId, RuleSet = rules, FactSnapshot = snap };
        }
    }
}
```

- [ ] **Step 3.3：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter ValidationContextServiceTests --nologo -v minimal
git add src/Tianming.ProjectData/Context/IValidationContextService.cs \
        src/Tianming.ProjectData/Context/ValidationContextService.cs \
        tests/Tianming.ProjectData.Tests/Context/ValidationContextServiceTests.cs
git commit -m "feat(context): M6.5.3 IValidationContextService 委托式"
```

---

## Task 4：IPackagingContextService（打包查全局）

**Files:**
- Create: `src/Tianming.ProjectData/Context/IPackagingContextService.cs`
- Create: `src/Tianming.ProjectData/Context/PackagingContextService.cs`
- Test: `tests/Tianming.ProjectData.Tests/Context/PackagingContextServiceTests.cs`

- [ ] **Step 4.1：接口**

```csharp
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Context
{
    public interface IPackagingContextService
    {
        Task<PackagingSnapshot> BuildSnapshotAsync(CancellationToken ct = default);
    }

    public sealed class PackagingSnapshot
    {
        public IReadOnlyList<DesignReference> AllDesignReferences { get; set; } = new List<DesignReference>();
        public IReadOnlyList<string> ChapterIds { get; set; } = new List<string>();
        public System.DateTime CapturedAt { get; set; } = System.DateTime.UtcNow;
        public string ProjectRoot { get; set; } = string.Empty;
    }
}
```

- [ ] **Step 4.2：测试 + 实现**

```csharp
// Tests
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Context;
using Xunit;

namespace Tianming.ProjectData.Tests.Context;

public class PackagingContextServiceTests
{
    [Fact]
    public async Task BuildSnapshot_lists_all_chapters_and_design_refs()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-pc-{System.Guid.NewGuid():N}");
        var chaptersDir = Path.Combine(root, "Generate", "Chapters");
        Directory.CreateDirectory(chaptersDir);
        await File.WriteAllTextAsync(Path.Combine(chaptersDir, "ch-001.md"), "Chapter 1 content");
        await File.WriteAllTextAsync(Path.Combine(chaptersDir, "ch-002.md"), "Chapter 2 content");

        var design = new DesignContextService(root);
        var svc = new PackagingContextService(root, design);
        var snap = await svc.BuildSnapshotAsync();

        Assert.Equal(2, snap.ChapterIds.Count);
        Assert.Contains("ch-001", snap.ChapterIds);
        Assert.Contains("ch-002", snap.ChapterIds);
        Assert.Equal(root, snap.ProjectRoot);
    }
}
```

```csharp
// 实现 PackagingContextService.cs
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Context
{
    public sealed class PackagingContextService : IPackagingContextService
    {
        private readonly string _projectRoot;
        private readonly IDesignContextService _design;

        public PackagingContextService(string projectRoot, IDesignContextService design)
        {
            _projectRoot = projectRoot;
            _design = design;
        }

        public async Task<PackagingSnapshot> BuildSnapshotAsync(CancellationToken ct = default)
        {
            var allDesigns = new List<DesignReference>();
            var categories = new[] { "Characters", "WorldRules", "Factions", "Locations", "Plot", "CreativeMaterials" };
            foreach (var c in categories)
                allDesigns.AddRange(await _design.ListByCategoryAsync(c, ct).ConfigureAwait(false));

            var chaptersDir = Path.Combine(_projectRoot, "Generate", "Chapters");
            var chapterIds = Directory.Exists(chaptersDir)
                ? Directory.GetFiles(chaptersDir, "*.md", SearchOption.AllDirectories)
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList()!
                : new List<string>();

            return new PackagingSnapshot
            {
                AllDesignReferences = allDesigns,
                ChapterIds = chapterIds!,
                ProjectRoot = _projectRoot,
            };
        }
    }
}
```

- [ ] **Step 4.3：commit**

```bash
dotnet test tests/Tianming.ProjectData.Tests/Tianming.ProjectData.Tests.csproj --filter PackagingContextServiceTests --nologo -v minimal
git add src/Tianming.ProjectData/Context/IPackagingContextService.cs \
        src/Tianming.ProjectData/Context/PackagingContextService.cs \
        tests/Tianming.ProjectData.Tests/Context/PackagingContextServiceTests.cs
git commit -m "feat(context): M6.5.4 IPackagingContextService 全局快照"
```

---

## Task 5：DI 注册 + 旧 IConversationTool 切到新 service

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`
- Modify: `src/Tianming.AI/SemanticKernel/Conversation/Tools/LookupDataTool.cs`（可选：换实现走 DesignContextService）

- [ ] **Step 5.1：DI 注册 4 service**

```csharp
// M6.5 ContextService 拆分
s.AddSingleton<IDesignContextService>(sp =>
    new DesignContextService(sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));

s.AddSingleton<IGenerationContextService>(sp =>
    new GenerationContextService(
        sp.GetRequiredService<ICurrentProjectService>().ProjectRoot,
        factProvider: async (chapterId, ct) =>
        {
            // 接现有 PortableFactSnapshotExtractor；M4 阶段先返回空 snapshot
            await System.Threading.Tasks.Task.CompletedTask;
            return new TM.Services.Modules.ProjectData.Tracking.FactSnapshot();
        },
        namesProvider: async (ct) =>
        {
            await System.Threading.Tasks.Task.CompletedTask;
            return new TM.Services.Modules.ProjectData.Tracking.DesignElementNames();
        },
        summaryProvider: async (chapterId, ct) =>
        {
            await System.Threading.Tasks.Task.CompletedTask;
            return string.Empty;
        }));

s.AddSingleton<IValidationContextService>(sp =>
    new ValidationContextService(
        ruleSetProvider: async (ct) =>
        {
            await System.Threading.Tasks.Task.CompletedTask;
            return new TM.Services.Modules.ProjectData.Tracking.LedgerRuleSet();
        },
        snapshotProvider: async (chapterId, ct) =>
        {
            await System.Threading.Tasks.Task.CompletedTask;
            return new TM.Services.Modules.ProjectData.Tracking.FactSnapshot();
        }));

s.AddSingleton<IPackagingContextService>(sp =>
    new PackagingContextService(
        sp.GetRequiredService<ICurrentProjectService>().ProjectRoot,
        sp.GetRequiredService<IDesignContextService>()));
```

加 using：
```csharp
using TM.Services.Modules.ProjectData.Context;
```

- [ ] **Step 5.2：写 DI 烟雾测试**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia;
using TM.Services.Modules.ProjectData.Context;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.DI;

public class ContextServiceRegistrationTests
{
    [Fact]
    public void Build_resolves_all_four_context_services()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        Assert.NotNull(sp.GetRequiredService<IDesignContextService>());
        Assert.NotNull(sp.GetRequiredService<IGenerationContextService>());
        Assert.NotNull(sp.GetRequiredService<IValidationContextService>());
        Assert.NotNull(sp.GetRequiredService<IPackagingContextService>());
    }
}
```

- [ ] **Step 5.3：build + test**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v minimal
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
```

Expected: 全过。

- [ ] **Step 5.4：commit**

```bash
git add src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs \
        tests/Tianming.Desktop.Avalonia.Tests/DI/ContextServiceRegistrationTests.cs
git commit -m "feat(context): M6.5.5 DI 注册 4 ContextService + 烟雾测试"
```

---

## M6.5 Gate 验收

| 项 | 标准 |
|---|---|
| 4 接口 | IDesignContextService / IGenerationContextService / IValidationContextService / IPackagingContextService 全部定义 |
| 4 实现 | 各 ≥2 测试 |
| DI | AppHost.Build() 能 resolve 全部 4 service |
| 全量 test | 新增 ≥10 条，dotnet test 全过 |
| 不破坏旧逻辑 | 现有 ChapterValidationPromptContextResolver / LookupDataTool 等不被改动（M6.5+ 后续 lane 再迁移调用方）|
