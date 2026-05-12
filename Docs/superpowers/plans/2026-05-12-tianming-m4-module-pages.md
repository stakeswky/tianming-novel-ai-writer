# 天命 macOS 迁移 — M4 核心模块页面迁移 Implementation Plan（里程碑级蓝图）

> **For agentic workers:** 此 plan 为里程碑级蓝图（task granularity），覆盖 M4.1 至 M4.6 六个子里程碑的目录布局 / 接口设计 / 依赖关系 / 验收标准。**每个子里程碑（M4.1、M4.2 …）开工前，用 superpowers:writing-plans skill 再细化为 step-level plan**，因为每个子里程碑涉及大量 Avalonia 具体 API 调用与 VM 组合，step-level 代码需在实际 Avalonia 11.x 环境验证后才能写得准确。
>
> REQUIRED SUB-SKILL: Use superpowers:writing-plans 在每个子里程碑开工前细化；然后 superpowers:subagent-driven-development 或 superpowers:executing-plans 执行细化后的 plan。

**Goal:** 在 M3 Avalonia Shell 骨架之上，把天命的创作闭环——从"设计规则"到"生成规划"再到"章节编辑器"到"章节生成管道"到"AI 对话面板"与"AI 模型 / 提示词管理"——整套 UI 迁到 macOS。终态：你能在 macOS 上新建项目 → 填五大规则 → 写大纲分卷章节 → 对话生成 → 编辑器修改 → 保存，整个流程顺滑可用。

**Architecture:** 按创作闭环时序串行，6 个子里程碑（M4.1-M4.6），每个都独立闭环可用后再进下一个。所有 VM 只承担"绑定 / 命令调度 / 状态投影"，业务逻辑走 portable service。shared control（`CategoryDataPageView`、`MarkdownEditor`、`ChatStreamView` 等）先写再复用。Rules / Generation / Validation 等现仍在旧 `Modules/` 目录的 service 需要**前置端口**到 `src/Tianming.ProjectData/Modules/`（portable lib），这是 M4.0 的主要工作。

**Tech Stack:** Avalonia 11.x / CommunityToolkit.Mvvm / AvaloniaEdit（Markdown 编辑器）/ Markdig（预览渲染）/ xUnit。

**Spec:** `Docs/superpowers/specs/2026-05-12-tianming-m4-module-pages-design.md`（位于分支 `m2-m6/specs-2026-05-12`）。

## Scope Alignment（仓库真实状态与架构决策）

核对过的关键事实 + 架构决策：

### 决策 A：M4.0 不搬 WPF Rules services，改走 schema-driven adapter 层

- **真实事实**：
  - `src/Tianming.ProjectData/Modules/FileModuleDataStore.cs` **已存在**，是 `sealed class FileModuleDataStore<TCategory, TData> where TCategory : class, ICategory where TData : class, IDataItem`，提供 `LoadAsync / GetCategories / GetData / AddCategoryAsync / AddDataAsync / DeleteDataAsync / CascadeDeleteCategoryAsync`——这就是 portable 数据底座
  - 旧 WPF 的 `WorldRulesService / CharacterRulesService / ...`（6 个 Rules + 4 个 Generation）都继承自 `Framework/Common/Services/ModuleServiceBase<TCategory, TData>`；`ModuleServiceBase` 带 `IWorkScopeService` / `ICategorySaver` / `IDataStorageStrategy<T>` 一长串 WPF 相关的 ambient 依赖
  - 旧 WPF VM 层共享 `Framework/Common/ViewModels/DataManagementViewModelBase<TData, TCategory, TService>`，每个 `*RulesViewModel` 只提供：(1) form 字段声明；(2) `PopulateForm/CollectForm` 映射；(3) AI 批量生成上下文。CRUD/tree 逻辑都在基类
  - 已有 `Services/Modules/ProjectData/Models/Validate/ValidationSummary/ValidationRules.cs:ExtendedDataSchemas` 是 `Dictionary<string, string[]>`（module → 字段名数组），是**部分** schema 概念但仅限 validation
- **决策**：M4.0 不端口 `ModuleServiceBase` 与 6 个 Rules + 4 个 Generation service（避免把 WPF ambient 依赖链带回）。改为在 portable lib 建立一层 **schema-driven 模块数据 adapter**：
  - `IModuleSchema<TCategory, TData>` 抽象（字段描述、默认值工厂、AI 提示词上下文钩子）
  - `ModuleDataAdapter<TCategory, TData>`：基于已有 `FileModuleDataStore<,>` 的薄壳，暴露 VM 需要的 Add/Update/Delete/List/SaveCategory 等 API
  - 为 6 Rules + 4 Generation 各写一个 `*Schema` + `*Category` + `*Data` POCO（字段按旧 VM 的 `Form*` property 反推），不写 service
  - Avalonia VM 基类 `DataManagementViewModel<TCategory, TData, TSchema>`：对应旧 `DataManagementViewModelBase` 的跨平台版，只依赖 `ModuleDataAdapter` + `IModuleSchema`，不碰 `Service` / `Kernel` / SK
- **收益**：跨平台层不引回 WPF ambient 依赖；10 个页面的 VM 只差"加载一个 schema"一行代码；AI 批量生成用抽象方法在基类里统一实现一次

### 决策 B：M4.5 AI 对话面板不搬 WPF SK-based 服务，新建 `ConversationOrchestrator`

（与 M2 plan Scope Alignment 里的 SK 决策对齐）

- **真实事实**：
  - 旧 WPF `Services/Framework/AI/SemanticKernel/` 下 17 个 .cs 深度使用 `Microsoft.SemanticKernel 1.73.0` 的 `Kernel` / `ChatHistory` / `IChatCompletionService` / `ChatCompletionAgent` / `ChatHistoryAgentThread` / `KernelArguments` / `KernelFunction` / `IFunctionInvocationFilter`：核心类是 `SKChatService` / `NovelAgent` / `ChatHistoryCompressionService` / `ThinkingStreamWrapper` / `NovelMemoryProvider` / `RAGContextProvider` / `ChatModeSettings` / `PlanModeFilter` / 3 个 Plugin
  - 跨平台 `src/Tianming.AI/` **完全 SK-free**（M2 已钉死）
  - M1 已端口：`ConversationModeProfileCatalog` / `TagBasedThinkingStrategy` / `AskModeMapper` / `PlanModeMapper` / `AgentModeMapper` / `ThinkingBlockParser` / `ExecutionTraceCollector` / `FileSessionStore` / `ChatHistoryCompressionService`（portable 版）/ `OpenAICompatibleChatClient`
- **决策**：M4.5 不端口 `SKChatService` / `NovelAgent` / `ThinkingStreamWrapper` / 3 个 Plugin / `PlanModeFilter`。新建 `src/Tianming.AI/SemanticKernel/Conversation/ConversationOrchestrator.cs`（portable，命名沿用"SemanticKernel"目录，不引 SK 包），对外暴露：
  ```
  Task<ConversationSession> StartSessionAsync(ConversationMode mode, ...)
  IAsyncEnumerable<ChatStreamDelta> SendAsync(ConversationSession s, string userInput, IReadOnlyList<ReferenceTag> refs, CancellationToken ct)
  ```
  内部用 M1 已端口零件 + `OpenAICompatibleChatClient.StreamAsync` 拼装：
  - `ConversationModeProfileCatalog.Get(mode)` 取 system prompt 模板
  - `TagBasedThinkingStrategy` 拆 `<thinking>` 块
  - `*ModeMapper` 按模式构造 message payload
  - Plan/Agent 的"计划解析/工具调用解析"走 M1 已端口的 `PlanStepParser` / `ExecutionTraceCollector` 而非 SK filter
- **Agent 模式工具调用的落地方式**：OpenAI-compatible `tools` 参数（不是 SK Plugin），工具定义走自研 `IConversationTool` 接口，M4.5 暂定 3 个内置工具（`lookup_data` / `read_chapter` / `search_references`），与旧 WPF DataLookupPlugin / SystemPlugin 的职能对齐但不继承 SK 类型

### 决策 C：其他事实与技术选型

- **`ChapterGenerationPipeline`** 真实类名未在仓里找到（`Modules/Generate/` 与 `src/` 下都 NOT FOUND）。M4.2.2 开工前用 Explore subagent 再扫一遍；如确认不存在，M4.2.2 在 portable 层新建 `ChapterGenerationPipeline` 类（"加载 Fact Snapshot → 组 prompt → 调 `OpenAICompatibleChatClient` → 解析 CHANGES → 应用"五步，每步各 1-2 测试）
- **`FileAIConfigurationStore` / `FilePromptTemplateStore` / `FileUsageStatisticsService` / `IApiKeySecretStore`** 已在 `src/Tianming.AI/` 下端口，M4.6 直接用
- **AvaloniaEdit** 加 NuGet `Avalonia.AvaloniaEdit`（M4.3 实测时选有效 11.x 兼容版本）
- **Markdig**：用 `Markdig` 解析生成 AST，自写 ~100 行 Avalonia renderer（不引 `Markdig.Avalonia` 第三方包）
- **智能拆书**：M2 spec 推后，M4 亦不做；`LocalFileFetcher` / `PortableBookAnalysisService` 到真需要再端口

## 子里程碑与依赖关系

```
M3 完成（Avalonia Shell 骨架）
   ↓
M4.0 ── Schema-driven 模块数据 adapter 层（不端口 WPF service）
   ↓
M4.1 设计模块（6 页，共用 CategoryDataPageView + schema 驱动）
   ↓
M4.2 生成规划（4 页 + 独立 ChapterPipelinePage）
   ↓
M4.3 章节编辑器（MarkdownEditor + ChapterTabBar + 持久化）
   ↓
M4.4 章节生成闭环（把 M4.2 pipeline 与 M4.3 编辑器串通）
   ↓
M4.5 AI 对话面板（右栏实装：新建 ConversationOrchestrator + Ask/Plan/Agent 三模式 + 会话历史）
   ↓
M4.6 AI 管理（模型 / Key / 提示词 / 用量 四页）
   ↓
M4 Done
```

## File Structure（高阶）

**portable adapter 层（M4.0）：**
- `src/Tianming.ProjectData/Modules/Schema/IModuleSchema.cs` — `IModuleSchema<TCategory, TData>` 抽象
- `src/Tianming.ProjectData/Modules/Schema/FieldDescriptor.cs` — 字段元数据
- `src/Tianming.ProjectData/Modules/Schema/ModuleDataAdapter.cs` — 基于 `FileModuleDataStore<,>` 的薄壳
- `src/Tianming.ProjectData/Modules/Design/WorldRules/WorldRulesSchema.cs` + `WorldRulesCategory.cs` + `WorldRulesData.cs`
- 同样：`CharacterRules / FactionRules / LocationRules / PlotRules / CreativeMaterials`（共 6 组 Design schema）
- 同样：`Generate/Outline / VolumeDesign / ChapterPlanning / ContentConfig`（共 4 组 Generate schema）
- `src/Tianming.ProjectData/Generation/ChapterGenerationPipeline.cs` — 新建或端口

**portable AI 层（M4.5 新建）：**
- `src/Tianming.AI/SemanticKernel/Conversation/ConversationOrchestrator.cs` — Ask/Plan/Agent 编排（不依赖 SK）
- `src/Tianming.AI/SemanticKernel/Conversation/IConversationTool.cs` — Agent 模式自研工具抽象
- `src/Tianming.AI/SemanticKernel/Conversation/Tools/LookupDataTool.cs`
- `src/Tianming.AI/SemanticKernel/Conversation/Tools/ReadChapterTool.cs`
- `src/Tianming.AI/SemanticKernel/Conversation/Tools/SearchReferencesTool.cs`

**共享控件（M4.1 Wave 0 时建立）：**
- `src/Tianming.Desktop.Avalonia/Controls/CategoryDataPageView.axaml`（左分类树 + 右数据表单）
- `src/Tianming.Desktop.Avalonia/Controls/DataFormView.axaml`（按 DataSchema 生成字段）
- `src/Tianming.Desktop.Avalonia/Controls/MarkdownEditor.axaml`（M4.3）
- `src/Tianming.Desktop.Avalonia/Controls/MarkdownPreview.axaml`（M4.3）
- `src/Tianming.Desktop.Avalonia/Controls/ChapterTabBar.axaml`（M4.3）
- `src/Tianming.Desktop.Avalonia/Controls/ChatStreamView.axaml`（M4.5）
- `src/Tianming.Desktop.Avalonia/Controls/ReferenceDropdown.axaml`（M4.5）
- `src/Tianming.Desktop.Avalonia/Controls/ToolCallCard.axaml`（M4.5）
- `src/Tianming.Desktop.Avalonia/Controls/PlanStepListView.axaml`（M4.5）
- `src/Tianming.Desktop.Avalonia/Controls/FactSnapshotView.axaml`（M4.4）
- `src/Tianming.Desktop.Avalonia/Controls/ChangesPreview.axaml`（M4.4）

**页面（各子里程碑添加）：** 详见各子里程碑下文。

**修改：**
- `src/Tianming.ProjectData/ServiceCollectionExtensions.cs`（M3 已建立骨架）— 每端口一组 service 就加注册
- `src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs`（M3 已建立）— 每新增页面加一个 PageKey
- `src/Tianming.Desktop.Avalonia/Navigation/PageRegistry.cs` 或等效的注册入口 — 每新增页面加一条映射
- `src/Tianming.Desktop.Avalonia/ViewModels/Shell/LeftNavViewModel.cs`（M3 已建立）— 加导航项

---

## Task M4.0：Schema-driven 模块数据 adapter 层

**工作量估计：1.5-2 天**（比"端口 6 WPF service"省约 1 天）

基于已有 `FileModuleDataStore<TCategory, TData>` 建一层 schema 抽象，供 10 个数据页面（6 Design + 4 Generate）和 1 个通用 VM 基类消费。不端口 WPF service / `ModuleServiceBase` / `DataManagementViewModelBase`。

### Task M4.0.1：IModuleSchema + FieldDescriptor + ModuleDataAdapter

**Files:**
- Create: `src/Tianming.ProjectData/Modules/Schema/IModuleSchema.cs`
- Create: `src/Tianming.ProjectData/Modules/Schema/FieldDescriptor.cs`
- Create: `src/Tianming.ProjectData/Modules/Schema/ModuleDataAdapter.cs`
- Create: `tests/Tianming.ProjectData.Tests/Modules/Schema/ModuleDataAdapterTests.cs`
- Modify: `src/Tianming.ProjectData/Tianming.ProjectData.csproj`（加 Compile Include）

**核心接口骨架：**

```csharp
// FieldDescriptor.cs
namespace TM.Services.Modules.ProjectData.Modules.Schema;

public sealed record FieldDescriptor(
    string PropertyName,       // e.g. "FormName" / "FormPowerSystem"
    string Label,              // 显示文案
    FieldType Type,
    bool Required,
    string? Placeholder,
    int? MaxLength = null,
    IReadOnlyList<string>? EnumOptions = null);

public enum FieldType
{
    SingleLineText,
    MultiLineText,
    Number,
    Tags,        // string[] 逗号分隔
    Enum,
    Boolean
}

// IModuleSchema.cs
public interface IModuleSchema<TCategory, TData>
    where TCategory : class, ICategory
    where TData : class, IDataItem
{
    string PageTitle { get; }
    string PageIcon { get; }
    string ModuleRelativePath { get; }   // 相对项目根，如 "Design/GlobalSettings/WorldRules"
    IReadOnlyList<FieldDescriptor> Fields { get; }
    TData CreateNewItem();                // 带默认值的空白 data
    TCategory CreateNewCategory(string name);
    // AI 批量生成上下文钩子（M4.1 末尾/M4.5 再接；M4.0 留接口）
    string BuildAIPromptContext(IReadOnlyList<TData> existing);
}

// ModuleDataAdapter.cs
public sealed class ModuleDataAdapter<TCategory, TData>
    where TCategory : class, ICategory
    where TData : class, IDataItem
{
    private readonly FileModuleDataStore<TCategory, TData> _store;
    public IModuleSchema<TCategory, TData> Schema { get; }

    public ModuleDataAdapter(IModuleSchema<TCategory, TData> schema, string projectRoot)
    {
        Schema = schema;
        var moduleDir = System.IO.Path.Combine(projectRoot, schema.ModuleRelativePath);
        _store = new FileModuleDataStore<TCategory, TData>(
            moduleDirectory: moduleDir,
            categoriesFileName: "categories.json",
            builtInCategoriesFileName: "categories.builtin.json",
            dataFileName: "data.json");
    }

    public Task LoadAsync() => _store.LoadAsync();
    public IReadOnlyList<TCategory> GetCategories() => _store.GetCategories();
    public IReadOnlyList<TData> GetData() => _store.GetData();
    public Task<bool> AddCategoryAsync(TCategory c) => _store.AddCategoryAsync(c);
    public Task AddAsync(TData data) => _store.AddDataAsync(data);
    public Task<bool> DeleteAsync(string id) => _store.DeleteDataAsync(id);
    public Task<CascadeDeleteResult> CascadeDeleteCategoryAsync(string name)
        => _store.CascadeDeleteCategoryAsync(name);

    // Update = Delete + Add（FileModuleDataStore 没有原子 Update，按需扩或在 VM 层实现）
    public async Task UpdateAsync(TData data)
    {
        await _store.DeleteDataAsync(data.Id);
        await _store.AddDataAsync(data);
    }

    // 按 category name 过滤数据（IDataItem.Category 字段存 category name；IDataItem.CategoryId 存 ID）
    public IEnumerable<TData> GetDataForCategory(TCategory category)
        => _store.GetData().Where(d => d.Category == category.Name);
}
```

**测试（ModuleDataAdapterTests.cs）至少 6 条：**
1. `AddAsync` → `GetData` 返回该项
2. `AddCategoryAsync` → `GetCategories` 返回
3. `DeleteAsync` → `GetData` 不再含该项
4. `UpdateAsync` → 字段已更新
5. `CascadeDeleteCategoryAsync` 返回正确 counts
6. 构造器用 fake schema 能正确组 moduleDirectory

**验收：** `dotnet test` 全过；新增 ≥ 6 测试

**Commit：** `feat(projectdata): 模块数据 adapter 层（schema-driven）`

### Task M4.0.2：六组 Design schema（POCO 走 csproj link，不重写）

**关键事实**（探索确认）：
- 旧 `Services/Modules/ProjectData/Models/Design/{World,Character,Faction,Location,Plot,CreativeMaterials}*/*Data.cs` 与 `*Category.cs` 已经是 **WPF-free** 的纯 POCO（继承 `BusinessDataBase : IDataItem, ISourceBookBound`；category 实现 `ICategory`；全 JsonPropertyName 标注；无 `System.Windows` / `System.Drawing` 引用）
- `src/Tianming.ProjectData/Tianming.ProjectData.csproj` 已对 `BusinessDataBase.cs` 做 `<Compile Include Link>` 软链接进 portable lib；WorldRulesData / WorldRulesCategory 等 **直接 link 即可**，不必重写
- 需要新建的只有：**`*Schema` 类**（描述字段元数据供 VM 消费）

**Files（每组 1 个新 schema + 2 个 csproj link 条目 + 1 测试）：**
- Create: `src/Tianming.ProjectData/Modules/Design/WorldRules/WorldRulesSchema.cs`
- Modify: csproj 加 2 行 link：
  ```xml
  <Compile Include="../../Services/Modules/ProjectData/Models/Design/Worldview/WorldRulesData.cs"
           Link="Modules/Design/WorldRules/WorldRulesData.cs" />
  <Compile Include="../../Services/Modules/ProjectData/Models/Design/Worldview/WorldRulesCategory.cs"
           Link="Modules/Design/WorldRules/WorldRulesCategory.cs" />
  ```
- Create: `tests/Tianming.ProjectData.Tests/Modules/Design/WorldRulesSchemaTests.cs`
- 同样：`CharacterRules / FactionRules / LocationRules / PlotRules / CreativeMaterials` 各自的 schema + 2 link 条目 + 1 测试
- Modify: `src/Tianming.ProjectData/ServiceCollectionExtensions.cs` 注册 6 个 schema：`s.AddSingleton<IModuleSchema<WorldRulesCategory, WorldRulesData>, WorldRulesSchema>()` 等

**字段来源：** 逐 VM 读旧 `Modules/Design/{...}/*ViewModel.cs` 的 `Form*` property 列表反推。参考样例：
- `WorldRules` 字段：Name / Icon / OneLineSummary / PowerSystem / Cosmology / SpecialLaws / HardRules / SoftRules / AncientEra / KeyEvents / ModernHistory / StatusQuo（12+）
- `CharacterRules` 字段：Name / Icon / CharacterType / Gender / Age / Identity / Race / Appearance / Want / Need / FlawBelief / GrowthPath / CombatSkills / ...（20+，按旧 VM 提取）

**Schema 模板（以 WorldRules 为例）：**

```csharp
// src/Tianming.ProjectData/Modules/Design/WorldRules/WorldRulesSchema.cs
using TM.Framework.Common.Helpers.Id;
using TM.Services.Modules.ProjectData.Models.Design.Worldview; // 旧 namespace，已通过 csproj link 导入
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace TM.Services.Modules.ProjectData.Modules.Design.WorldRules;

public sealed class WorldRulesSchema : IModuleSchema<WorldRulesCategory, WorldRulesData>
{
    public string PageTitle => "世界观规则";
    public string PageIcon => "🌍";
    public string ModuleRelativePath => "Design/GlobalSettings/WorldRules";

    public IReadOnlyList<FieldDescriptor> Fields { get; } = new[]
    {
        new FieldDescriptor("Name",           "名称",       FieldType.SingleLineText, true,  "如：九州大陆"),
        new FieldDescriptor("OneLineSummary", "一句话概述", FieldType.SingleLineText, false, null),
        new FieldDescriptor("PowerSystem",    "力量体系",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Cosmology",      "宇宙观",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("SpecialLaws",    "特殊法则",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("HardRules",      "硬性规则",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("SoftRules",      "软性规则",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("AncientEra",     "远古时期",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("KeyEvents",      "关键事件",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("ModernHistory",  "近代史",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("StatusQuo",      "当下格局",   FieldType.MultiLineText,  false, null),
    };

    public WorldRulesData CreateNewItem() => new()
    {
        Id = ShortIdGenerator.New("D"),
        Name = string.Empty,
    };

    public WorldRulesCategory CreateNewCategory(string name) => new()
    {
        Id = ShortIdGenerator.New("C"),
        Name = name,
        Icon = "📁",
        Level = 1,
        IsEnabled = true,
    };

    public string BuildAIPromptContext(IReadOnlyList<WorldRulesData> existing)
        => string.Join("\n---\n", existing.Select(x => $"{x.Name}: {x.OneLineSummary}"));
}
```

**测试：** 每 schema 至少 1 条（`Fields.Count == N`、`PageTitle` 正确、`CreateNewItem().Id.StartsWith("D")`、`CreateNewCategory("foo").Name == "foo"`）

**验收：** 6 schema 全部通过测试；`dotnet test` 全过

**Commit（每 schema 一次，共 6 次）：**
- `feat(projectdata): WorldRules schema + csproj link`
- `feat(projectdata): CharacterRules schema + csproj link`
- `feat(projectdata): FactionRules schema + csproj link`
- `feat(projectdata): LocationRules schema + csproj link`
- `feat(projectdata): PlotRules schema + csproj link`
- `feat(projectdata): CreativeMaterials schema + csproj link`

### Task M4.0.3：四组 Generation schema + ChapterGenerationPipeline

**Files（4 Generation schema）：**
- Create: `src/Tianming.ProjectData/Modules/Generate/Outline/OutlineSchema.cs`
- Create: 同 `VolumeDesign / ChapterPlanning / ContentConfig` schema
- Create: `tests/Tianming.ProjectData.Tests/Modules/Generate/*SchemaTests.cs`

**开工前置（M4.0.3 Step 0）：** 先 Explore 旧 `Services/Modules/ProjectData/Models/Generate/**` 是否已有 `OutlineData / OutlineCategory / VolumeDesignData / ... ` 等 POCO 且 WPF-free。预期：
- (a) 若**已有 WPF-free 旧 POCO** → 同 M4.0.2 模式，csproj 加 link，**不重写** POCO，只写 Schema
- (b) 若**旧 POCO 依赖 WPF** → 按需拆依赖，端口到 portable 或新建
- (c) 若**旧 POCO 不存在**（某些生成模块只是 VM 层数据）→ 在 portable 新建 POCO + Schema

**Files（ChapterGenerationPipeline）：**

**开工前置（M4.0.3 Step 1）：** Explore subagent 30 分钟确认现实类名。三种结果分支：
- (A) 已存在于 `src/Tianming.AI/` 或 `src/Tianming.ProjectData/` 下 → 跳过新建，直接 M4.2.2 消费
- (B) 在旧 `Modules/Generate/` 或 `Services/` WPF 目录下 → 按"抽抽象 + 重写"路径：读旧类 `RunAsync` 等方法签名，在 portable 层新建 `ChapterGenerationPipeline`，只复用 prompt / CHANGES 协议逻辑，SK 相关部分改走 `OpenAICompatibleChatClient.StreamAsync`
- (C) 不存在 → 在 portable 层从零建，五步：
  1. `LoadFactSnapshotAsync(chapterId)` — 从 Chapter + Volume + Rules 拉当前快照
  2. `BuildPrompt(snapshot, contentConfig)` — 组 prompt
  3. `StreamAsync(prompt, ct)` — 调 `OpenAICompatibleChatClient.StreamAsync` 流式返回
  4. `ParseChanges(rawText)` — 解析 CHANGES 协议为 `ChapterChangesResult`
  5. `ApplyAsync(chapterId, changes)` — 落盘到 `ChapterContentStore`（M1 已端口）

**Files:**
- Create: `src/Tianming.ProjectData/Generation/ChapterGenerationPipeline.cs`
- Create: `src/Tianming.ProjectData/Generation/ChapterGenerationContracts.cs`（`FactSnapshot` / `ChapterChangesResult` / 等 record）
- Create: `tests/Tianming.ProjectData.Tests/Generation/ChapterGenerationPipelineTests.cs`

**测试（Pipeline，用 fake chat client）：** 至少 5 条——每步各 1 测试（能 load snapshot / build prompt 正确 / stream delta 能消费 / ParseChanges 能识别 CHANGES 块 / ApplyAsync 能落盘）

**验收：** 4 schema + Pipeline 新增测试 ≥ 12 条；`dotnet test` 全过

**Commit（5 次）：**
- `feat(projectdata): Outline schema + link/POCO`
- `feat(projectdata): VolumeDesign schema + link/POCO`
- `feat(projectdata): ChapterPlanning schema + link/POCO`
- `feat(projectdata): ContentConfig schema + link/POCO`
- `feat(projectdata): ChapterGenerationPipeline + 测试`

### M4.0 Gate：adapter 层基线

```bash
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -8
```

Expected: 新增 ≥ 25 条测试（1143 → 1168+），全过。

**硬子闭环（M4.0 验收）：** 这是底层搭积木，无 UI；验收靠"下面的 M4.1 Gate 能跑通第一页就说明 adapter 设计 ok"。M4.0 不单独做 user-facing 验收。

---

## Task M4.1：设计模块 6 页

**工作量估计：2 天**

### Task M4.1.1：共享控件 CategoryDataPageView + DataFormView + 基类 VM

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/CategoryDataPageView.axaml`（.cs）
- Create: `src/Tianming.Desktop.Avalonia/Controls/DataFormView.axaml`（.cs）
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Shared/ModuleDataPageViewModel.cs` — 泛型基类 VM
- Create: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/ModuleDataPageViewModelTests.cs`
- Modify: csproj

**核心职责：**
- `CategoryDataPageView`：左 `TreeView` 绑 `Categories`，中 `ListBox` 绑 `CurrentCategoryItems`，右 `DataFormView`（按 `Schema.Fields` 动态生成字段）
- `ModuleDataPageViewModel<TCategory, TData>`：泛型基类，构造器注入 `ModuleDataAdapter<TCategory, TData>`；实现 CRUD 命令 + form 绑定 + 项目切换响应
- 字段绑定走反射：`FieldDescriptor.PropertyName` → `typeof(TData).GetProperty(name)`，读写 `CurrentItem`（避免每页重写 `PopulateForm/CollectForm`）

**基类 VM 骨架：**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels.Shared;

public abstract partial class ModuleDataPageViewModel<TCategory, TData> : ObservableObject
    where TCategory : class, TM.Framework.Common.Models.ICategory
    where TData     : class, TM.Framework.Common.Models.IDataItem
{
    protected readonly ModuleDataAdapter<TCategory, TData> Adapter;

    [ObservableProperty] private ObservableCollection<TCategory> _categories = new();
    [ObservableProperty] private TCategory? _currentCategory;
    [ObservableProperty] private ObservableCollection<TData> _currentCategoryItems = new();
    [ObservableProperty] private TData? _currentItem;

    public IModuleSchema<TCategory, TData> Schema => Adapter.Schema;

    protected ModuleDataPageViewModel(ModuleDataAdapter<TCategory, TData> adapter)
    {
        Adapter = adapter;
    }

    public async Task LoadAsync()
    {
        await Adapter.LoadAsync();
        Categories = new(Adapter.GetCategories());
        if (Categories.Count > 0) CurrentCategory = Categories[0];
    }

    partial void OnCurrentCategoryChanged(TCategory? value)
    {
        // IDataItem.Category 字段存 category name（注意：是 Category 不是 CategoryName，见 Framework/Common/Models/IDataItem.cs）
        CurrentCategoryItems = new(Adapter.GetData()
            .Where(d => d.Category == value?.Name));
    }

    [RelayCommand]
    private async Task AddItemAsync()
    {
        var item = Schema.CreateNewItem();
        if (CurrentCategory is not null) item.Category = CurrentCategory.Name;
        await Adapter.AddAsync(item);
        CurrentCategoryItems.Add(item);
        CurrentItem = item;
    }

    [RelayCommand]
    private async Task DeleteItemAsync()
    {
        if (CurrentItem is null) return;
        await Adapter.DeleteAsync(CurrentItem.Id);
        CurrentCategoryItems.Remove(CurrentItem);
        CurrentItem = null;
    }

    [RelayCommand]
    private async Task SaveCurrentAsync()
    {
        if (CurrentItem is not null) await Adapter.UpdateAsync(CurrentItem);
    }

    [RelayCommand]
    private async Task AddCategoryAsync(string name)
    {
        var c = Schema.CreateNewCategory(name);
        await Adapter.AddCategoryAsync(c);
        Categories.Add(c);
    }
}
```

**测试（至少 5 条）：**
- `LoadAsync` 后 Categories 与 CurrentCategoryItems 正确填充
- `AddItemCommand` 新增项落盘且出现在列表
- `DeleteItemCommand` 移除项
- `SaveCurrentCommand` 更新字段
- `OnCurrentCategoryChanged` 切换分类时项目列表刷新

**验收：** VM 单测全过；CategoryDataPageView 能手工挂一个 dummy schema 渲染可视化正确

**Commit：** `feat(ui): M4.1.1 共享 ModuleDataPageViewModel + CategoryDataPageView`

### Task M4.1.2：六个 Rules 页面

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Views/Design/WorldRulesPage.axaml`（.cs）+ `ViewModels/Design/WorldRulesViewModel.cs`
- Create: `src/Tianming.Desktop.Avalonia/Views/Design/CharacterRulesPage.axaml` + VM
- Create: 同样的 Faction / Location / Plot / CreativeMaterials
- Modify: `Navigation/PageKeys.cs` + `Navigation/PageRegistry.cs` 注册 6 个 PageKey
- Modify: `ViewModels/Shell/LeftNavViewModel.cs` 加 6 项导航
- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs` 注册 6 个 adapter（每个走 `sp => new ModuleDataAdapter<...>(sp.GetRequiredService<WorldRulesSchema>(), projectRoot)`）

**核心职责：** 每页 XAML 只一行（`<ctl:CategoryDataPageView DataContext="{Binding}" />`），VM 是 `ModuleDataPageViewModel<,>` 的无代码派生类：

**VM 模板（WorldRulesViewModel.cs，全 10 行）：**

```csharp
namespace Tianming.Desktop.Avalonia.ViewModels.Design;

public sealed partial class WorldRulesViewModel
    : ModuleDataPageViewModel<WorldRulesCategory, WorldRulesData>
{
    public WorldRulesViewModel(ModuleDataAdapter<WorldRulesCategory, WorldRulesData> adapter)
        : base(adapter) { }
}
```

其他 5 页等价。字段差异由 `*Schema` 已在 M4.0.2 定义；VM 零业务代码。

**验收：** 导航栏 6 项；点每页能看到自己的 schema 字段；CRUD 正常落盘到 `<project>/Design/.../data.json`

**Commit（每页一次，共 6 次）：** `feat(ui): M4.1.2 {World/Character/Faction/Location/Plot/CreativeMaterials}RulesPage`

### M4.1 Gate（硬子闭环）

**用户视角闭环任务**：用天命 UI 从零录入一个最小可用的故事设计底盘。

具体步骤（手工走一遍）：
1. 新建项目 "M4.1-smoke"
2. 在 **世界观规则** 页：添加分类 "主线"，新建 "九州大陆"，填 PowerSystem / Cosmology / HardRules 三个多行字段（各至少 50 字），保存
3. 在 **角色规则** 页：添加分类 "主角"，新建 "张三"，填 Name / Identity / Want / Need / FlawBelief / GrowthPath 六字段，保存
4. 在 **势力规则** 页：添加 "天命殿"，填 Name / OneLineSummary + 至少 2 个多行字段，保存
5. 在 **地点规则** 页：添加 "主角故乡"，填至少 3 字段
6. 在 **剧情规则** 页：添加 "开篇冲突"，填至少 3 字段
7. 在 **创意素材** 页：添加 "备用名字列表"，填 3 条
8. **关闭应用 → `find <project> -name "data.json"`** 应看到 6 个 `data.json` 文件，每个 JSON 里字段齐全且 UTF-8 正确
9. **重开应用** → 导航回每页 → 数据全部恢复可编辑

**不过不进 M4.2：** 如果任何一页的"保存—重开—恢复"循环掉字段或掉数据，属于 adapter / schema / 反射绑定的问题，在 M4.1 里修到位再推进。

---

## Task M4.2：生成规划（4 页 + 章节生成管道页）

**工作量估计：1.5-2 天**

### Task M4.2.1：Outline / VolumeDesign / ChapterPlanning / ContentConfig 四页

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Views/Generate/OutlinePage.axaml` + `OutlineViewModel.cs`
- Create: 同样 VolumeDesignPage / ChapterPlanningPage / ContentConfigPage
- Modify: PageKeys / PageRegistry / LeftNavViewModel
- Modify: `AvaloniaShellServiceCollectionExtensions.cs` 注册 4 个 adapter（`ModuleDataAdapter<OutlineCategory, OutlineData>` 等）

**核心职责：** 与 M4.1.2 同模板——VM 是 `ModuleDataPageViewModel<,>` 派生类 10 行；字段差异由 M4.0.3 的 `*Schema` 已定义；CRUD 全部走 adapter。

**验收：** 4 页 schema 字段可视化正确；能 CRUD；落盘路径正确（`<project>/Generate/.../data.json`）

**Commit：** 4 个

### Task M4.2.2：ChapterPipelinePage（独立布局）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Views/Generate/ChapterPipelinePage.axaml`（.cs）
- Create: `ViewModels/Generate/ChapterPipelineViewModel.cs`
- Create: `Controls/FactSnapshotView.axaml` + VM
- Create: `Controls/ChangesPreview.axaml` + VM
- Modify: PageKeys / PageRegistry / LeftNavViewModel

**核心职责：** 三列布局：左侧选章（`ListBox` 显示 ChapterPlanning 下所有 chapter），中间两部分上下分（上 `FactSnapshotView` 下 `ChangesPreview`），右侧执行日志 `TextBlock` + 生成 button + 取消 button。
`ChapterPipelineViewModel` 构造器注入 `ChapterGenerationPipeline`（portable）。按"生成"按钮调 pipeline 的 entry method，订阅进度事件更新 UI。CHANGES 协议返回后在 `ChangesPreview` 渲染，用户点"应用"才落盘。

**验收：** 选一章后能看 Fact Snapshot（即使是空数据也能可视化）；点生成 → 接 M2 的 OpenAI-compatible 真实 API → 流式日志显示 → 完成后渲染 CHANGES → 点应用 → 章节内容写入 `ChapterContentStore`

**Commit：** 2-3 个 commit

### M4.2 Gate（硬子闭环）

**用户视角闭环任务**：从设计到章节文本的第一次真实生成。

前置：M4.1 smoke 项目里已有世界观/角色/势力数据。

具体步骤（手工走一遍）：
1. 在 **大纲** 页：新建 "开篇三章"，填入主干情节概要（200 字+）
2. 在 **分卷设计** 页：新建第 1 卷 "相遇"，填分卷目标
3. 在 **章节规划** 页：在第 1 卷下规划 3 章，各写章节大纲（100 字+）
4. 在 **内容配置** 页：新建一个默认配置（章节字数 2500、风格 "悬疑"），保存
5. 在 **章节生成管道** 页：选择第 1 章 → 查看 Fact Snapshot 汇总正确（含世界观 / 角色 / 大纲）→ 点生成 → 实际向 OpenAI-compatible 端点发请求 → 流式日志能看到 token 流入 → 完成后弹出 CHANGES 预览 → 点应用
6. 检查 `<project>/Generate/Chapters/ch-001.md` 存在且内容长度合理（1000 字+ / 不是空）

**不过不进 M4.3：** Fact Snapshot 空 / 生成不流式 / CHANGES 解析失败 / 应用后文件不落盘——任一失败即 blocker。这是整个创作闭环的核心路径。

---

## Task M4.3：章节编辑器

**工作量估计：2 天**

### Task M4.3.1：MarkdownEditor 控件（AvaloniaEdit 包装）

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj` 加 NuGet `Avalonia.AvaloniaEdit`（或 `AvaloniaEdit` stable）
- Create: `src/Tianming.Desktop.Avalonia/Controls/MarkdownEditor.axaml` + `.cs`
- Create: `ViewModels/Controls/MarkdownEditorViewModel.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/MarkdownEditorViewModelTests.cs`

**核心职责：** AvaloniaEdit `TextEditor` 包装；加基础 Markdown 语法高亮（通过 `AvaloniaEdit.Highlighting.HighlightingManager.Instance.RegisterHighlighting`）；暴露 `Text` / `IsModified` / `WordCount` / `CaretOffset` 双向绑定属性；自动保存草稿（每 2 秒 debounce 写 `<project>/.drafts/<chapterId>.md`）；快捷键 ⌘+S / ⌘+B（加粗）/ ⌘+I（斜体）

**AvaloniaEdit API 风险：** 具体 API 可能与我预期不符（`TextEditor` 的事件名、绑定方式、DI 注入路径）。开工前用 20 分钟 Avalonia.AvaloniaEdit 官方 sample 验证。

**验收：** 能输入、语法高亮生效、WordCount 实时更新、⌘+S 触发 `SavedRequested` 事件

**Commit：** 1-2 个

### Task M4.3.2：MarkdownPreview 控件（Markdig 驱动）

**Files:**
- Modify: csproj 加 NuGet `Markdig`
- Create: `Controls/MarkdownPreview.axaml` + `.cs`
- Create: `Controls/MarkdigToAvaloniaRenderer.cs`（~100 行）
- Create: 测试（给一段 Markdown 断言 render 出的 `TextBlock` 结构）

**核心职责：** `Markdig.Parsers.MarkdownParser.Parse(text)` → `MarkdownDocument` → 自写 visitor 遍历转成 Avalonia `TextBlock`/`StackPanel` 堆叠。支持 heading（H1-H3）/ paragraph / bold / italic / bullet list / code block（`FormattedText` monospace）。其他语法（表格、链接等）先不支持。

**验收：** 给一段有 H1/H2/bold/list 的 Markdown 渲染可视化正确

**Commit：** 1-2 个

### Task M4.3.3：ChapterTabBar + EditorWorkspaceView + 持久化

**Files:**
- Create: `Controls/ChapterTabBar.axaml` + VM
- Create: `Views/Editor/EditorWorkspaceView.axaml` + `EditorWorkspaceViewModel`
- Create: `Infrastructure/EditorTabsStore.cs`（读写 `editor_tabs.json`）
- Create: 测试
- Modify: PageKeys / PageRegistry / LeftNavViewModel 加 `editor.workspace`

**核心职责：** 多 tab（每 tab 一个章节）；关闭 tab 前若有未保存改动弹确认；tab 状态持久化到 `<project>/.editor/editor_tabs.json`；切项目清空；启动时恢复

**验收：** 开 3 tab → 编辑 → 关闭应用 → 重开 → tab 与光标位置恢复

**Commit：** 2-3 个

### M4.3 Gate（硬子闭环）

**用户视角闭环任务**：编辑器能当作你实际写小说的工具用。

前置：M4.2 已生成一章真实 AI 文本到 `<project>/Generate/Chapters/ch-001.md`。

具体步骤：
1. 在章节编辑器打开 ch-001.md
2. **语法高亮** — `#`、`##`、`**bold**`、`_italic_`、列表、代码块——肉眼可辨
3. **WordCount** — 底部显示"1234 字"，在文本末尾输入"测试"立刻变成"1236 字"（Chinese char 计数 1 一个不是 2）
4. **⌘S 保存** — 改几段话按 ⌘S → 关闭编辑器 → 重开 → 改动保留
5. **草稿自动保存** — 改文本后 2 秒内 `<project>/.drafts/ch-001.md` 出现；没按 ⌘S 强退应用 → 重开能从草稿恢复
6. **多 tab** — 同时打开 3 章编辑 → 关闭应用 → 重开 → 3 tab 全恢复 + 各自光标位置恢复
7. **预览** — 切"分屏预览" → H1/H2/bold/列表在右侧预览里正确渲染

**不过不进 M4.4：** ⌘S 不触发 / 草稿丢失 / 重开 tab 丢失——任一失败修到位。这是日常写作的地基。

---

## Task M4.4：章节生成闭环

**工作量估计：0.5 天**

### Task M4.4.1：Pipeline 生成成功 → 自动打开编辑器 tab

**Files:**
- Modify: `ViewModels/Generate/ChapterPipelineViewModel.cs` - 在"应用 CHANGES"成功后发 `NavigationRequest` 到 `editor.workspace` 并传 chapter id 参数
- Modify: `ViewModels/Editor/EditorWorkspaceViewModel.cs` - 响应参数加载对应 chapter 到新 tab
- Create: `Messaging/ChapterAppliedEvent.cs`（WeakReferenceMessenger 驱动）

**验收：** 生成章节 → 应用 → 自动跳转到编辑器页 → 新章节 tab 打开并加载内容

**Commit：** 1

### M4.4 Gate（硬子闭环）

**用户视角闭环任务**：生成 → 编辑的 seamless 跳转。

具体步骤：
1. 在章节生成管道页选第 2 章 → 生成 → 应用
2. **自动跳转** — 页面自动切到编辑器，第 2 章 tab 激活，光标在文首
3. 立刻修改：把第一段改写得更像自己的风格
4. ⌘S 保存
5. **跨页面一致性** — 切回章节规划页，第 2 章状态应显示 "已生成"（而非"规划中"）
6. **再次生成不覆盖已改写** — 重新生成第 2 章 → Pipeline 应提示"该章已生成，是否覆盖？"而非盲写
7. 选"覆盖"后 tab 里内容变新生成版本；选"取消"后原改写保留

**不过不进 M4.5：** 自动跳转不发生 / 状态不同步 / 覆盖保护缺失——修到位。这个环节是写作闭环的"节拍"，不 seamless 整条流水线就废了。

---

## Task M4.5：AI 对话面板（右栏实装）

**工作量估计：3 天**

**前置决策（见 Scope Alignment 决策 B）：** 不端口 WPF SK-based 服务（`SKChatService` / `NovelAgent` / `ThinkingStreamWrapper` / 3 个 Plugin / `PlanModeFilter`）。M4.5.3 新建 portable `ConversationOrchestrator`，基于 M1 已端口的 `ConversationModeProfileCatalog` / `TagBasedThinkingStrategy` / `AskModeMapper` / `PlanModeMapper` / `AgentModeMapper` / `PlanStepParser` / `ExecutionTraceCollector` / `FileSessionStore` + M2 的 `OpenAICompatibleChatClient.StreamAsync` 拼装。不引入 `Microsoft.SemanticKernel` NuGet 包。

### Task M4.5.1：ChatStreamView + 消息 VM 模型

**Files:**
- Create: `Controls/ChatStreamView.axaml` + VM
- Create: `Controls/AssistantMessageCard.axaml`、`UserMessageCard.axaml`（thinking/answer/toolCall 分块）
- Create: `ViewModels/Conversation/ChatMessageViewModel.cs`（含子类 `UserChatMessageVM`、`AssistantChatMessageVM`）
- Create: `ViewModels/Conversation/BulkEmitter.cs`（每 16ms flush ObservableCollection）
- Create: 测试

**核心职责：** 滚动列表绑定 `ObservableCollection<ChatMessageViewModel>`；自动滚底；thinking 块可折叠；每 16ms flush 流式增量；

**验收：** 给 fake `IAsyncEnumerable<ChatStreamDelta>` 触发 → 消息实时出现且无 UI 卡顿

**Commit：** 2

### Task M4.5.2：ReferenceDropdown + 输入框 + 模式切换

**Files:**
- Create: `Controls/ReferenceDropdown.axaml` + VM
- Create: `Controls/ConversationInputBox.axaml` + VM
- Create: `Controls/ModePill.axaml`（Ask / Plan / Agent 三选一）

**核心职责：** 输入框 `@` 触发下拉，显示当前项目的可引用项（章节 / 规则 / 角色等），选中后插入 `@reference-name`；模式 pill 切换后同时切换对话 system prompt；

**依赖：** `ReferenceCatalog`（M1 已端口）

**Commit：** 2

### Task M4.5.3：对话编排服务接入（新建 portable ConversationOrchestrator）

**Files:**
- Create: `src/Tianming.AI/SemanticKernel/Conversation/ConversationOrchestrator.cs` — portable，不依赖 SK
- Create: `src/Tianming.AI/SemanticKernel/Conversation/ChatStreamDelta.cs` — `abstract record` + 子类 `ThinkingDelta` / `AnswerDelta` / `ToolCallDelta` / `ToolResultDelta` / `PlanStepDelta`
- Create: `src/Tianming.AI/SemanticKernel/Conversation/ConversationSession.cs` — 会话上下文（mode、history、current references、system prompt）
- Create: `src/Tianming.AI/SemanticKernel/Conversation/IConversationTool.cs` + `Tools/LookupDataTool.cs` + `Tools/ReadChapterTool.cs` + `Tools/SearchReferencesTool.cs`
- Create: `tests/Tianming.AI.Tests/Conversation/ConversationOrchestratorTests.cs`（用 `FakeChatClient` stub 覆盖三模式 happy path）
- Create: `ViewModels/Conversation/ConversationPanelViewModel.cs`

**核心接口骨架：**

```csharp
namespace TM.Services.Framework.AI.SemanticKernel.Conversation;

public sealed class ConversationOrchestrator
{
    private readonly OpenAICompatibleChatClient _chat;
    private readonly ConversationModeProfileCatalog _profiles;
    private readonly TagBasedThinkingStrategy _thinking;
    private readonly IFileSessionStore _sessions;
    private readonly IReadOnlyList<IConversationTool> _tools;
    private readonly AskModeMapper _askMapper;
    private readonly PlanModeMapper _planMapper;
    private readonly AgentModeMapper _agentMapper;

    public ConversationOrchestrator(
        OpenAICompatibleChatClient chat,
        ConversationModeProfileCatalog profiles,
        TagBasedThinkingStrategy thinking,
        IFileSessionStore sessions,
        IEnumerable<IConversationTool> tools,
        AskModeMapper askMapper,
        PlanModeMapper planMapper,
        AgentModeMapper agentMapper) { /* ... */ }

    public Task<ConversationSession> StartSessionAsync(
        ConversationMode mode,
        string? sessionId = null,
        CancellationToken ct = default);

    public IAsyncEnumerable<ChatStreamDelta> SendAsync(
        ConversationSession session,
        string userInput,
        IReadOnlyList<ReferenceTag> references,
        CancellationToken ct = default);

    public Task PersistAsync(ConversationSession session, CancellationToken ct = default);
}

public interface IConversationTool
{
    string Name { get; }
    string Description { get; }   // 用于 OpenAI tools 声明
    object GetParameterSchema();  // JSON Schema for tools API
    Task<string> InvokeAsync(IReadOnlyDictionary<string, object?> args, CancellationToken ct);
}
```

**核心职责：**
- `StartSessionAsync`：按 mode 从 `ConversationModeProfileCatalog` 取 system prompt 模板 + 初始 history；可选 `sessionId` 从 `IFileSessionStore` 恢复历史
- `SendAsync`：
  - Ask/Plan：`*ModeMapper.Map(session, userInput, refs)` 组 payload → `_chat.StreamAsync()` → `TagBasedThinkingStrategy.Parse` 拆 thinking/answer → yield `ThinkingDelta` / `AnswerDelta` / `PlanStepDelta`（Plan 模式下把 answer 送 `PlanStepParser` 得到 step 列表）
  - Agent：`AgentModeMapper` 组 payload（含 tools 声明 from `_tools`） → 循环：流式 chat → 检测 `tool_calls` → 对每个 tool_call 找对应 `IConversationTool.InvokeAsync` → 把结果塞回 history 继续流 → yield `ToolCallDelta` / `ToolResultDelta` / `AnswerDelta`
- `PersistAsync`：把 session（含 full history）写 `IFileSessionStore`

**风险：**
- **SK 特有能力的对等替换**：旧 WPF 的 `PlanModeFilter`（SK `IFunctionInvocationFilter`）无直接对等物 → 用 `AgentModeMapper` 在消息构造层做同样的 gating（Plan 模式下禁用工具调用），测试断言"Plan 模式 payload 里 tools 字段为空"
- **Function calling 格式**：不同 OpenAI-compatible endpoint 对 tools 格式细节有差异（DeepSeek / Qwen 部分兼容）。先只在 OpenAI / DeepSeek 上测 happy path，其他待遇到再适配
- **流式解析边界**：`<thinking>` 块跨 chunk 边界时，`TagBasedThinkingStrategy` 已在 M1 测试覆盖，但 tool_call 增量累积是新场景，测试要覆盖"delta 里 arguments 字段分 3 次到达"

**测试（`ConversationOrchestratorTests.cs` ≥ 8 条）：**
1. Ask 模式 happy path：fake chat 返回 `<thinking>X</thinking>Hello` → 收到 1 个 ThinkingDelta + 1 个 AnswerDelta
2. Plan 模式：fake chat 返回带 PLAN 协议的文本 → 收到 N 个 PlanStepDelta
3. Agent 模式工具调用：fake chat 第一轮返回 tool_call → orchestrator 调 fake tool → 第二轮返回 answer → 收到 ToolCallDelta + ToolResultDelta + AnswerDelta
4. Plan 模式禁用工具：payload 检查 `tools` 字段为 null/empty
5. 取消 token：`ct.Cancel()` 后 `SendAsync` 抛 `OperationCanceledException`
6. Session 恢复：`StartSessionAsync(mode, existingSessionId)` 能加载 history 首条消息
7. `PersistAsync` 后从 store 读出 session 全字段一致
8. 多个 reference tags：payload 里包含所有 tag 文本

**Commit：** 3-4 个（orchestrator + tools + VM + tests 可分次）

### Task M4.5.4：PlanStepListView + Agent ToolCallCard

**Files:**
- Create: `Controls/PlanStepListView.axaml` + VM
- Create: `Controls/ToolCallCard.axaml` + VM
- Modify: `ConversationPanelViewModel` 根据 mode 路由到相应可视化

**核心职责：** Plan 模式输出 plan → 解析为步骤列表可视化，每步显示 pending / running / done；Agent 模式 tool call → ToolCallCard 展开显示（工具名、参数、结果）

**依赖：** `PlanStepParser`、`PlanStepNormalizer`、`ExecutionTraceCollector`（M1 已端口）

**Commit：** 2

### Task M4.5.5：会话历史抽屉 + FileSessionStore 接线

**Files:**
- Create: `Views/Conversation/ConversationHistoryDrawer.axaml` + VM
- Modify: `ConversationPanelViewModel` 加 "新建会话" / "切换会话" / "删除会话" 命令

**核心职责：** 右栏顶部加"历史"按钮，点开抽屉列出 `FileSessionStore.ListAllAsync()` 返回的会话，按时间倒序；点击加载会话到当前 `ChatStreamView`；新建清空当前 stream

**依赖：** `FileSessionStore`（M1 已端口）

**Commit：** 1-2

### M4.5 Gate（硬子闭环，三模式各跑一次真任务）

**用户视角闭环任务**：对话面板真的能帮你写小说，不是"能跑流式就算"。

前置：M4.2 项目里已有真实的世界观 / 角色 / 大纲数据；API Key 已配（可先明文 JSON 存，M5 换 Keychain）。

#### Ask 模式（10 分钟闭环）
1. 输入 "@世界观 九州大陆 的核心冲突是什么？"（`@` 触发 ReferenceDropdown 选中"九州大陆"）
2. 发送 → 流式回复里 **thinking 块** 可折叠 → answer 块引用了 schema 里真实填的 PowerSystem / HardRules 字段内容（不是胡编）
3. 关应用 → 重开 → 打开 **会话历史** → 能点击这条会话加载，消息历史完整恢复

#### Plan 模式（15 分钟闭环）
1. 切 Plan 模式 → 输入 "帮我规划一下 第 5 章 应该怎么写"
2. 流式输出后，**PlanStepListView** 显示至少 3 个步骤（如 "回顾前情 → 设置主冲突 → 写转折"）
3. 每步有 pending/running/done 状态，**点击步骤能展开查看该步的详细说明**
4. 此步骤列表应保存到 session（关闭重开能恢复）

#### Agent 模式（20 分钟闭环）
1. 切 Agent 模式 → 输入 "帮我查张三在第 3 章有没有矛盾"
2. 看到 **ToolCallCard** 展示 `lookup_data` 或 `read_chapter` 被调用，显示参数、返回的真实数据片段
3. 最终 answer 基于工具返回的真实内容给出分析（引用具体章节编号与角色字段）

**不过不进 M4.6：**
- Ask 模式的引用没实际生效（回答未使用 `@引用` 的真实数据）
- Plan 模式步骤列表只是纯文本，无状态可视化
- Agent 模式工具调用卡片不显示 / 工具返回值为空
- 会话历史恢复掉字段

——任一失败即 blocker，M4.5 修到 "你自己用得下去" 才进 M4.6。**M4.5 是 M4 的主战场，不要赶进度。**

---

## Task M4.6：AI 模型 / 提示词 / 用量

**工作量估计：1.5 天**

### Task M4.6.1：ModelManagementPage

**Files:**
- Create: `Views/AI/ModelManagementPage.axaml` + VM
- Modify: PageKeys / PageRegistry / LeftNavViewModel

**核心职责：** 列出、增删改 AI provider 配置（baseUrl / model / temperature / 默认）；VM 注入 `FileAIConfigurationStore`（M1 已端口）

**Commit：** 1

### Task M4.6.2：ApiKeysPage

**Files:**
- Create: `Views/AI/ApiKeysPage.axaml` + VM

**核心职责：** 每个 provider 下可填 1+ API Key；密钥显示用 password box（可切换明文）；VM 注入 `IApiKeySecretStore`（M1 已端口，macOS 走 shell `/usr/bin/security`；M5 可以考虑升级 P/Invoke 但 shell 版本已经能用）

**关键点：** `IApiKeySecretStore` 是 sync API（`GetSecret / SaveSecret / DeleteSecret`），在 VM 里要在 `Task.Run` 里跑避免阻塞 UI

**Commit：** 1

### Task M4.6.3：PromptManagementPage

**Files:**
- Create: `Views/AI/PromptManagementPage.axaml` + VM

**核心职责：** 复用 `CategoryDataPageView`；CategorySpec 包含提示词字段（name / description / template / variables / category）；VM 注入 `FilePromptTemplateStore`（M1 已端口）

**Commit：** 1

### Task M4.6.4：UsageStatisticsPage

**Files:**
- Create: `Views/AI/UsageStatisticsPage.axaml` + VM

**核心职责：** 按日期 / 按 provider / 按 model 聚合调用次数与 token 用量；简单表格视图（不做图表）；VM 注入 `FileUsageStatisticsService`（M1 已端口）

**Commit：** 1

### M4.6 Gate（硬子闭环，管理页真正被 M4.5 消费）

**用户视角闭环任务**：切换模型 / 换提示词 / 看用量，都要在 M4.5 对话里真生效。

具体步骤：
1. **ModelManagement** — 新增 "DeepSeek" provider（baseUrl / model 填好），标记为默认 → 切到 AI 对话面板 → 发消息 → 网络请求应真正发到 DeepSeek 端点（用 Charles / mitmproxy 验证）
2. **ApiKeys** — 在 "DeepSeek" 下填入 Key → 关闭重开 → Key 仍在（明文 JSON 或 Keychain 皆可，M5 替换）
3. **PromptManagement** — 新建提示词 "历史顾问"，模板里用 `{{topic}}` 占位 → 切回对话页面应能在 system prompt 里选用 → 发消息后 AI 回复风格明显偏向历史顾问角色
4. **UsageStatistics** — 经过前述 3 步，打开用量页应看到：至少 1 条今日的调用记录，带 prompt tokens / completion tokens / 费用估算（按 DeepSeek pricing 粗估）
5. 切换到另一 provider（如 OpenAI）→ 同样流程应走 OpenAI endpoint

**不过不算 M4 Done：**
- 模型切换在对话页不生效（还走默认 / hardcoded 端点）
- Key 重启后丢失
- 提示词无法在对话中选用
- 用量永远显示 0

——任一失败修到位。

---

## 测试策略

- **Portable services（M4.0）：** xUnit 单测，每个 service ≥ 3 条（CRUD + round-trip）
- **VM 层：** 每个主要 VM ≥ 1-2 条单测（加载、保存、导航命令）
- **控件层：** 复杂控件（`CategoryDataPageView`、`MarkdownPreview`、`ChatStreamView`）各 1-2 条测试
- **不做：** Avalonia.Headless 端到端、UI 自动化脚本
- **手工验收清单：** 每个子里程碑有自己的 Gate 章节描述的手工冒烟场景

预期测试增量：
- M4.0：+25（adapter 层 ~6 + 6 Design schema ~6 + 4 Generate schema ~4 + ChapterGenerationPipeline ~5 + 其余 ~4）
- M4.1：+10（基类 VM 5 + 6 个派生 VM 冒烟 5）
- M4.2：+7（4 VM + pipeline VM）
- M4.3：+10（编辑器 / 预览 / tab VM）
- M4.5：+15（orchestrator ≥8 + VM + BulkEmitter + 3 工具 ≥3）
- M4.6：+6（4 页 VM）

**M4 总计新增测试：~73 条**（1177 → ~1250）

## 风险与缓解

| ID | 风险 | 影响 | 缓解 |
|---|---|---|---|
| R1 | Schema 字段反射绑定性能问题（每次 form 切换 `GetProperty()`）| M4.1 体验 | 在 `ModuleDataPageViewModel` ctor 里一次性把 `typeof(TData).GetProperties()` 缓存成 dict |
| R2 | AvaloniaEdit API 与预期不一致 | M4.3 卡壳 | 开工前 30 分钟跑一个 hello-world AvaloniaEdit sample 验证 API；若 AvaloniaEdit 不可用，降级到 `TextBox` multi-line + 自写简单行号 / 字数统计 |
| R3 | Markdig → Avalonia renderer 工作量超预估 | M4.3 +1 天 | 极简版只支持 heading / paragraph / bold / italic / list / code；其他降级为原文显示 |
| R4 | M4.5 orchestrator 的 Agent 模式 tool calling 在非 OpenAI endpoint 不兼容 | Agent 模式可能只在 OpenAI/DeepSeek 工作 | 先在 OpenAI 上测通；其他 endpoint（Claude、Qwen）用到时再适配。Agent 模式在 Gate 里只要求至少一个 provider 能走通 |
| R5 | 流式绑定高频 UI 更新卡顿 | 体验 | `BulkEmitter` 16ms 批量 flush；实在不行降级为每 200ms 整块替换 |
| R6 | `FieldDescriptor` 覆盖不了某些页面的特殊字段（如富文本、列表嵌套）| M4.1/M4.2 某页特例需单独写 | `IModuleSchema` 加可选 `CustomFormView: object?`（派生 schema 返回一个 Avalonia `UserControl` 类型）做 override；先不实现，真遇到再加 |
| R7 | 自研 orchestrator 与 WPF SK `PlanModeFilter` / `ChatCompletionAgent` 的功能缺口 | Plan / Agent 模式体验可能缩水 | 接受；优先让 Ask 闭环可用；Plan 只要"能拿到步骤列表可视化"即可，Agent 只要"至少一个工具被真实调用"即可，细节后续再补 |
| R8 | `ChapterGenerationPipeline` 新建后与旧 WPF CHANGES 协议细节不一致 | M4.2 Gate 失败 | M4.0.3 开工前用 Explore subagent 读旧生成代码（grep "CHANGES"）提取协议 spec；测试用 fixture 文件覆盖 |

## 验收标准（M4 整体）

1. `dotnet build Tianming.MacMigration.sln` → 0 Warning / 0 Error
2. `dotnet test Tianming.MacMigration.sln` → ~1250 全过
3. `Scripts/check-windows-bindings.sh src/ tests/` → 0 命中
4. **六个子里程碑 Gate 全部通过**（每个 Gate 都有自己的"硬子闭环"描述，见上文）；**一个 Gate 失败即 M4 未 Done**，不做"跳过 A 做 B"的绕行
5. 每子里程碑分别有独立 commit 组（commit message 可追溯到 task ID）
6. **没有残留的"旧 WPF service 引用"**（`WorldRulesService` / `CharacterRulesService` / `SKChatService` / `NovelAgent` / `KernelFunction` 等不应在 `src/Tianming.Desktop.Avalonia/` 或 `src/Tianming.ProjectData/` 或 `src/Tianming.AI/` 下出现）—— grep 审计为硬门槛

**完成后进入 M5。**

## 执行建议

> 此 plan 为里程碑级蓝图。每个子里程碑（M4.0 / M4.1 / ... / M4.6）**开工前**：
>
> 1. 用 Explore subagent 确认依赖类真实签名（防止盲写）
> 2. 用 superpowers:writing-plans skill 生成该子里程碑的 step-level plan（bite-sized，TDD，完整代码）
> 3. 用 superpowers:subagent-driven-development 或 executing-plans 执行
>
> **Gate 是硬门槛，不是软建议**：上一个 Gate 没通不进下一个；如果某个 Gate 卡 3 天以上无法突破，停下来重新规划——可能是架构假设有问题，比"硬推"值钱。
