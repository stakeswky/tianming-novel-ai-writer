# 天命 macOS 迁移 — M4 核心模块页面迁移设计（自用版）

日期：2026-05-12
分支：`m2-m6/specs-2026-05-12`
依据：`Docs/superpowers/specs/2026-05-12-tianming-m3-avalonia-shell-design.md`
定位：**个人自用**，不做公开分发

## 1. 范围与边界

### 1.1 纳入 M4（按创作闭环顺序）

按你实际写小说的流程排序，每步做完能用了再进下一步：

- **M4.1 设计模块（规则/素材）**：6 个共用 `CategoryDataPageView` 的页面 — 世界观、角色、势力、地点、剧情、创意素材库。
- **M4.2 生成规划**：4 页 — 大纲、分卷设计、章节规划、内容配置。蓝图页降级为列表视图（不做图形化节点连线）。
- **M4.3 章节编辑器**：`AvaloniaEdit` 包装的 Markdown 编辑器 + 多标签页 + 章节元数据 + 实时字数统计 + Markdig 预览。
- **M4.4 章节生成管道**：选章 → Fact Snapshot 预览 → 生成门禁 → CHANGES preview → 应用。
- **M4.5 AI 对话面板（右栏）**：流式输出 + thinking 分流 + 引用 `@` 下拉 + Ask/Plan/Agent 三种模式 + 会话历史。这是右栏实装，M3 只占位。
- **M4.6 AI 模型 / 提示词管理**：模型管理、API Key 配置（写 Keychain）、提示词 CRUD、用量统计。提示词版本测试降级：保留 portable 能力但 UI 只做单版本运行。
- **M4.7 v2.8.7 入口预留**：只做可见入口与状态位，不做内核重写；把 HumanizeRules、CHANGES Canonicalizer、分层校验、WAL、一键成书、Agent 写工程能力放入 M6。

### 1.2 不做

- 统一校验 / 卷级汇总 / 章节修复 UI —— portable 层 M1 已端口，可从命令行/临时脚本触发；UI 等你真用上再说
- 智能拆书页面 —— M2 已有 `LocalFileFetcher`，UI 按需可极简做一个"拖入 HTML → 看拆分结果"
- 蓝图图形化视图 —— 先列表凑合，你要的画图体验不如白板
- 系统偏好页面（字体管理器、主题设计器、定时主题、日志输出、数据清理、系统监控等）—— 真需要时单开偏好窗口，M4 不铺
- 编辑器复杂能力（Diff 查看、多光标、代码折叠、内联 AI 补全）—— AvaloniaEdit 默认能力先用着
- v2.8.7 写作内核升级 —— M4 只做入口和 UI 展示骨架，真正实现放 M6
- 多 Wave / 多 agent 并行 —— 一人写，按子里程碑串行

### 1.3 决策

| 编号 | 决策 |
|---|---|
| Q1 | Markdown 编辑器：`AvaloniaEdit`（AvalonEdit 的 Avalonia 分支）；语法高亮用内置 Markdown 规则 |
| Q2 | Markdown 预览：`Markdig` + 自写极简 Avalonia renderer（或直接 TextBlock 堆叠） |
| Q3 | `CategoryDataPageView` 共享控件：左分类树 + 右数据表单，`CategorySpec` 参数化六大设计模块 |
| Q4 | 页面缓存：Design / Generate / Editor / AI 对话常驻；其他 Transient |
| Q5 | 对话流式绑定：`IAsyncEnumerable<ChatStreamDelta>` → `ObservableCollection`，每 16ms flush |
| Q6 | 标签页持久化：`editor_tabs.json` 存 open/active/cursor；切项目清空 |
| Q7 | API Key 写入：M4.6 调 `IApiKeySecretStore`；真 Keychain 在 M5，M4 先用临时内存/明文 JSON 存（本机自用，等 M5 替换） |
| Q8 | v2.8.7 能力在 M4 只做“能看到、能挂接、能替换”的 UI 契约，业务实现统一推到 M6 |

## 2. 架构

### 2.1 共享控件

```
Controls/
├── CategoryDataPageView.axaml    （左分类树 + 右数据表单，6 设计页 + 4 规划页共用）
├── DataFormView.axaml            （按 DataSchema 动态生成字段）
├── MarkdownEditor.axaml          （AvaloniaEdit 包装）
├── MarkdownPreview.axaml         （Markdig 渲染）
├── ChapterTabBar.axaml           （多标签页）
├── ChatStreamView.axaml          （流式消息列表 + thinking 折叠）
├── ReferenceDropdown.axaml       （@ 触发的引用下拉）
├── ToolCallCard.axaml            （工具调用可视化）
├── ChangesPreview.axaml          （CHANGES 协议可视化）
├── FactSnapshotView.axaml
└── ValidationIssueList.axaml     （M4.4 门禁结果展示用）
```

每控件配 VM；VM 走 `ObservableProperty` 源生成器。

### 2.2 页面映射

| PageKey | View | portable service |
|---|---|---|
| `design.world` | WorldRulesPage | WorldRulesService（M1 D1）|
| `design.character` | CharacterRulesPage | CharacterRulesService |
| `design.faction` | FactionRulesPage | FactionRulesService |
| `design.location` | LocationRulesPage | LocationRulesService |
| `design.plot` | PlotRulesPage | PlotRulesService |
| `design.materials` | CreativeMaterialsPage | CreativeMaterialsService |
| `generate.outline` | OutlinePage | OutlineService |
| `generate.volume` | VolumeDesignPage | VolumeDesignService |
| `generate.chapter` | ChapterPlanningPage | ChapterService |
| `generate.contentconfig` | ContentConfigPage | ContentConfigService |
| `generate.pipeline` | ChapterPipelinePage | ChapterGenerationPipeline |
| `editor.workspace` | EditorWorkspaceView | ChapterContentStore |
| `ai.models` | ModelManagementPage | FileAIConfigurationStore |
| `ai.keys` | ApiKeysPage | FileAIConfigurationStore + IApiKeySecretStore |
| `ai.prompts` | PromptManagementPage | FilePromptTemplateStore |
| `ai.usage` | UsageStatisticsPage | FileUsageStatisticsService |

右栏 `ConversationPanelView` 不走导航，常驻。

### 2.3 VM 约束

- 所有业务逻辑走 portable service；VM 只做绑定/命令/状态投影
- 异步结果走 `DispatcherScheduler` 切 UI 线程
- 导航走 `INavigationService`，不直接 `new Window`
- 不引入 Avalonia 命名空间（除少量 `Dispatcher` / `Visual` 帮助）

### 2.4 v2.8.7 入口映射

| v2.8.7 能力 | M4 只做什么 | M6 真正实现什么 |
|---|---|---|
| 维度漂移 / 遗漏 / Deadline / Pledge / SecretReveal | `FactSnapshotView` 增加“追踪债务/约束”空状态和问题列表槽位 | 扩展 Tracking 模型、提取器、重建订阅和告警 |
| HumanizeRules + 双通道润色 | `ChapterPipelinePage` 增加“润色”步骤占位和字数偏差提示位 | 本地规则、在线润色、专用润色 API、分段重试 |
| CHANGES Canonicalizer | `ChangesPreview` 支持显示“原始/归一化”两列，但 M4 可先只显示原始 | 实现归一化、重要性校正、协议保留 |
| 分层抽样 + 向量定位校验 | `ValidationIssueList` 支持章节定位、向量命中摘要字段 | 校验子系统拆分、抽样策略、向量联动 |
| 一键成书 + 断点续跑 | 生成规划页预留 `generate.oneclick` PageKey 和按钮禁用态 | 10 步流水线、前置补全、断点续跑、短篇蓝图 |
| WAL / 里程碑压缩 / 卷级归档 | 编辑器和 pipeline 展示“恢复中/可恢复草稿”状态位 | 章节写入 WAL、恢复控制器、归档与压缩 |
| AI middleware / 多模型路由 | `ai.models` 增加任务类型槽位：对话、正文、润色、校验 | 7 层 middleware、路由、降级、能力探测 |
| Agent 写工程能力 | `ToolCallCard` 支持“待确认变更”视觉状态 | WorkspacePlugin / DataEditPlugin / ContentEditPlugin |

## 3. 工作拆分

**每个子里程碑独立串行**，跑通后再进下一个：

### M4.1 设计模块（~2 天）

步骤：
1. 写 `CategoryDataPageView` + `DataFormView` 共享控件（半天）
2. 六个页面依次：World → Character → Faction → Location → Plot → Materials，每个 ~30-40 分钟（布局复用，只改 CategorySpec）
3. 左侧导航加入 6 个节点
4. 每写完一个在 UI 里手试 CRUD

Commit：`feat(ui): M4.1 设计模块 6 页`。

### M4.2 生成规划（~1.5 天）

- Outline / VolumeDesign / ChapterPlanning / ContentConfig 四页，复用 `CategoryDataPageView`（~3 小时）
- `ChapterPipelinePage`：独立布局，左选章、中 Fact Snapshot + CHANGES preview、右执行日志（~4 小时）
- Commit：`feat(ui): M4.2 生成规划 + 章节生成管道`

### M4.3 编辑器（~2 天）

1. `MarkdownEditor` 控件（AvaloniaEdit 包装，基础高亮 + 快捷键）~4 小时
2. `MarkdownPreview` 控件（Markdig → TextBlock 堆叠）~3 小时
3. `EditorWorkspaceView` + `ChapterTabBar` + `editor_tabs.json` 持久化 ~4 小时
4. Commit：`feat(ui): M4.3 章节编辑器`

### M4.4 章节生成管道串联（~半天）

- 把 M4.2 的 pipeline 页与 M4.3 编辑器打通：生成完成 → 自动打开编辑器标签
- 增加 M6 预留状态：润色步骤、CHANGES 归一化状态、WAL 可恢复状态均可显示“未启用”
- Commit：`feat(ui): M4.4 章节生成闭环`

### M4.5 AI 对话面板（~3 天）

1. `ChatStreamView` + `ReferenceDropdown` + 输入框 + 模式切换 ~6 小时
2. 接 `SKChatService`（M1 已端口），流式消息绑定 ~4 小时
3. `PlanStepListView` + Plan 模式执行路径 ~3 小时
4. `ToolCallCard` + Agent 模式工具调用展示 ~3 小时
5. 会话历史抽屉 + `FileSessionStore` 接线 ~3 小时
6. `ToolCallCard` 增加“待确认变更”状态，为 M6 DataEditPlugin / ContentEditPlugin 做 UI 契约
7. Commit：`feat(ui): M4.5 AI 对话面板三模式`

### M4.6 AI 管理（~1.5 天）

- `ModelManagementPage` / `ApiKeysPage` / `PromptManagementPage` / `UsageStatisticsPage` 串行 ~10 小时
- 任务类型槽位先建起来：Chat / Writing / Polish / Validation；M6 再接真实多模型路由
- Commit：`feat(ui): M4.6 AI 模型/提示词/用量`

## 4. 测试

- VM 单测：每页至少 1-2 个（加载/保存/导航）
- UI 手工：每个子里程碑做完你自己点一遍常用路径
- 不写：E2E 脚本 / Avalonia.Headless 场景

## 5. 风险

| ID | 风险 | 缓解 |
|---|---|---|
| R1 | AvaloniaEdit macOS 键盘快捷键与系统冲突 | 真撞车了改编辑器快捷键，不硬对齐原版 |
| R2 | 流式绑定高频触发导致 UI 卡 | 16ms 批量 flush；实在不行降级整块替换 |
| R3 | portable service 某处缺 API / 行为偏差 | 该改回 portable 层就去改（在 `src/Tianming.AI`/`ProjectData` 补） |
| R4 | `CategoryDataPageView` 参数化覆盖不了某一页的特例 | 该页单独写 view，不硬塞共享控件 |

## 6. 验收

1. `dotnet build Tianming.MacMigration.sln` → 0 Error
2. VM 单测全过
3. 全流程手工走通：新建项目 → 填 5 大规则 → 写大纲分卷章节 → 生成章节 → 编辑器里修改 → 保存
4. 对话面板 Ask 模式至少能与 OpenAI-compatible endpoint 完整对话 1 次
5. API Key 在 `ai.keys` 页填入能保存（M5 改接 Keychain 前可明文 JSON）
6. M6 预留入口可见但不误导：未实现能力显示“未启用/待 M6”，不出现假可用按钮
7. 你自己用完一章从头到尾顺 — 这个最重要

完成后进入 M5（Keychain + 系统代理）。

## 7. Mac_UI 视觉真值源

M4 6 个子里程碑对应参考图：

| 子里程碑 | 参考图 | 伪代码 |
|---|---|---|
| M4.1 设计模块（5 页 schema-driven）| `Mac_UI/images/03-design-module-data-editor.png` | `Mac_UI/pseudocode/03-design-module-data-editor.md` |
| M4.2 生成规划（大纲 / 分卷 / 章节 / 内容配置）| `Mac_UI/images/04-generation-planning.png` | `Mac_UI/pseudocode/04-generation-planning.md` |
| M4.3 章节编辑器 | `Mac_UI/images/05-chapter-markdown-editor.png` | `Mac_UI/pseudocode/05-chapter-markdown-editor.md` |
| M4.4 章节生成管道（M6 实装 M6.2/M6.3）| `Mac_UI/images/06-chapter-generation-pipeline.png` | `Mac_UI/pseudocode/06-chapter-generation-pipeline.md` |
| M4.5 AI 对话面板 | `Mac_UI/images/07-ai-conversation-panel.png` | `Mac_UI/pseudocode/07-ai-conversation-panel.md` |
| M4.6 AI 管理 / Key / 用量 | `Mac_UI/images/09-ai-models-api-key-usage.png` | `Mac_UI/pseudocode/09-ai-models-api-key-usage.md` |

每个子里程碑开工前用 superpowers:writing-plans 把 plan 细化到 step level 时，**首先打开对应 pseudocode 文件**，把 `state` / `services` / `commands` / `render` 各小节作为接线 + UI 结构的权威输入。

