# 天命 macOS 迁移 — M4 核心模块页面迁移 Implementation Plan（里程碑级蓝图）

> **For agentic workers:** 此 plan 为里程碑级蓝图（task granularity），覆盖 M4.1 至 M4.6 六个子里程碑的目录布局 / 接口设计 / 依赖关系 / 验收标准。**每个子里程碑（M4.1、M4.2 …）开工前，用 superpowers:writing-plans skill 再细化为 step-level plan**，因为每个子里程碑涉及大量 Avalonia 具体 API 调用与 VM 组合，step-level 代码需在实际 Avalonia 11.x 环境验证后才能写得准确。
>
> REQUIRED SUB-SKILL: Use superpowers:writing-plans 在每个子里程碑开工前细化；然后 superpowers:subagent-driven-development 或 superpowers:executing-plans 执行细化后的 plan。

**Goal:** 在 M3 Avalonia Shell 骨架之上，把天命的创作闭环——从"设计规则"到"生成规划"再到"章节编辑器"到"章节生成管道"到"AI 对话面板"与"AI 模型 / 提示词管理"——整套 UI 迁到 macOS。终态：你能在 macOS 上新建项目 → 填五大规则 → 写大纲分卷章节 → 对话生成 → 编辑器修改 → 保存，整个流程顺滑可用。

**Architecture:** 按创作闭环时序串行，6 个子里程碑（M4.1-M4.6），每个都独立闭环可用后再进下一个。所有 VM 只承担"绑定 / 命令调度 / 状态投影"，业务逻辑走 portable service。shared control（`CategoryDataPageView`、`MarkdownEditor`、`ChatStreamView` 等）先写再复用。Rules / Generation / Validation 等现仍在旧 `Modules/` 目录的 service 需要**前置端口**到 `src/Tianming.ProjectData/Modules/`（portable lib），这是 M4.0 的主要工作。

**Tech Stack:** Avalonia 11.x / CommunityToolkit.Mvvm / AvaloniaEdit（Markdown 编辑器）/ Markdig（预览渲染）/ xUnit。

**Spec:** `Docs/superpowers/specs/2026-05-12-tianming-m4-module-pages-design.md`（位于分支 `m2-m6/specs-2026-05-12`）。

## Scope Alignment（仓库真实状态）

在正式写 plan 前，核对过的关键事实：

1. **Rules services 还未端口到 portable 类库**。当前在 `Modules/Design/{GlobalSettings,Elements}/{World,Character,Faction,Location,Plot,CreativeMaterials}/Services/`，继承自 `Framework/Common/Services/ModuleServiceBase.cs`（也在原路径）。M4.0 必须先把这些端口到 `src/Tianming.ProjectData/Modules/Design/`。
2. **Generation services** 类似：在 `Modules/Generate/...` 老路径。M4.2 前需端口。
3. **Chapter pipeline（`ChapterGenerationPipeline` 或等效）** 需探索现实类名并考虑端口状态。
4. **对话面板驱动类** 真实名字未确认（`SKChatService` / `ChatStreamService` / `AIConversationService` 三种可能）。M4.5 前先用 Explore subagent 做 30 分钟探索并确认名字 + 端口状态。
5. **`ConversationModeProfileCatalog`、`TagBasedThinkingStrategy`、`FileSessionStore`** 等 M4.5 依赖已在 `src/Tianming.AI/SemanticKernel/Conversation/` 下端口（M1 已确认）。
6. **`FileAIConfigurationStore`、`FilePromptTemplateStore`、`FileUsageStatisticsService`、`IApiKeySecretStore`** 已在 `src/Tianming.AI/` 下端口（M1 已确认）。M4.6 直接用。
7. **AvaloniaEdit** 需加 NuGet `Avalonia.AvaloniaEdit`（或 `AvaloniaEdit`，实测时选有效版本）。
8. **Markdig + 简单 renderer**：M4.3 用 `Markdig` 解析 Markdown 生成 AST，自写 100 行左右的 Avalonia `TextBlock` 堆叠 renderer（不引 `Markdig.Avalonia` 第三方包，避免版本锁定）。

## 子里程碑与依赖关系

```
M3 完成（Avalonia Shell 骨架）
   ↓
M4.0 ── Portable 端口：Rules services（6 个）+ Generation services（4 个）+ ChapterPipeline
   ↓
M4.1 设计模块（6 页，共用 CategoryDataPageView）
   ↓
M4.2 生成规划（5 页；1 个独立 ChapterPipelinePage）
   ↓
M4.3 章节编辑器（MarkdownEditor + ChapterTabBar + 持久化）
   ↓
M4.4 章节生成闭环（把 M4.2 pipeline 与 M4.3 编辑器串通）
   ↓
M4.5 AI 对话面板（右栏实装：Ask/Plan/Agent 三模式 + 会话历史）
   ↓
M4.6 AI 管理（模型 / Key / 提示词 / 用量 四页）
   ↓
M4 Done
```

## File Structure（高阶）

**portable 端口（M4.0）：**
- `src/Tianming.ProjectData/Framework/Services/ModuleServiceBase.cs`（从旧 `Framework/Common/Services/` 端口，去掉 WPF 依赖）
- `src/Tianming.ProjectData/Modules/Design/WorldRules/` → WorldRulesService + WorldRulesCategory + WorldRulesData
- `src/Tianming.ProjectData/Modules/Design/CharacterRules/` → 同
- `src/Tianming.ProjectData/Modules/Design/FactionRules/`
- `src/Tianming.ProjectData/Modules/Design/LocationRules/`
- `src/Tianming.ProjectData/Modules/Design/PlotRules/`
- `src/Tianming.ProjectData/Modules/Design/CreativeMaterials/`
- `src/Tianming.ProjectData/Modules/Generate/Outline/`
- `src/Tianming.ProjectData/Modules/Generate/VolumeDesign/`
- `src/Tianming.ProjectData/Modules/Generate/ChapterPlanning/`
- `src/Tianming.ProjectData/Modules/Generate/ContentConfig/`
- `src/Tianming.ProjectData/Generation/ChapterGenerationPipeline.cs`（若未端口）

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

## Task M4.0：Portable 端口前置工作

**工作量估计：2-3 天**

把旧 `Modules/` 路径下的 Rules / Generation services 端口到 `src/Tianming.ProjectData/Modules/` 下；去掉 WPF 依赖；挂 NUnit/xUnit 单测迁移。

### Task M4.0.1：端口 ModuleServiceBase + 必需基础依赖

**Files:**
- Create: `src/Tianming.ProjectData/Framework/Services/ModuleServiceBase.cs`（从 `Framework/Common/Services/ModuleServiceBase.cs` 端口）
- Create: `src/Tianming.ProjectData/Framework/Services/IClearAllService.cs`
- Create: `src/Tianming.ProjectData/Framework/Services/ICascadeDeleteCategoryService.cs`
- Modify: `src/Tianming.ProjectData/Tianming.ProjectData.csproj`（加 Compile Include）

**核心职责：** 把 `ModuleServiceBase<TCategory, TData>` 端口到 portable lib；依赖的 `IWorkScopeService`、`ICategorySaver`、`IAsyncInitializable`、`IDataStorageStrategy<T>`、`ShortIdGenerator` 等按需一并端口（部分已在 Framework/Common 端口过）。

**关键步骤：**
1. 用 `grep` 找 `ModuleServiceBase` 的所有依赖类（using 子句）
2. 按依赖链从叶子到根依次端口到 `src/Tianming.ProjectData/Framework/`
3. 每端口一个 .cs 在 csproj 加 `<Compile Include>`
4. 逐步 build 确认编译通过
5. 原路径里 portable 版本会被旧 WPF 项目引用冲突——此时决定：**旧 WPF 项目暂不管，M4 结束后它应该被整体废弃或改引用 portable 版本**

**验收：** `dotnet build src/Tianming.ProjectData/Tianming.ProjectData.csproj` → 0 error；新端口的 ModuleServiceBase 有 1-2 个基础单测覆盖"CRUD + 存储 round-trip"

**Commit：** `feat(projectdata): 端口 ModuleServiceBase 与基础依赖`

### Task M4.0.2：端口六个 Rules services

**Files:**（每个 service 对应 3-4 个文件：service + category + data + 单测）
- Create: `src/Tianming.ProjectData/Modules/Design/WorldRules/` 整组
- Create: `src/Tianming.ProjectData/Modules/Design/CharacterRules/` 整组
- Create: `src/Tianming.ProjectData/Modules/Design/FactionRules/` 整组
- Create: `src/Tianming.ProjectData/Modules/Design/LocationRules/` 整组
- Create: `src/Tianming.ProjectData/Modules/Design/PlotRules/` 整组
- Create: `src/Tianming.ProjectData/Modules/Design/CreativeMaterials/` 整组
- Create: `tests/Tianming.ProjectData.Tests/Modules/Design/` 对应 6 组测试
- Modify: `src/Tianming.ProjectData/Tianming.ProjectData.csproj` + `src/Tianming.ProjectData/ServiceCollectionExtensions.cs`

**核心职责：** 每组 = 1 个 service + 1 个 category 类 + 1 个 data 类 + 3-5 个单测（Add/Update/Delete/Clear/ListAll round-trip）

**模板**（以 WorldRules 为例）：

```csharp
// src/Tianming.ProjectData/Modules/Design/WorldRules/WorldRulesService.cs
namespace TM.Services.Modules.ProjectData.Modules.Design.WorldRules;

public class WorldRulesService : ModuleServiceBase<WorldRulesCategory, WorldRulesData>
{
    public WorldRulesService() : base(modulePath: "Design/GlobalSettings/WorldRules") { }
    public List<WorldRulesData> GetAllWorldRules() => GetAllData();
    public void AddWorldRule(WorldRulesData data) { /* 略，从旧 service 端口 */ }
    public async Task AddWorldRuleAsync(WorldRulesData data) { /* 略 */ }
    public void UpdateWorldRule(WorldRulesData data) { /* 略 */ }
    public async Task UpdateWorldRuleAsync(WorldRulesData data) { /* 略 */ }
    public void DeleteWorldRule(string id) { /* 略 */ }
    public int ClearAllWorldRules() => ClearAllData();
}
```

**验收：** 六个 service 各通过 3+ 测试；合计 ≥ 18 新测试；`dotnet test` 全过

**Commit（每 service 一次 commit，共 6 次）：**
- `feat(projectdata): 端口 WorldRulesService + 测试`
- `feat(projectdata): 端口 CharacterRulesService + 测试`
- `feat(projectdata): 端口 FactionRulesService + 测试`
- `feat(projectdata): 端口 LocationRulesService + 测试`
- `feat(projectdata): 端口 PlotRulesService + 测试`
- `feat(projectdata): 端口 CreativeMaterialsService + 测试`

### Task M4.0.3：端口四个 Generation services + ChapterPipeline

**Files:**
- Create: `src/Tianming.ProjectData/Modules/Generate/Outline/` 整组（OutlineService + OutlineCategory + OutlineData）
- Create: `src/Tianming.ProjectData/Modules/Generate/VolumeDesign/` 整组
- Create: `src/Tianming.ProjectData/Modules/Generate/ChapterPlanning/` 整组
- Create: `src/Tianming.ProjectData/Modules/Generate/ContentConfig/` 整组
- Create: `src/Tianming.ProjectData/Generation/ChapterGenerationPipeline.cs`
- Create: `tests/Tianming.ProjectData.Tests/Modules/Generate/` 对应 4 组测试
- Create: `tests/Tianming.ProjectData.Tests/Generation/ChapterGenerationPipelineTests.cs`
- Modify: `src/Tianming.ProjectData/Tianming.ProjectData.csproj` + `src/Tianming.ProjectData/ServiceCollectionExtensions.cs`

**核心职责：** 前 4 组与 M4.0.2 同模板（ModuleServiceBase 派生 + CRUD + 测试）。`ChapterGenerationPipeline` 是独立实体，不继承 ModuleServiceBase：

**开工前置（M4.0.3 Step 0）：** 用 Explore subagent 确认 `ChapterGenerationPipeline` 真实类名与入口方法。预期有三种结果，按结果进入不同路径：
- (A) 已存在于 `src/Tianming.AI/` 或 `src/Tianming.ProjectData/` 下 → 跳过此 task，直接 Task M4.2.2 消费
- (B) 存在于 `Modules/Generate/` 或 `Services/` 下旧 WPF 目录 → 按"端口"模板做：复制 + 去 WPF 依赖 + 改到 portable namespace + 加 Compile Include + 单测
- (C) 不存在（旧版没实现管道化，可能是散装生成代码）→ 新建 `ChapterGenerationPipeline` 类，暴露 `Task<PipelineResult> RunAsync(ChapterId id, CancellationToken ct)`，内部分"加载 Fact Snapshot → 组 prompt → 调 IChatClient → 解析 CHANGES → 应用"五步；每步各 1 测试

**验收：** 新测试 ≥ 16 条；`dotnet test` 全过

**Commit（每 service 一次，ChapterGenerationPipeline 单独一次，共 5 次）：**
- `feat(projectdata): 端口 OutlineService + 测试`
- `feat(projectdata): 端口 VolumeDesignService + 测试`
- `feat(projectdata): 端口 ChapterService (章节规划) + 测试`
- `feat(projectdata): 端口 ContentConfigService + 测试`
- `feat(projectdata): ChapterGenerationPipeline 端口/实现 + 测试`

### M4.0 Gate：端口后基线

```bash
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -8
```

Expected: 新增 ≥ 34 条测试（1143 → 1177+）。

---

## Task M4.1：设计模块 6 页

**工作量估计：2 天**

### Task M4.1.1：共享控件 CategoryDataPageView + DataFormView

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/CategoryDataPageView.axaml`（.cs）
- Create: `src/Tianming.Desktop.Avalonia/Controls/DataFormView.axaml`（.cs）
- Create: `src/Tianming.Desktop.Avalonia/Controls/CategorySpec.cs`（参数化配置类型）
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/CategoryDataPageViewTests.cs`
- Modify: csproj

**核心职责：** 左侧 `TreeView` 绑定 category 集合（按 ViewModel 暴露的 `ObservableCollection<CategoryItem>`），中央 `ListBox` 绑定当前 category 下的 data 集合，右侧 `DataFormView` 按 selected item 的 `DataSchema` 动态生成表单字段。`CategorySpec` 定义每页差异（标题、字段列表、图标、默认新建模板）。

**关键接口：**

```csharp
public sealed record CategorySpec(
    string PageTitle,
    string PageIcon,
    IReadOnlyList<FieldDescriptor> Fields,
    Func<string> NewItemDefaultFactory,    // 返回 JSON 模板
    string StorageModulePath               // 如 "Design/GlobalSettings/WorldRules"
);

public sealed record FieldDescriptor(
    string PropertyName,
    string Label,
    FieldType Type,
    bool Required,
    string? Placeholder
);

public enum FieldType { SingleLineText, MultiLineText, Number, Tags, Enum }
```

**验收：** 1-2 VM 测试（CategoryDataPageViewModel 的 `AddCategoryCommand` / `DeleteCategoryCommand` / `SaveDataCommand`）；手工建一个 dummy spec 渲染页面可视化正确

**Commit：** `feat(ui): M4.1.1 共享控件 CategoryDataPageView`

### Task M4.1.2：六个 Rules 页面

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Views/Design/WorldRulesPage.axaml`（.cs）+ `ViewModels/Design/WorldRulesViewModel.cs`
- Create: `src/Tianming.Desktop.Avalonia/Views/Design/CharacterRulesPage.axaml` + VM
- Create: 同样的 Faction / Location / Plot / CreativeMaterials
- Modify: `Navigation/PageKeys.cs` + `Navigation/PageRegistry.cs` 注册 6 个 PageKey
- Modify: `ViewModels/Shell/LeftNavViewModel.cs` 加 6 项导航

**核心职责：** 每个 Page = 一行 XAML（`<ctl:CategoryDataPageView Spec="{Binding Spec}" />`），其 VM 构造器注入对应 Rules service 并生成 `CategorySpec`。

**模板**（WorldRulesViewModel）：

```csharp
public partial class WorldRulesViewModel : CategoryDataPageViewModel
{
    public WorldRulesViewModel(WorldRulesService service) : base(service)
    {
        Spec = new CategorySpec(
            PageTitle: "世界观规则",
            PageIcon: "🌍",
            Fields: new[]
            {
                new FieldDescriptor("Name", "名称", FieldType.SingleLineText, true, "如：九州大陆"),
                new FieldDescriptor("Description", "描述", FieldType.MultiLineText, false, null),
                new FieldDescriptor("Tags", "标签", FieldType.Tags, false, null)
            },
            NewItemDefaultFactory: () => "{}",
            StorageModulePath: "Design/GlobalSettings/WorldRules"
        );
    }
}
```

每个 `*RulesViewModel` ~20-30 行，只改 `Spec`。

**验收：** 导航栏能跳 6 页；每页能 CRUD；落盘路径正确（`<project>/Design/.../data.json`）

**Commit（推荐按页一次 commit）：** 6 个 commit

### M4.1 Gate

手工跑：新建项目 → 点"世界观规则" → 添加分类"主线" → 在该分类下新建"九州大陆" → 填名称描述 → 保存 → 关闭应用重启 → 数据恢复 → 其他 5 页同样操作。

---

## Task M4.2：生成规划（5 页 + 章节生成管道页）

**工作量估计：1.5-2 天**

### Task M4.2.1：Outline / VolumeDesign / ChapterPlanning / ContentConfig 四页

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Views/Generate/OutlinePage.axaml` + VM
- Create: 同样 VolumeDesignPage / ChapterPlanningPage / ContentConfigPage
- Modify: PageKeys / PageRegistry / LeftNavViewModel

**核心职责：** 与 M4.1 同模板，复用 `CategoryDataPageView`；VM 按各自 `CategorySpec` 差异化字段。

**验收：** 4 页 CRUD 正常

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

### M4.2 Gate

手工：选一章 → 生成 → 应用 → 章节 .md 文件出现在 `<project>/Generate/Chapters/`。

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

### M4.3 Gate

手工：打开一章 → 编辑 → 保存（⌘S）→ 预览面板同步；换 tab → 新 tab 内容独立；关闭应用重开状态保留。

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

### M4.4 Gate

全流程：选章 → 生成 → 应用 → 跳编辑器 → 文本在 → 可编辑 → 保存。

---

## Task M4.5：AI 对话面板（右栏实装）

**工作量估计：3 天**

前置：**确认对话驱动类的真实名字与签名**。M1 已在 `src/Tianming.AI/SemanticKernel/Conversation/` 端口了 `ConversationModeProfileCatalog`、`TagBasedThinkingStrategy`、mapper 等；需要找"入口服务"（可能是 `SKChatService` 或等效，也可能需要**新建**一个 `ConversationOrchestrator` 整合现有 portable 零件）。**M4.5 开工前先用 Explore subagent 确认**。

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

### Task M4.5.3：对话编排服务接入

**Files:**
- Create（如果不存在）: `src/Tianming.AI/SemanticKernel/Conversation/ConversationOrchestrator.cs`（portable）— 这个类把 M1 端口的 catalog / thinking strategy / session store / chat client 等组合成对外统一的 `StartConversationAsync(prompt, mode) → IAsyncEnumerable<ChatStreamDelta>`
- Create: `ViewModels/Conversation/ConversationPanelViewModel.cs` - 订阅流、驱动 ChatStreamView 的 VM

**核心职责：** ViewModel 调 `ConversationOrchestrator.StartConversationAsync(userInput, mode, references)` → 拿 `IAsyncEnumerable<ChatStreamDelta>` → 按 delta 类型（thinking / answer / toolCall）路由到当前助手消息 VM 的对应子块

**风险：** `ConversationOrchestrator` 如果 portable 层还未建立，M4.5 要同时新建 portable 类 + VM。Explore subagent 先确认。

**Commit：** 2-3

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

### M4.5 Gate

手工：右栏输入"你好" → 选 Ask 模式 → 发送 → 看到流式回复；切 Plan 模式 → 输入"帮我写一章" → plan 步骤可视化；新建会话 → 切回上一个会话 → 消息恢复。

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

### M4.6 Gate

手工：填 OpenAI-compatible API Key → 在对话面板试一次 → UsageStatisticsPage 能看到一条记录。

---

## 测试策略

- **Portable services（M4.0）：** xUnit 单测，每个 service ≥ 3 条（CRUD + round-trip）
- **VM 层：** 每个主要 VM ≥ 1-2 条单测（加载、保存、导航命令）
- **控件层：** 复杂控件（`CategoryDataPageView`、`MarkdownPreview`、`ChatStreamView`）各 1-2 条测试
- **不做：** Avalonia.Headless 端到端、UI 自动化脚本
- **手工验收清单：** 每个子里程碑有自己的 Gate 章节描述的手工冒烟场景

预期测试增量：
- M4.0：+34（6 rules × ~3 + 4 generation × ~3 + pipeline ~4）
- M4.1：+12（共享控件 + 6 VM）
- M4.2：+8（5 VM + pipeline VM）
- M4.3：+10（编辑器 / 预览 / tab VM）
- M4.5：+15（对话编排 / 流绑定 / plan / tool call）
- M4.6：+8（4 页 VM）

**M4 总计新增测试：~87 条**（1177 → ~1264）

## 风险与缓解

| ID | 风险 | 影响 | 缓解 |
|---|---|---|---|
| R1 | M4.0 端口 Rules services 发现依赖链太长（IWorkScopeService / VersionTracking / 等等）| 工期 +1 天 | 按需端口，只端口到 ModuleServiceBase 当前代码实际调用的最小依赖闭包 |
| R2 | AvaloniaEdit API 与预期不一致 | M4.3 卡壳 | 开工前 30 分钟跑一个 hello-world AvaloniaEdit sample 验证 API；若 AvaloniaEdit 不可用，降级到 `TextBox` multi-line + 自写简单行号 / 字数统计 |
| R3 | Markdig → Avalonia renderer 工作量超预估 | M4.3 +1 天 | 极简版只支持 heading / paragraph / bold / italic / list / code；其他降级为原文显示 |
| R4 | `ConversationOrchestrator` 不存在，需新建 | M4.5 +0.5 天 | M4.5 开工前 Explore subagent 确认；如需新建，先写 portable 接口 `IConversationOrchestrator`，默认实现用 M1 已端口零件拼装 |
| R5 | 流式绑定高频 UI 更新卡顿 | 体验 | `BulkEmitter` 16ms 批量 flush；实在不行降级为每 200ms 整块替换 |
| R6 | Rules services 端口后被旧 WPF 项目引用冲突（命名空间撞车）| build fail | 端口后的 portable 类用新命名空间（`TM.Services.Modules.ProjectData.Modules.Design.*`）；旧 WPF 项目暂不处理，M7 砍掉或由它自行 merge |
| R7 | CategoryDataPageView 动态字段生成不够灵活覆盖所有页面 | M4.1 某页特例需单独写 | 预留 `CategorySpec.CustomView: UserControl?` 字段做 override 入口 |

## 验收标准（M4 整体）

1. `dotnet build Tianming.MacMigration.sln` → 0 Warning / 0 Error
2. `dotnet test Tianming.MacMigration.sln` → ~1264 全过
3. `Scripts/check-windows-bindings.sh src/ tests/` → 0 命中
4. 全流程手工：新建项目 → 世界观/角色/势力/地点/剧情 5 大规则填一些 → 大纲/分卷/章节规划 → 生成一章 → 编辑器里修改 → 保存 → 关应用 → 重开 → 状态恢复
5. 对话面板 Ask / Plan / Agent 三模式至少各跑一次
6. API Key 能保存（Keychain 或明文 JSON 皆可，M5 再升级）
7. 每子里程碑分别有独立 commit 组

**完成后进入 M5。**

## 执行建议

> 此 plan 为里程碑级蓝图。每个子里程碑（M4.0 / M4.1 / ... / M4.6）**开工前**：
>
> 1. 用 Explore subagent 确认依赖类真实签名（防止盲写）
> 2. 用 superpowers:writing-plans skill 生成该子里程碑的 step-level plan（bite-sized，TDD，完整代码）
> 3. 用 superpowers:subagent-driven-development 或 executing-plans 执行
>
> 这样既避免"一次性把 6000+ 行 plan 写好"的过度承诺，又能让每个子里程碑的细节在最新环境下精确落地。
