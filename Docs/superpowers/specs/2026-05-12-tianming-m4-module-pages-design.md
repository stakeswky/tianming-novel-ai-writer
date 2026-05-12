# 天命 macOS 迁移 — M4 核心模块页面迁移设计

日期：2026-05-12
分支：`m4/module-pages-2026-05-12`（计划，本 spec 在 `m2-m6/specs-2026-05-12`）
依据：`Docs/macOS迁移/功能对齐审计与迁移设计草案.md`、`Docs/macOS迁移/功能对齐矩阵.md`、`Docs/superpowers/specs/2026-05-12-tianming-m3-avalonia-shell-design.md`

## 1. 范围与边界

### 1.1 纳入 M4

按"创作闭环优先"顺序分 6 个子里程碑（M4.1–M4.6）串并行：

- **M4.1 设计模块**：世界观规则、角色规则、势力规则、地点规则、剧情规则、创意素材库六个 Category + Data 型页面（共享控件 `CategoryDataPageView`）。
- **M4.2 生成规划模块**：大纲、分卷设计、章节规划、蓝图、内容配置五个页面（共享 `CategoryDataPageView` 扩展 + 专用 `BlueprintGraphView`）。
- **M4.3 章节编辑器**：Markdown 编辑器（`AvaloniaEdit` 装饰）、多标签页、章节头/脚元数据、即时字数统计、章节保存/staging、章节预览（Markdig Avalonia 渲染）。
- **M4.4 章节生成管道 UI**：生成门禁可视化、CHANGES 协议 preview、Fact Snapshot 展示、进度 Hub 绑定、生成取消与续写。
- **M4.5 校验与修复**：统一校验页面、卷级汇总、校验报告、章节修复弹窗 + 生成修复提示词可视化。
- **M4.6 AI 模型 / 提示词管理 / 对话面板**：模型管理、API Key 配置、多 Key 轮换可视化、用量统计、提示词 CRUD、提示词版本测试、右侧 SK 对话面板（Ask/Plan/Agent 三模式）、会话历史。

对话面板在 M3 只是占位，M4.6 实装：流式输出、thinking 标签分流、引用 @选择下拉、工具调用可视化、计划模式 step 展示与执行、消息压缩提示、历史加载/命名。

### 1.2 不在 M4 范围

- 系统偏好页面（主题设计器、定时主题、字体管理、自动主题、日志输出设置、数据清理、系统信息/监控/诊断/运行环境等 — 归并入 M4.7 或 M5）
- 智能拆书 UI（依赖 M2 WebView2 替换；若 M2 完成则 M4.7 可并入）
- 服务端登录/订阅/账号页面（M6）
- macOS 平台偏好真实接入（M5）

### 1.3 决策记录

| 编号 | 决策 |
|---|---|
| Q1 | Markdown 编辑器：`AvaloniaEdit`（AvalonEdit 的 Avalonia 分支），自写最小化 Markdown 语法高亮；Markdig Avalonia renderer 做只读预览 |
| Q2 | Diff 视图：保留 `DiffPlex` 核心计算，自写 `DiffView` 控件（左右分栏 + 行着色 + chunk 导航） |
| Q3 | CategoryDataPageView 作为共享控件，参数化 `CategorySpec`，Design/Generate 两大模块的规则/设计页复用 |
| Q4 | 页面缓存策略：常用页面（Dashboard/Design/Generate/Editor）常驻 VM；较少用页面按导航进入 Transient |
| Q5 | VM 与 portable service 的关系：所有业务逻辑走 portable service；VM 仅负责绑定/命令调度/状态投影 |
| Q6 | 对话面板流式：`IAsyncEnumerable<ChatStreamDelta>` → `ObservableCollection<ChatMessageViewModel>`，MainThread 批量 flush（16ms）避免过度重绘 |
| Q7 | 章节标签页持久化：`editor_tabs.json` 存 open/active/cursor 位置；切项目清空 |
| Q8 | M4 子里程碑合入 main 节奏：每个子里程碑独立 PR；主分支要求 CI 过（本地脚本） |
| Q9 | 视觉对齐：以原版截图为基线但不强求像素级还原；布局与交互优先于视觉细节 |

## 2. 架构改造

### 2.1 视图-ViewModel-服务 分层

```
View (AXAML)
  ↓ x:DataType 绑定
ViewModel (CommunityToolkit.Mvvm)
  ↓ 构造器注入
Portable Service (Tianming.ProjectData / Tianming.AI / Tianming.Framework)
  ↓ 读写
本地 JSON / 项目数据
```

ViewModel 约束：
- 不允许引入 Avalonia 命名空间（除 `Dispatcher` 走 `IDispatcherScheduler` 封装）
- 所有异步流处理用 `ObservableCollection<T>` + `AddRange` 扩展
- 导航走 `INavigationService`，不直接 `new Window`

### 2.2 共享组件清单

新增公共控件（M4.1 Wave 0 先行，M4.2+ 复用）：

```
Controls/
├── CategoryDataPageView.axaml        （左分类树 + 右数据表单）
├── DataListView.axaml                 （分页/过滤/选择）
├── DataFormView.axaml                 （按 DataSchema 动态生成字段）
├── MarkdownEditor.axaml               （AvaloniaEdit 包装）
├── MarkdownPreview.axaml              （Markdig Avalonia 渲染）
├── DiffView.axaml                     （DiffPlex 驱动）
├── ChapterTabBar.axaml                （多标签页）
├── ChatStreamView.axaml               （流式消息列表 + thinking 折叠）
├── ToolCallCard.axaml                 （对话工具调用可视化）
├── PlanStepListView.axaml             （Plan 模式 step 列表 + 执行状态）
├── ReferenceDropdown.axaml            （@引用下拉）
├── ProgressiveSummaryPanel.axaml
├── ValidationIssueListView.axaml
├── FactSnapshotView.axaml
└── ChangesPreview.axaml               （CHANGES 协议可视化）
```

每个控件配对 VM；VM 走 `ObservableProperty` 源生成器。

### 2.3 页面清单与映射

#### M4.1 设计模块（6 页）

| PageKey | View | 复用基类 | portable service |
|---|---|---|---|
| `design.world` | WorldRulesPage.axaml | CategoryDataPageView | `WorldRulesService`（M1 D1）|
| `design.character` | CharacterRulesPage.axaml | CategoryDataPageView | `CharacterRulesService` |
| `design.faction` | FactionRulesPage.axaml | CategoryDataPageView | `FactionRulesService` |
| `design.location` | LocationRulesPage.axaml | CategoryDataPageView | `LocationRulesService` |
| `design.plot` | PlotRulesPage.axaml | CategoryDataPageView | `PlotRulesService` |
| `design.materials` | CreativeMaterialsPage.axaml | CategoryDataPageView | `CreativeMaterialsService` |

每页差异：`CategorySpec`（字段定义、图标、默认分类）、右侧数据表单字段。

#### M4.2 生成规划模块（5 页）

| PageKey | View | 备注 |
|---|---|---|
| `generate.outline` | OutlinePage.axaml | CategoryDataPageView |
| `generate.volume` | VolumeDesignPage.axaml | CategoryDataPageView |
| `generate.chapter` | ChapterPlanningPage.axaml | CategoryDataPageView + `ChapterPipelinePanel` |
| `generate.blueprint` | BlueprintPage.axaml | 自绘 `BlueprintGraphView`（节点 + 连线） |
| `generate.contentconfig` | ContentConfigPage.axaml | CategoryDataPageView |

#### M4.3 编辑器（2 页）

| PageKey | View |
|---|---|
| `editor.tabs` | EditorWorkspaceView.axaml（ChapterTabBar + MarkdownEditor + 元数据边栏） |
| `editor.preview` | EditorPreviewView.axaml（MarkdownPreview，分栏或全屏） |

#### M4.4 章节生成（1 综合页 + 弹窗）

| PageKey | View |
|---|---|
| `generate.pipeline` | ChapterPipelinePage.axaml（选章 → Fact Snapshot preview → 生成门禁 → CHANGES preview → 应用）|

#### M4.5 校验修复（3 页 + 弹窗）

| PageKey | View |
|---|---|
| `validate.summary` | ValidationSummaryPage.axaml |
| `validate.report` | ValidationReportPage.axaml |
| `validate.repair` | ChapterRepairPage.axaml（ChapterRepairProblemHintBuilder 驱动）|

#### M4.6 AI / 对话（5 页）

| PageKey | View |
|---|---|
| `ai.models` | ModelManagementPage.axaml |
| `ai.keys` | ApiKeysPage.axaml（Keychain 接入占位，M5 真值填） |
| `ai.usage` | UsageStatisticsPage.axaml |
| `ai.prompts` | PromptManagementPage.axaml |
| `ai.version-test` | PromptVersionTestPage.axaml |

对话面板在 `RightConversationView` 内置，不走导航：
- `ChatStreamView` + `ReferenceDropdown` + `PlanStepListView` + mode 切换（Ask/Plan/Agent） + 会话历史抽屉

### 2.4 目录结构

```
src/Tianming.Desktop.Avalonia/
├── Views/
│   ├── Shell/...                （M3 已建）
│   ├── Design/
│   │   ├── WorldRulesPage.axaml
│   │   ├── CharacterRulesPage.axaml
│   │   ├── FactionRulesPage.axaml
│   │   ├── LocationRulesPage.axaml
│   │   ├── PlotRulesPage.axaml
│   │   └── CreativeMaterialsPage.axaml
│   ├── Generate/
│   │   ├── OutlinePage.axaml
│   │   ├── VolumeDesignPage.axaml
│   │   ├── ChapterPlanningPage.axaml
│   │   ├── BlueprintPage.axaml
│   │   ├── ContentConfigPage.axaml
│   │   ├── ChapterPipelinePage.axaml
│   │   └── Controls/BlueprintGraphView.axaml
│   ├── Editor/
│   │   ├── EditorWorkspaceView.axaml
│   │   └── EditorPreviewView.axaml
│   ├── Validate/
│   │   ├── ValidationSummaryPage.axaml
│   │   ├── ValidationReportPage.axaml
│   │   └── ChapterRepairPage.axaml
│   ├── AI/
│   │   ├── ModelManagementPage.axaml
│   │   ├── ApiKeysPage.axaml
│   │   ├── UsageStatisticsPage.axaml
│   │   ├── PromptManagementPage.axaml
│   │   └── PromptVersionTestPage.axaml
│   └── Conversation/               （右侧面板组件）
│       ├── ConversationPanelView.axaml
│       ├── ChatStreamView.axaml
│       └── ToolCallCard.axaml
├── ViewModels/
│   └── ...（Views 对称镜像）
└── Controls/ (M3 建 + M4 扩)
```

### 2.5 Markdown 编辑器 & Diff 控件

`MarkdownEditor`:
- 基于 `AvaloniaEdit.TextEditor`
- 轻量语法高亮（# 标题、加粗/斜体、链接、代码块）通过 `IHighlightingDefinition` 注入
- 快捷键：⌘+B/I/K（加粗/斜体/链接）、⌘+S（保存）、⌘+Z/⇧⌘+Z（撤销/重做）
- 字数统计：`TextChanged` 防抖（150ms）调 `PortableChineseWordCounter`
- 草稿自动保存：`DocumentChanged` 防抖（1000ms）落盘 `<chapter>.staging`

`MarkdownPreview`:
- `Markdig.AvaloniaUI` 或自写 `Markdig` → Avalonia `FlowDocument`-like visual tree
- 表格 / 代码 / 引用块 / 图片（本地相对路径） / 链接
- 渲染缓存：按 source hash 缓存

`DiffView`:
- `DiffPlex` 计算行级 diff
- 左右分栏，行号对齐，增/删/改着色
- 变更导航：上下 chunk 跳转

### 2.6 对话面板（M4.6）

```
RightConversationView
├── ConversationHeader        （会话名 / 模式切换 / 历史 / 新建 / 压缩指示）
├── ChatStreamView            （滚动容器，自动滚到底）
│   ├── AssistantMessageCard  （thinking/answer/toolCall 分块）
│   ├── UserMessageCard
│   ├── PlanCard              （Plan 模式 step 列表）
│   └── ExecutionTraceCard    （Agent 模式工具调用轨迹）
├── ReferenceDropdown         （@ 触发：续写/重写/仿写/规则）
├── InputBox                  （多行 + 附件 + Mode pill + 发送）
└── BusyIndicator             （压缩中/等待首 token）
```

VM 责任：
- 订阅 `SKChatService` 的 `IAsyncEnumerable<StreamEvent>`；按 `TagBasedThinkingStrategy`（M1 端口）分流
- 消息集合 `ObservableCollection<ChatMessageVM>`，流式写入用 `BulkEmitter`（每 16ms flush）
- 模式切换绑定 `ModeProfileRegistry`
- 会话切换走 `FileSessionStore`
- 引用 `@` 触发 `ReferenceCatalog` 查询

## 3. 工作拆分（Wave 模型）

### 3.1 Wave 0（M4 主代理串行，~2 小时）

1. 建分支 `m4/module-pages-2026-05-12`
2. 新增共享控件骨架（13 个 Controls/ 文件，空壳 + 基础样式）
3. 新增 `PageRegistry` 全量 28 个 `PageKey` 与 View-VM 映射（M4 全期清单）
4. 扩展左侧导航树（`LeftNavView`）加入 M4 所有一级/二级节点（只跳 stub 页）
5. 为 M4.1–M4.6 各建子目录（Views / ViewModels）
6. `dotnet build` + 冒烟测试：所有导航节点可点（打开空白 stub 页即可）
7. Commit `feat(ui): M4 骨架与共享控件占位`

### 3.2 M4.1 Wave（6 agents 并行，~3 小时）

| ID | 页面 | portable 接线 | 测试 |
|---|---|---|---|
| D1 | WorldRulesPage + VM | `WorldRulesService` | VM 单元测试 + AXAML 编译 |
| D2 | CharacterRulesPage + VM | `CharacterRulesService` | 同上 |
| D3 | FactionRulesPage + VM | `FactionRulesService` | 同上 |
| D4 | LocationRulesPage + VM | `LocationRulesService` | 同上 |
| D5 | PlotRulesPage + VM | `PlotRulesService` | 同上 |
| D6 | CreativeMaterialsPage + VM | `CreativeMaterialsService` | 同上 |

约束：所有页共用 `CategoryDataPageView`，仅差异在 `CategorySpec`；agent 不动共享控件（若需改先提单点修复）。合流后 1 commit `feat(ui): M4.1 设计模块 6 页`。

### 3.3 M4.2 Wave（5 agents 并行，~4 小时）

| ID | 页面 | portable 接线 |
|---|---|---|
| G1 | OutlinePage + VM | `OutlineService` |
| G2 | VolumeDesignPage + VM | `VolumeDesignService` |
| G3 | ChapterPlanningPage + VM | `ChapterService` |
| G4 | BlueprintPage + BlueprintGraphView + VM | `BlueprintService` |
| G5 | ContentConfigPage + VM | `ContentConfigService` |

`BlueprintGraphView` 自绘：`Canvas` + `ItemsControl` + 连线用 `Path`，拖拽节点、缩放 `Matrix` 变换。

### 3.4 M4.3 Wave（3 agents 并行，~5 小时）

| ID | 内容 |
|---|---|
| E1 | MarkdownEditor 控件 + 快捷键 + 字数统计 + 草稿保存 |
| E2 | MarkdownPreview 控件 + Markdig Avalonia renderer |
| E3 | EditorWorkspaceView + ChapterTabBar + 元数据侧栏 + `editor_tabs.json` |

依赖：E3 依赖 E1；Wave 内 E1 + E2 先并行，E3 合流后单独做（~2 小时追加）。

### 3.5 M4.4 Wave（2 agents 并行，~3 小时）

| ID | 内容 |
|---|---|
| P1 | ChapterPipelinePage + VM（选章 / Fact Snapshot / 门禁 / CHANGES preview / 应用） |
| P2 | ChangesPreview 控件 + FactSnapshotView 控件 + ProgressiveSummaryPanel |

### 3.6 M4.5 Wave（3 agents 并行，~3 小时）

| ID | 内容 |
|---|---|
| V1 | ValidationSummaryPage + VolumeValidationSummary 绑定 |
| V2 | ValidationReportPage + ValidationIssueListView |
| V3 | ChapterRepairPage + RepairPromptPreview + 修复结果应用 |

### 3.7 M4.6 Wave（对话面板 Wave 2.0 + AI 管理 Wave 2.1）

**Wave 2.0（2 agents 并行，~5 小时）**

| ID | 内容 |
|---|---|
| C1 | ConversationPanelView + ChatStreamView + ToolCallCard + 流式绑定 + PlanStepListView |
| C2 | ReferenceDropdown + InputBox + ModePill + 会话历史抽屉 |

**Wave 2.1（5 agents 并行，~4 小时）**

| ID | 内容 |
|---|---|
| A1 | ModelManagementPage |
| A2 | ApiKeysPage（安全 UI，接入 `IApiKeySecretStore`；M5 真值联调） |
| A3 | UsageStatisticsPage |
| A4 | PromptManagementPage |
| A5 | PromptVersionTestPage |

### 3.8 合流与人工验收（主代理串行）

每子里程碑完成后：
1. `dotnet build` + `dotnet test` 全过
2. `Scripts/check-windows-bindings.sh` 零命中
3. 本机启动 UI 人工走通该子里程碑场景清单
4. 生成一张截图存 `Docs/macOS迁移/Screenshots/M4.X-<page>.png`
5. 更新功能对齐矩阵对应行
6. 单独 commit / push

## 4. 依赖图

```
M4.1 设计 ───┐
             ├──► M4.2 生成规划 ───┐
M4.3 编辑器 ─┤                   │
             │                   ├──► M4.4 生成管道 ──► M4.5 校验修复
M4.6.1 对话 ─┘                   │
M4.6.2 AI 管理 (独立) ────────────┘
```

顺序可执行路径（实际）：
1. Wave 0（骨架）
2. M4.1 设计六页（并行）
3. M4.2 生成五页（并行）+ M4.3 编辑器（并行）
4. M4.4 生成管道（依赖 M4.2/M4.3）
5. M4.5 校验修复（依赖 M4.4）
6. M4.6.1 对话面板（可与 M4.3 起并行）
7. M4.6.2 AI 管理五页（独立）

估算总时间：5 个工作日（含人工验收 & 视觉打磨）。

## 5. 测试策略

| 层次 | 覆盖 | 工具 |
|---|---|---|
| VM 单元测试 | 所有 M4 新增 VM 的纯逻辑（命令可用性、属性计算、服务调用次序） | xUnit + FakeItEasy（或手写 fake） |
| 控件 UI 测试 | Avalonia.Headless 跑关键交互（⌘+B、@触发、流式追加） | Avalonia.Headless |
| 快照对比 | 共享控件在固定样本数据下渲染 snapshot，PR 对比 diff | `Verify.Avalonia` |
| 人工验收 | 每子里程碑的场景清单；失败项挂 `Docs/macOS迁移/M4-人工验收/` | 清单文件 |

VM 测试是主力（目标占 70%）；Headless UI 测试做关键路径（占 20%）；快照对比做视觉守护（占 10%）。

测试总量目标：相比 M3 基线再增 ≥ 300 用例。

## 6. Agent Prompt 模板

```
你是 Tianming macOS 迁移 M4.<X> 的 <ID> agent。
仓库：/Users/jimmy/Downloads/tianming-novel-ai-writer
分支：m4/module-pages-2026-05-12（已切好）
父子里程碑：M4.<X>

任务范围：<具体页面>
涉及 portable service：<具体 service 清单>
共享控件：<复用的控件，不允许修改>
新增目标：
- Views/<Module>/<Page>.axaml
- ViewModels/<Module>/<Page>ViewModel.cs
- 对称测试

硬约束：
- 只新增文件；共享控件必须复用不得修改（需改走单点修复 agent）
- 不引入新 NuGet；不动 csproj / sln / global.json
- VM 不引用 Avalonia 命名空间（Dispatcher 走 IDispatcherScheduler）
- 所有业务走 portable service
- 不 commit / 不 push

交付：
- 新增文件清单
- `dotnet build src/Tianming.Desktop.Avalonia` → 0 警告 0 错误
- VM 测试 tail 10 行
- 页面交互说明（<100 字）
```

## 7. 风险与回滚

### 7.1 主要风险

| ID | 风险 | 影响 | 缓解 |
|---|---|---|---|
| R1 | AvaloniaEdit 在 macOS 渲染/快捷键不稳 | 编辑器体验受阻 | Wave 0 先做 `MarkdownEditor` 冒烟 demo；不稳则退到 `TextBox` 简版并标注降级 |
| R2 | Markdig Avalonia renderer 生态不成熟 | 预览渲染缺特性 | 用自写最小 renderer；表格/代码块 fallback 到 monospace |
| R3 | `BlueprintGraphView` 自绘节点图复杂度超估 | M4.2 延期 | 退回简化的树形 + 列表展示；真正图视图推 v1.1 |
| R4 | 28 个页面 agent 并行导致 csproj / PageRegistry 合流冲突 | 合入阻塞 | Wave 0 把 28 个 PageKey 与 View-VM 映射一次写满；agent 不动 PageRegistry 只填实体 |
| R5 | 对话面板流式高频 UI 刷新卡顿 | 体验劣化 | `BulkEmitter` 16ms batch；`VirtualizingStackPanel` 渲染 |
| R6 | 人工验收清单与实际交互脱节 | 回归风险 | 每子里程碑随 commit 附截图；合流 PR 带手动验收清单 |
| R7 | Dispatcher 误用导致跨线程异常 | 偶发崩溃 | `IDispatcherScheduler` 抽象强制走 UI 线程；lint rule / analyzer 可选 |
| R8 | VM 测试量暴涨带来维护成本 | 测试稳定性 | 手写 fake 替代 mocking 框架；共享 `TestFixtureBuilder` |
| R9 | Markdown 编辑器键盘快捷键与 macOS 系统快捷键冲突 | 使用性 | 全部快捷键过 `KeyboardShortcutsCatalog` 注册并可重配（设置页 M4.7+） |

### 7.2 回滚策略

- 每子里程碑单独分支合入 PR，失败子里程碑单独 revert
- 共享控件若需改走"单点修复 agent"，修改单独 commit，便于 revert 不影响页面 agent
- `Docs/macOS迁移/功能对齐矩阵.md` 更新放每子里程碑最后一个 commit

### 7.3 退路

- 若 R1 发生：M4.3 退化为 `TextBox` 多行 + 基础字数统计；真实 AvaloniaEdit 留 v1.1
- 若 R3 发生：`BlueprintPage` 简化为列表 + 关系选择；图视图留 v1.1
- 若 M4.6.1 对话面板复杂度超估：先发布 Ask 模式，Plan / Agent 模式推 v1.05

## 8. 验收标准

M4 全完成的判据：

1. `dotnet build Tianming.MacMigration.sln` → 0 Warning / 0 Error
2. `dotnet test Tianming.MacMigration.sln -v minimal` → 全过（较 M3 基线增加 ≥ 300 用例）
3. `Scripts/check-windows-bindings.sh src/ tests/` → 0 命中
4. macOS 本机走通"创作闭环"：新建项目 → 填写设计规则 → 大纲/分卷/章节规划 → 编辑器生成 → 章节生成管道 → 校验 → 修复；每步有截图
5. 对话面板三种模式均可用（Ask 必过；Plan/Agent 至少冒烟一次）
6. `Docs/macOS迁移/功能对齐矩阵.md` 中 M4 涉及 28+ 行状态更新为"已 UI 落地"
7. `Docs/macOS迁移/M4-人工验收/` 目录含每子里程碑的验收清单与截图
8. 每子里程碑分支已 push，PR 已合入 main

完成后进入 M5（macOS 平台能力补齐）。
