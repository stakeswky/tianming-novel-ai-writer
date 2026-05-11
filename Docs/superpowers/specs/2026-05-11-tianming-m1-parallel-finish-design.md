# 天命 macOS 迁移 — M1 剩余并行收尾设计

日期：2026-05-11
分支：`m1/parallel-finish-2026-05-11`
依据：`Docs/macOS迁移/M0-环境与阻塞基线.md`、`Docs/macOS迁移/M0-服务层抽离候选清单.md`、`Docs/macOS迁移/功能对齐矩阵.md`、`Docs/macOS迁移/功能对齐审计与迁移设计草案.md`

## 1. 范围与边界

### 1.1 纳入 M1 收尾

- **A. ProjectData/Tracking 9 个状态服务**：CharacterStateService、FactionStateService、LocationStateService、ItemStateService、ForeshadowingStatusService、ConflictProgressService、RelationStrengthService、TimelineService、LedgerTrimService。
- **B. AI 多协议 ChatClient**：Anthropic Messages API、Gemini GenerateContent、Azure OpenAI。自建 HTTP adapter，实现 SK `IChatCompletionService`。
- **C. SK 编排层**：WriterPlugin / DataLookupPlugin / SystemPlugin / LayeredPromptBuilder / ChapterDiffContext / StructuredMemoryExtractor / AutoRewriteEngine / SKChatService / NovelAgent / Agents Providers&Wrappers / ChatModeSettings / PlanModeFilter / GenerationProgressHub / UIMessageItem / Chunk 残留。
- **D. 精筛后的模块业务服务**：5 大规则 Portable 包装、大纲/分卷/章节/蓝图/ContentConfig、智能拆书核心（剔除 WebView2）、Progressive/Global 摘要残留逻辑、ChapterRepair 真实 WriterPlugin 接线。

### 1.2 不在 M1 范围

- Avalonia UI 全部页面 / 控件 / XAML（M3）
- WebView2 爬虫替换（M2 决策）
- macOS 系统集成 LaunchAgent / URL Protocol / 文件关联实际接线（M5）
- 打包签名公证（M7）
- ONNX 真向量（HashingTextEmbedder 已占位，真模型验证留 M2）
- TMProtect 真实证书 pin 值（M2）

### 1.3 决策记录

| 编号 | 决策 |
|---|---|
| Q1 | M1 含 SK 编排层 |
| Q2 | SK 仅做 Chat/Plugin/Function-calling，Memory/Embedding 接入已端口的 `FileVectorSearchService` + `HashingTextEmbedder` + `ChatHistoryCompressionService` |
| Q3 | csproj glob 改造 + 共享 workspace 并行（不走 worktree） |
| Q4 | D 块仅做"未被现有 portable 覆盖"的核心，主代理 Wave 0 精筛 |
| Q5 | 各 agent 不 commit，主代理统一 sln test、分批 commit |
| Q6 | 多协议自建 HTTP adapter，不引 SK alpha connector 包 |

## 2. 架构改造

### 2.1 csproj glob 改造

3 个 src/ csproj 与 3 个 tests/ csproj 现状：`EnableDefaultCompileItems=false` + 全显式 `<Compile Include>`。

改造做法：
- 移除 `EnableDefaultCompileItems=false`（默认 SDK glob 生效）
- 删除"自家 `src/<lib>/` 下 .cs 文件"的显式 Include 行（共 ~60 行）
- **保留**跨目录复用原项目 .cs 的 `<Compile Include="../../Framework/..." Link="..." />`
- 显式 `<Compile Remove>` 排除以防 glob 误吸：`../../Core/**`、`../../Framework/**`、`../../Modules/**`、`../../Services/**`、`../../Storage/**`

预审跨目录引用：主代理在 Wave 0 扫一遍 Wave 1/2 各 agent 计划使用的原项目 .cs，一次性加齐到 csproj 显式 Include，避免并行阶段碰 csproj。

### 2.2 SK 接入形态

```
SK Kernel
├── IChatCompletionService
│   ├── OpenAICompatibleChatClient    （已端口）
│   ├── AnthropicChatClient            （B1 新增）
│   ├── GeminiChatClient               （B2 新增）
│   └── AzureOpenAIChatClient          （B3 新增）
├── KernelPlugins
│   ├── WriterPlugin                   （C3 新增，委托到 ProjectData portable）
│   ├── DataLookupPlugin               （C3 新增）
│   ├── SystemPlugin                   （C3 新增）
│   └── NovelAgent 内置 Plugin         （C6 新增）
└── Memory（不用 SK Memory）
    ├── FileVectorSearchService        （已端口）
    ├── ChatHistoryCompressionService  （已端口）
    └── FileSessionStore               （已端口）
```

NuGet：仅 `Microsoft.SemanticKernel` 主包；不引 `Connectors.Anthropic|Google|HuggingFace`、不引 `KernelMemory`。

### 2.3 目录结构

新增子目录：
- `src/Tianming.ProjectData/Tracking/States/`
- `src/Tianming.AI/Providers/`
- `src/Tianming.AI/SemanticKernel/Orchestration/`
- `src/Tianming.AI/SemanticKernel/Plugins/`
- `src/Tianming.AI/SemanticKernel/Agents/`
- `src/Tianming.AI/SemanticKernel/Rewrite/`
- `src/Tianming.ProjectData/Modules/Design/`
- `src/Tianming.ProjectData/Modules/Generate/`
- `src/Tianming.ProjectData/Modules/Analysis/`
- `src/Tianming.ProjectData/Modules/Summary/`
- `src/Tianming.ProjectData/Modules/Repair/`

测试对称镜像在 `tests/<lib>.Tests/<Subdir>/`。

## 3. 工作拆分

### 3.1 Wave 1（12 个 agent，可并行）

| ID | 范围 | 主要新增 |
|---|---|---|
| **A1** | Tracking 9 个状态服务一气呵成 | `Tracking/States/CharacterStateService.cs`、`FactionStateService.cs`、`LocationStateService.cs`、`ItemStateService.cs`、`ForeshadowingStatusService.cs`、`ConflictProgressService.cs`、`RelationStrengthService.cs`、`TimelineService.cs`、`LedgerTrimService.cs` + 9 个测试 |
| **B1** | Anthropic Messages API ChatClient | `Providers/AnthropicChatClient.cs`（IChatCompletionService + tool_use + stream）+ 测试 |
| **B2** | Gemini GenerateContent ChatClient | `Providers/GeminiChatClient.cs` + 测试 |
| **B3** | Azure OpenAI ChatClient | `Providers/AzureOpenAIChatClient.cs`（deployment-id 路由 + AAD/key 鉴权）+ 测试 |
| **C1** | SK 配置类与 Plan 过滤器 | `Orchestration/ChatModeSettings.cs`、`PlanModeFilter.cs` + 测试 |
| **C2** | 纯逻辑工具 | `Rewrite/LayeredPromptBuilder.cs`、`Rewrite/ChapterDiffContext.cs`、`Orchestration/StructuredMemoryExtractor.cs` + 测试 |
| **C3** | SK Plugins | `Plugins/WriterPlugin.cs`、`DataLookupPlugin.cs`、`SystemPlugin.cs`（KernelFunction 装饰）+ 测试 |
| **C4** | AutoRewriteEngine | `Rewrite/AutoRewriteEngine.cs`（CHANGES diff 应用 + AI 重写控制，依赖 `IChatRewriteClient`）+ 测试 |
| **C7** | 进度/消息模型 | `Orchestration/GenerationProgressHub.cs`、`UIMessageItem.cs`、Chunk 残留模型 + 测试 |
| **D1** | 设计/规则 portable 包装 | `Modules/Design/{Character,Faction,Location,Plot,World}RulesService.cs` + `CreativeMaterialsService.cs`（基于 `FileModuleDataStore` 薄壳）+ 测试 |
| **D2** | 生成/规划 portable 包装 | `Modules/Generate/{Outline,VolumeDesign,Chapter,Blueprint,ContentConfig}Service.cs` + 测试 |
| **D3** | 智能拆书核心（去 WebView2） | `Modules/Analysis/BookAnalysisService.cs`、`EssenceChapterSelectionService.cs`（剥离爬虫，留 `IBookSourceFetcher` placeholder）+ 测试 |
| **D4** | 摘要/残留服务 | `Modules/Summary/ProgressiveSummaryService.cs`、`GlobalSummaryService.cs`、`GeneratedContentService` 残留逻辑 + 测试 |

### 3.2 Wave 2（3 个 agent，依赖 Wave 1）

| ID | 依赖 | 范围 | 主要新增 |
|---|---|---|---|
| **C5** | B1/B2/B3/C3/C4 | SKChatService 主对话循环 | `Orchestration/SKChatService.cs`（SK Kernel 装配 + IChatCompletionService 注入 + Plugin 注册 + Memory 通过 FileVectorSearchService 注入 + ChatHistoryCompressionService 接线）+ 测试 |
| **C6** | C5 | NovelAgent + Providers/Wrappers | `Agents/NovelAgent.cs`、`Agents/Providers/{NovelMemoryProvider,RAGContextProvider}.cs`、`Agents/Wrappers/*.cs` + 测试 |
| **D5** | C3/C5 | ChapterRepairService 真实接线 | `Modules/Repair/ChapterRepairService.cs`（对接 `PortableChapterRepairService` + WriterPlugin + AutoRewriteEngine）+ 测试 |

## 4. 执行编排

### 4.1 Wave 0（主代理串行，~10 分钟）

1. 6 个 csproj 改造（src + tests）：移除 `EnableDefaultCompileItems=false`，删自家目录显式 Compile Include，加 `<Compile Remove>` 排除原项目目录
2. 预审跨目录引用 → 一次性加齐
3. 主包加 NuGet 引用 `Microsoft.SemanticKernel`（最新稳定版）到 `Tianming.AI.csproj`
4. 基线测试：`dotnet test Tianming.MacMigration.sln -v minimal` 必须保持 1132 全过、0 警告 0 错误
5. 建分支 `m1/parallel-finish-2026-05-11`
6. Commit Wave 0 改造

### 4.2 Wave 1（主代理同消息内派发 12 agents 并行）

主代理在单条消息内做 12 次 `Agent` tool call（subagent_type=general-purpose），等待全部返回。

### 4.3 Wave 1 合流（主代理串行）

1. 逐 agent review 文件清单 → 扫违例
2. `git status --short` + `git diff --stat` 检查是否有意外既有文件改动
3. `dotnet build Tianming.MacMigration.sln` → 0 错
4. `dotnet test Tianming.MacMigration.sln -v minimal` → 全过
5. 任一失败：针对性 fix（直接改 or 派单点修复 agent）
6. 分 4 个 commit：`feat(tracking): port 9 state services`、`feat(ai): anthropic/gemini/azure clients`、`feat(ai-sk): orchestration foundation`、`feat(modules): portable wrappers`

### 4.4 Wave 2（分两次派发，因 C6 依赖 C5）

依赖关系：D5 仅依赖 C3（Wave 1 已合流），C5 仅依赖 B/C3/C4（Wave 1 已合流），C6 依赖 C5。

- **Wave 2.0**（主代理同消息内派发 2 agents 并行）：C5、D5
- **Wave 2.0 合流**（主代理串行）：build + test + commit
- **Wave 2.1**（主代理单独派发 1 agent）：C6

### 4.5 Wave 2 合流（主代理串行，Wave 2.0 ~10 分钟 + Wave 2.1 ~10 分钟）

Wave 2.0 合流：同 Wave 1 流程，2 个 commit：`feat(ai-sk): orchestration runtime`（C5）、`feat(modules): chapter repair wiring`（D5）。

Wave 2.1 合流：同 Wave 1 流程，1 个 commit：`feat(ai-sk): novel agent`（C6）。

### 4.6 最终收尾（主代理，~30 分钟）

1. `Scripts/check-windows-bindings.sh` 扫 `src/` + `tests/` 确认 0 命中
2. 更新 `Docs/macOS迁移/M0-环境与阻塞基线.md`：追加"第六/第七验证点"小节
3. 更新 `Docs/macOS迁移/功能对齐矩阵.md`：把对应行的"仍需迁移"改为"已端口"
4. 最终 commit：`docs(macos): M1 收尾验证点`
5. push 分支

## 5. 验收门禁

### 5.1 Agent 硬门禁（每个 agent 必满足）

1. 不出现 `using System.Windows`、`using TM.App`、`using NAudio`、`System.Speech`、`Microsoft.Web.WebView2`、`System.Management`、`ProtectedData`、注册表 P/Invoke
2. 不引 SK alpha connector 包
3. `dotnet test tests/<lib>.Tests/<lib>.Tests.csproj -v minimal` 全过
4. 只新增文件，不动既有文件，不动 csproj/sln/global.json
5. 不 commit / 不 push

### 5.2 主代理合流门禁

1. `dotnet build Tianming.MacMigration.sln` → 0 Warning / 0 Error
2. `dotnet test Tianming.MacMigration.sln -v minimal` → 全过
3. `Scripts/check-windows-bindings.sh src/ tests/` → 0 命中
4. 任一 agent 失败先 fix 再合流，不允许带失败合并

### 5.3 Agent prompt 模板

```
你是 Tianming macOS 迁移 M1 收尾的 <ID> agent。
仓库：/Users/jimmy/Downloads/tianming-novel-ai-writer
分支：m1/parallel-finish-2026-05-11（已切好）

任务范围：<具体描述>
原版源文件：<具体路径列表>
新增目标位置：<具体路径列表>

硬约束：
- 只新增文件，不动既有文件，不动 csproj/sln，不 commit
- 不引 SK alpha connector，不引 WPF/Win32/TM.App
- 必须新增对称测试，跑 `dotnet test tests/<lib>.Tests/...` 全过

接口契约：<列出本 agent 需对接的现有 portable 接口/类>

汇报要求：
- 新增文件清单（完整相对路径）
- 测试通过日志 tail（最后 10 行）
- <100 字设计取舍说明
```

## 6. 风险与回滚

### 6.1 主要风险

| ID | 风险 | 影响 | 缓解 |
|---|---|---|---|
| R1 | csproj glob 误吸跨目录原项目 .cs | 编译失败 / Windows 绑定泄露 | `<Compile Remove>` 排除全部 `../../<原项目目录>/**`；Wave 0 基线测试发现立即回滚 |
| R2 | SK 主包引入破坏既有 1132 测试基线 | 既有验证点回归 | Wave 0 加包后立即跑全量 sln test，基线必须保持才进 Wave 1 |
| R3 | 12 并行 agent 同名文件 / 类名冲突 | 编译冲突 | 每 agent 限定到不同 `<Subdir>`，类名按文件路径前缀消歧；Wave 0 主代理预建空目录 |
| R4 | agent 误改既有文件 | 合流时发现意外 diff | 主代理合流第一步 `git status --short` + `git diff --stat`，发现意外改动 revert 该 hunk 主代理手做 |
| R5 | SK 2.x API 与原版不兼容 | C5 卡 → C6/D5 连锁 | Wave 2 派发前主代理跑最小 SK Kernel demo 验证；不兼容则降级 C5 仅接 OpenAI-compatible，多协议接入推 M2 |
| R6 | C4 依赖 SKChatService 形成鸡蛋互锁 | C4 与 C5 互相等待 | 抽 `IChatRewriteClient` 接口；C4 仅依赖接口，C5 完成后做适配器接入；Wave 1 C4 用 fake 实现做测试 |
| R7 | D3 智能拆书剥离 WebView2 后核心解析不完整 | macOS v1 智能拆书受限 | D3 仅端口"已有 HTML/文本的解析"逻辑；爬虫层留 `IBookSourceFetcher` placeholder，M2 决策替换方案 |

### 6.2 回滚策略

- 全部工作在独立分支 `m1/parallel-finish-2026-05-11`，最坏 `git checkout main && git branch -D` 即可全弃
- 每个 Wave 单独 commit 组，Wave 1 失败不影响 Wave 0 csproj 改造保留
- 文档更新放最后一个 commit，便于若发现回归先 revert 文档 commit 再 revert 代码

### 6.3 退路

- 若 Wave 2 卡死：Wave 1 单独可宣告完成，C5/C6/D5 拆出留 M1.6 次循环
- 若 R5 发生：降级 C5 仅 OpenAI-compatible，B 块多协议 ChatClient 已独立可用（B1/B2/B3 仍合流），M1 仍可关闭

## 7. 验收标准

M1 收尾完成的判据：

1. `dotnet build Tianming.MacMigration.sln` → 0 Warning / 0 Error
2. `dotnet test Tianming.MacMigration.sln -v minimal` → 全过（预计较 Wave 0 基线增加 80+ 测试用例）
3. `Scripts/check-windows-bindings.sh src/ tests/` → 0 命中
4. `Docs/macOS迁移/M0-环境与阻塞基线.md` 包含第六/第七验证点小节
5. `Docs/macOS迁移/功能对齐矩阵.md` 中本设计涵盖的行状态更新为"已端口"
6. 分支 `m1/parallel-finish-2026-05-11` 已 push 到远端

完成后下一步进入 M2 决策（WebView2 替换 / TMProtect 真实 pin / SK 2.x API 适配验收 / ONNX 真向量验证）与 M3（Avalonia UI 启动）。
