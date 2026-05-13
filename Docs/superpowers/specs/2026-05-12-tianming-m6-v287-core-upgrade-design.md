# 天命 macOS 迁移 — M6 v2.8.7 写作内核升级设计（自用版）

日期：2026-05-12
分支：`m2-m6/specs-2026-05-12`
依据：`Docs/superpowers/specs/2026-05-12-tianming-m5-macos-platform-design.md`、`v2.8.7-升级说明.txt`
定位：**个人自用**，M5 后的业务内核增强，不阻塞 M3-M5 可用闭环

## 1. 范围与边界

### 1.1 纳入 M6

M6 只做 v2.8.7 对“写长篇更稳、更像人、更自动、Agent 能动手”的核心价值，不恢复被 M5 砍掉的服务端/分发型能力。

- **A. 追踪系统升级**：实体漂移、实体遗漏、Deadline、Pledge、SecretReveal；`FactSnapshot` 从单体抽取流程拆为专项提取器，支持设计数据变更后重建相关章节快照。
- **B. 生成质量层**：HumanizeRules 本地规则、在线润色、专用润色 API 配置、字数偏差检测、超长分段重试、CHANGES 保留。
- **C. CHANGES Canonicalizer**：归一化 9 类变更声明，修正重要性，保证润色和重写不破坏协议。
- **D. 校验系统升级**：分层抽样、正文描述校验、正文实体提取、向量定位、校验子系统拆分。
- **E. 一键成书与断点续跑**：模板 → 世界 → 角色 → 势力 → 位置 → 剧情 → 大纲 → 分卷 → 章节 → 蓝图的 10 步管线，支持前置缺失补全、短篇蓝图、内容提炼工坊。
- **F. 长篇稳定性**：章节变更 WAL、生成中断恢复、里程碑压缩、卷级事实归档、关键事件存储、多层语义索引。
- **G. ContextService 拆分**：按设计、生成、校验、完整上下文、打包、内容检查、数据聚合等任务类型分层组装上下文。
- **H. AI 调度层升级**：错误归一化、Key 轮换、重试降级、thinking 提取、用量统计、链路追踪、多模型任务路由、模型能力探测。
- **I. Agent 插件升级**：拆分 WriterPlugin / LayeredPromptBuilder / AutoRewriteEngine，新增 WorkspacePlugin / DataEditPlugin / ContentEditPlugin，所有写入类工具调用必须经过“待确认变更”。
- **J. 数据打包与备份入口**：打包前预检、完整性扫描、报告、模块启用禁用、章节导出 ZIP、全量备份和恢复。

### 1.2 不做

- 登录/注册/OAuth/订阅/heartbeat/功能授权/SSL pinning/自动更新等服务端商业化能力。
- 公开分发、签名、公证、hardened runtime、安装包渠道。
- 大而全的偏好中心；字体/通知/日志等 M1 已端口能力只在 M6 需要时接最小入口。
- 多 agent 并行编排 UI；M6 的 Agent 是写作工程工具调用，不是开发流程编排。

### 1.3 决策

| 编号 | 决策 |
|---|---|
| Q1 | M6 先补内核，再补漂亮 UI；M4 的入口够用即可 |
| Q2 | 所有新能力优先写在 `src/Tianming.ProjectData` / `src/Tianming.AI` portable 层，Avalonia 只做绑定 |
| Q3 | 先稳定章节生成链路，再做一键成书；不要先写全书自动化把故障面放大 |
| Q4 | Agent 所有写文件/改数据/改正文动作默认进入 staged changes，由用户确认后应用 |
| Q5 | 多模型路由按任务类型分：Chat / Writing / Polish / Validation / Embedding |
| Q6 | WAL 只保护章节正文、CHANGES、追踪派生数据三类关键写入，先不做通用事务系统 |

## 2. 架构拆分

### 2.1 ProjectData

```
src/Tianming.ProjectData/
├── Tracking/
│   ├── Extractors/                 （FactSnapshot 9 个专项提取器）
│   ├── Debt/                       （EntityDrift / Omission / Deadline / Pledge / SecretReveal）
│   └── Rebuild/                    （设计数据变更 → 快照重建订阅）
├── Generation/
│   ├── Humanize/                   （本地规则 + 在线润色编排）
│   ├── ChangesCanonicalization/    （CHANGES 归一化）
│   ├── Wal/                        （章节写入预写日志）
│   └── OneClick/                   （10 步一键成书状态机）
├── Validation/
│   ├── Sampling/
│   ├── RuleChecks/
│   ├── VectorLocate/
│   └── Reports/
├── Context/
│   ├── DesignContextService.cs
│   ├── GenerationContextService.cs
│   ├── ValidationContextService.cs
│   ├── PackagingContextService.cs
│   └── ContextSliceAggregator.cs
└── Backup/
    ├── ChapterExportService.cs
    ├── FullBackupService.cs
    └── RestorePlanner.cs
```

### 2.2 AI

```
src/Tianming.AI/
├── Middleware/
│   ├── ErrorNormalizationMiddleware.cs
│   ├── KeyRotationMiddleware.cs
│   ├── RetryFallbackMiddleware.cs
│   ├── ThinkingExtractionMiddleware.cs
│   ├── UsageStatisticsMiddleware.cs
│   └── TraceMiddleware.cs
├── Routing/
│   ├── TaskModelRouter.cs
│   ├── ModelCapabilityProbe.cs
│   └── ModelCapabilityStore.cs
└── SemanticKernel/Plugins/
    ├── Writer/
    ├── WorkspacePlugin.cs
    ├── DataEditPlugin.cs
    └── ContentEditPlugin.cs
```

### 2.3 Avalonia 接线

Avalonia 只消费 M4 已预留的入口：

- `ChapterPipelinePage` 显示润色、CHANGES 归一化、WAL 恢复状态。
- `ValidationIssueList` 显示抽样范围、向量定位章节、问题严重度。
- `ConversationPanelView` 的 `ToolCallCard` 展示 staged changes 和确认/拒绝。
- `ai.models` 页面把任务路由配置接到 `TaskModelRouter`。
- `generate.oneclick` 页面显示 10 步流水线、当前 step、可恢复 checkpoint。

## 3. 工作拆分

### 3.0 开工门槛与总顺序

M6 开始前必须满足：

1. M3 shell 已能启动并完成项目打开/新建、导航、主题、窗口状态持久化。
2. M4 创作闭环已可手工走通：设计数据 → 章节规划 → 章节生成 → 编辑保存 → AI 对话。
3. M5 Keychain / 系统代理 / 主题跟随已接入，AI 请求能在 macOS 自用环境稳定跑通。
4. `dotnet test Tianming.MacMigration.sln` 全过，作为 M6 改内核前的行为基线。

M6 按“先保护写作链路，再做自动化”的顺序推进：

| 顺序 | 里程碑 | 主目标 | 依赖 |
|---|---|---|---|
| 1 | M6.1 Tracking 债务与 FactSnapshot 拆分 | 让剧情债务可建模、可提取、可重建 | M4.4 FactSnapshot 入口 |
| 2 | M6.2 HumanizeRules + CHANGES Canonicalizer | 让生成结果更像人，同时不破坏变更协议 | M6.1 的债务模型 |
| 3 | M6.3 WAL + 生成恢复 | 保护章节写入和派生数据，不怕中断 | M6.2 的 canonical output |
| 4 | M6.4 校验分层 + 向量定位 | 让问题能抽样发现并定位到章节/段落 | M6.1 债务、M6.3 派生索引 |
| 5 | M6.5 ContextService 拆分 | 降低上下文污染，给一键成书铺路 | M6.1-M6.4 |
| 6 | M6.6 AI middleware + 多模型路由 | 让不同任务走不同模型并可降级 | M4.6 AI 管理入口 |
| 7 | M6.7 Agent 插件写工程能力 | 让 Agent 能读写工程但必须确认 | M6.3 WAL、M6.6 路由 |
| 8 | M6.8 一键成书 + 内容提炼 | 把前面能力串成自动流水线 | M6.1-M6.7 |
| 9 | M6.9 打包预检 + 备份 | 交付可迁移、可恢复、可报告的数据包 | M6.8 |

每个里程碑都按同一节奏推进：先加模型/接口和测试，再接现有 facade，最后接 M4 预留 UI 状态。中途不要删除旧 facade，直到所有旧测试和新测试都通过。

### M6.1 Tracking 债务与 FactSnapshot 拆分（~2 天）

目标：把“事实快照”从一次性大提取，升级为可解释的专项提取 + 剧情债务登记。

改动面：

- `src/Tianming.ProjectData/Tracking/Debt/`
  - `TrackingDebt.cs`
  - `EntityDriftDebt.cs`
  - `EntityOmissionDebt.cs`
  - `DeadlineDebt.cs`
  - `PledgeDebt.cs`
  - `SecretRevealDebt.cs`
  - `TrackingDebtSeverity.cs`
- `src/Tianming.ProjectData/Tracking/Extractors/`
  - `IFactSnapshotSectionExtractor.cs`
  - `CharacterStateSnapshotExtractor.cs`
  - `ConflictProgressSnapshotExtractor.cs`
  - `ForeshadowingSnapshotExtractor.cs`
  - `LocationStateSnapshotExtractor.cs`
  - `FactionStateSnapshotExtractor.cs`
  - `TimelineSnapshotExtractor.cs`
  - `ItemStateSnapshotExtractor.cs`
  - `PlotPointSnapshotExtractor.cs`
  - `DesignDescriptionSnapshotExtractor.cs`
- `src/Tianming.ProjectData/Tracking/Rebuild/`
  - `FactSnapshotRebuildPlanner.cs`
  - `FactSnapshotRebuildPlan.cs`
  - `DesignChangeImpactAnalyzer.cs`

步骤：

1. 先新增 debt 模型和 JSON 持久化契约，不接现有 pipeline。
2. 给 `PortableFactSnapshotExtractor` 增加内部组合模式，旧 public API 保持不变。
3. 把现有抽取逻辑一点点搬到 9 个 section extractor；每搬一块跑对应旧测试。
4. 增加 `TrackingDebtDetector`，先基于快照与 CHANGES 做保守规则，不调用 AI。
5. 增加 `FactSnapshotRebuildPlanner`，输入设计数据变更，输出受影响章节和原因，第一版只产出 plan。
6. M4.4 的 `FactSnapshotView` 只需要能显示 debt summary，不要求可编辑。

测试：

- `TrackingDebtTests`：每类 debt 的创建、序列化、严重度排序。
- `FactSnapshotSectionExtractorTests`：9 个 extractor 的边界输入。
- `PortableFactSnapshotExtractorTests`：旧测试全部保留，验证 facade 行为不漂移。
- `FactSnapshotRebuildPlannerTests`：角色/地点/剧情变更能定位受影响章节。

退出标准：

- 旧 `PortableFactSnapshotExtractor` 调用方无需改动。
- debt summary 能从章节生成结果里读到。
- rebuild plan 可以解释“为什么这些章节需要重建快照”。

Commit：`feat(core): add tracking debt extractors`。

### M6.2 HumanizeRules + CHANGES Canonicalizer（~2 天）

目标：在不破坏 CHANGES 协议的前提下，给正文加一层“本地快修 + 在线精修”。

改动面：

- `src/Tianming.ProjectData/Generation/Humanize/`
  - `HumanizeRule.cs`
  - `HumanizeRuleResult.cs`
  - `HumanizeRulePipeline.cs`
  - `FillerWordRule.cs`
  - `TranslationeseRule.cs`
  - `DialoguePunctuationRule.cs`
  - `NumberFormatRule.cs`
  - `OverModifierRule.cs`
  - `ArtificialPhraseRule.cs`
  - `PolishOrchestrator.cs`
  - `PolishLengthGuard.cs`
- `src/Tianming.ProjectData/Generation/ChangesCanonicalization/`
  - `ChangesCanonicalizer.cs`
  - `ChangesImportanceNormalizer.cs`
  - `CanonicalChangesResult.cs`

步骤：

1. 从 `ChangesProtocolParser` 的现有解析结果出发，先写 `ChangesCanonicalizer`，保证所有 CHANGES 都能输出稳定格式。
2. 增加本地 HumanizeRules，第一版只做确定性替换和告警，不做激进改写。
3. 增加 `PolishOrchestrator`，支持 LocalOnly、OnlineOnly、LocalThenOnline 三种模式。
4. 在线润色只通过抽象 `IPolishTextGenerator`，后续由 M6.6 路由到 Polish 模型。
5. `PolishLengthGuard` 检测润色前后字数偏差，超过阈值返回 warning 或要求重试。
6. 把 `ContentGenerationPreparer` 的输出改为：parse CHANGES → canonicalize → humanize body → canonicalize again → gate。

测试：

- `ChangesCanonicalizerTests`：乱序、别名、缺字段、重要性修正。
- `HumanizeRulePipelineTests`：填充词、翻译腔、引号、数字、过度修饰。
- `PolishOrchestratorTests`：本地-only、在线 fallback、超长分段、字数偏差。
- `ContentGenerationPreparerTests`：润色后 CHANGES 仍能解析，门禁仍生效。

退出标准：

- 润色不能删除或污染 CHANGES。
- 本地规则失败不阻塞章节保存，只输出 warning。
- 在线润色未配置时自动降级，不影响原生成闭环。

Commit：`feat(core): add humanize and changes canonicalizer`。

### M6.3 WAL + 生成恢复（~1.5 天）

目标：保护章节正文、CHANGES 和派生索引写入，避免生成中断丢状态。

改动面：

- `src/Tianming.ProjectData/Generation/Wal/`
  - `ChapterWriteAheadLog.cs`
  - `ChapterWalEntry.cs`
  - `ChapterWalStore.cs`
  - `ChapterWalRecoveryService.cs`
  - `ChapterWalApplyResult.cs`
- `src/Tianming.ProjectData/Generation/ChapterGenerationPipeline.cs`
- `src/Tianming.ProjectData/Generation/ChapterContentStore.cs`

步骤：

1. 定义 WAL 存储路径：`ProjectData/Generation/Wal/{chapterId}/{generationId}.json`。
2. 保存前写入 `Pending` entry，包含 raw content、canonical content、parsed changes、fact snapshot hash、derived index plan。
3. 章节文件保存成功后标记 `ContentSaved`，派生数据写入成功后标记 `Committed`。
4. `ChapterWalRecoveryService.ScanAsync` 返回可恢复、已提交可清理、损坏需隔离三类结果。
5. `ApplyAsync` 必须幂等：同一个 `generationId` 只能应用一次，重复调用返回 already applied。
6. M4.4 pipeline 页显示“发现可恢复生成”，提供恢复/丢弃按钮。

测试：

- `ChapterWalStoreTests`：原子写入、坏 JSON 隔离、状态迁移。
- `ChapterWalRecoveryServiceTests`：Pending、ContentSaved、Committed 三状态扫描。
- `ChapterGenerationPipelineWalTests`：保存前写 WAL、保存后提交、派生索引失败仍可恢复。
- `ChapterContentStoreTests`：重复应用不覆盖更新版本。

退出标准：

- 模拟保存过程中抛异常，重启后能从 WAL 恢复。
- 损坏 WAL 不阻塞正常生成。
- committed WAL 可安全清理。

Commit：`feat(core): protect chapter writes with wal`。

### M6.4 校验分层 + 向量定位（~2 天）

目标：把校验从“跑全量”升级为“先找高风险，再定位证据”。

改动面：

- `src/Tianming.ProjectData/Validation/Sampling/`
  - `ValidationSamplingStrategy.cs`
  - `RiskBasedChapterSampler.cs`
  - `ValidationRiskSignal.cs`
- `src/Tianming.ProjectData/Validation/RuleChecks/`
  - `ContentDescriptionRuleChecker.cs`
  - `TrackingDebtRuleChecker.cs`
  - `DeadlineRuleChecker.cs`
- `src/Tianming.ProjectData/Validation/VectorLocate/`
  - `ValidationVectorLocator.cs`
  - `ValidationVectorHit.cs`
  - `ValidationEvidenceRange.cs`
- `src/Tianming.ProjectData/Validation/Reports/`
  - `LayeredValidationReportBuilder.cs`

步骤：

1. 保留 `PortableUnifiedValidationService` facade，内部新增 coordinator。
2. `RiskBasedChapterSampler` 读取近期改动、tracking debt、关键事件、随机兜底四类信号。
3. 规则检查先覆盖正文描述矛盾、实体遗漏、Deadline 过期、Pledge 违背。
4. `ValidationVectorLocator` 接现有 `FileVectorSearchService` / `FileVectorChapterIndexAdapter`，输出章节和片段摘要。
5. 报告结构增加：sample reason、evidence hits、repair hint、confidence。
6. M4 的 `ValidationIssueList` 展示 issue、命中章节、证据摘要。

测试：

- `RiskBasedChapterSamplerTests`：固定随机种子，验证高风险章节优先。
- `TrackingDebtRuleCheckerTests`：Debt 转 issue。
- `ValidationVectorLocatorTests`：fake vector index 命中排序。
- `PortableUnifiedValidationServiceTests`：旧 facade 输出兼容。

退出标准：

- 可以只校验一个卷的抽样章节，而不是必须全书。
- 每个高危 issue 都至少带一个来源：规则、debt 或 vector hit。
- 无 vector index 时降级为规则校验，不失败。

Commit：`feat(core): split validation with vector locating`。

### M6.5 ContextService 拆分（~2 天）

目标：把上下文从“大包塞给 AI”拆成按任务读取的切片服务。

改动面：

- `src/Tianming.ProjectData/Context/`
  - `IContextSliceService.cs`
  - `ContextSlice.cs`
  - `DesignContextService.cs`
  - `GenerationContextService.cs`
  - `ValidationContextService.cs`
  - `PackagingContextService.cs`
  - `ContentInspectionContextService.cs`
  - `ContextSliceAggregator.cs`
  - `ContextBudgetPlanner.cs`

步骤：

1. 先定义 `ContextTaskType`：Design、ChapterGeneration、Polish、Validation、Repair、Packaging、AgentRead、AgentEdit。
2. 给每类 task 定义默认上下文预算和优先级。
3. 从现有 prompt/context resolver 中抽公共读取逻辑，先让旧调用走新 aggregator。
4. `ContextBudgetPlanner` 根据 token budget 做裁剪，优先保留硬规则、FactSnapshot、TrackingDebt、当前章节蓝图。
5. 加入污染防护：当前任务不需要的模块不进入上下文。

测试：

- `ContextSliceAggregatorTests`：不同 task type 选择不同 slice。
- `ContextBudgetPlannerTests`：超预算时按优先级裁剪。
- `ValidationContextPromptContextResolverTests` / `ChapterValidationPromptContextResolverTests`：旧行为兼容。

退出标准：

- 章节生成上下文不再默认带全量校验/打包数据。
- 校验上下文能读到债务和历史证据。
- 旧 validation/generation prompt 测试继续过。

Commit：`feat(core): split context by task type`。

### M6.6 AI middleware + 多模型路由（~2 天）

目标：让不同任务使用不同模型，并把失败、重试、thinking、统计、trace 做成统一管道。

改动面：

- `src/Tianming.AI/Middleware/`
  - `IAIRequestMiddleware.cs`
  - `AIRequestContext.cs`
  - `AIResponseContext.cs`
  - `ErrorNormalizationMiddleware.cs`
  - `KeyRotationMiddleware.cs`
  - `RetryFallbackMiddleware.cs`
  - `ThinkingExtractionMiddleware.cs`
  - `UsageStatisticsMiddleware.cs`
  - `TraceMiddleware.cs`
- `src/Tianming.AI/Routing/`
  - `AITaskType.cs`
  - `TaskModelRouter.cs`
  - `TaskModelRouteStore.cs`
  - `ModelCapabilityProbe.cs`
  - `ModelCapabilityStore.cs`
- `src/Tianming.AI/Core/ConfiguredAITextGenerationService.cs`

步骤：

1. 增加 `AITaskType` 并把现有请求默认映射为 Chat，保持旧调用可用。
2. `TaskModelRouter` 支持 Chat / Writing / Polish / Validation / Embedding，每类有 active route 和 fallback route。
3. 把 `ApiKeyRotationService` 放入 middleware，不复制轮换逻辑。
4. thinking 提取复用现有 parsing 测试，输出正文和 thinking 两个字段。
5. retry/fallback 只对网络、429、5xx 生效；认证失败不重试同 key。
6. trace 记录 task type、provider、model、latency、retry count、token estimate、error category。

测试：

- `TaskModelRouterTests`：任务路由和 fallback。
- `AIMiddlewarePipelineTests`：middleware 执行顺序。
- `RetryFallbackMiddlewareTests`：429/5xx 降级、401 不重试。
- `ThinkingExtractionMiddlewareTests`：DeepSeek/o1 风格 thinking 剥离。
- `ConfiguredAITextGenerationServiceTests`：旧接口仍可用。

退出标准：

- M4.6 的 Chat / Writing / Polish / Validation 槽位都能落到 route store。
- 未配置任务模型时 fallback 到当前 active model。
- 失败统计和用量统计不因重试重复计错。

Commit：`feat(ai): add task routing middleware`。

### M6.7 Agent 插件写工程能力（~2 天）

目标：让右栏 Agent 不只聊天，还能读工程、提出修改，并在确认后落盘。

改动面：

- `src/Tianming.AI/SemanticKernel/Plugins/Writer/`
  - `WriterGenerationPlugin.cs`
  - `WriterCleanupPlugin.cs`
  - `WriterRecallPlugin.cs`
  - `WriterShortFormPlugin.cs`
  - `WriterVolumeTransitionPlugin.cs`
- `src/Tianming.AI/SemanticKernel/Plugins/WorkspacePlugin.cs`
- `src/Tianming.AI/SemanticKernel/Plugins/DataEditPlugin.cs`
- `src/Tianming.AI/SemanticKernel/Plugins/ContentEditPlugin.cs`
- `src/Tianming.ProjectData/AgentEdits/`
  - `StagedEdit.cs`
  - `StagedEditStore.cs`
  - `StagedEditApplier.cs`
  - `StagedEditDiffBuilder.cs`

步骤：

1. 先引入 staged edit 模型，包含 target path、operation、before hash、after content、risk level、reason。
2. WorkspacePlugin 只允许读项目工作区内文件，并限制最大读取大小。
3. DataEditPlugin 修改设计数据时只生成 staged edit，不直接写 `FileModuleDataStore`。
4. ContentEditPlugin 修改章节时也只生成 staged edit，并附带 diff preview。
5. `StagedEditApplier` 应用前检查 before hash，防止用户已经手动改过。
6. WriterPlugin 拆分时保留旧 facade 或 adapter，避免 M4.5 对话面板断线。
7. `ToolCallCard` 显示 staged edit 数量、风险、diff，并提供确认/拒绝。

测试：

- `WorkspacePluginTests`：路径逃逸、大小限制、只读预览。
- `StagedEditStoreTests`：保存、读取、坏 JSON 恢复。
- `DataEditPluginTests`：生成 staged edit，不落盘。
- `ContentEditPluginTests`：章节 diff、before hash 冲突。
- `StagedEditApplierTests`：确认落盘、拒绝不落盘。

退出标准：

- Agent 不能绕过 staged edit 直接写项目数据。
- 用户拒绝后没有任何项目文件变化。
- 用户确认后能追踪修改来源和理由。

Commit：`feat(agent): stage workspace and content edits`。

### M6.8 一键成书 + 内容提炼（~3 天）

目标：把模板、设定、规划、章节、蓝图等步骤串成可断点续跑的状态机。

改动面：

- `src/Tianming.ProjectData/Generation/OneClick/`
  - `OneClickBookPipeline.cs`
  - `OneClickBookStep.cs`
  - `OneClickBookCheckpointStore.cs`
  - `OneClickBookDependencyChecker.cs`
  - `OneClickBookStepExecutor.cs`
  - `ContentExtractionWorkshop.cs`
  - `ExtractedDesignData.cs`
  - `ShortBlueprintGenerator.cs`

步骤：

1. 定义 10 步状态：Template、World、Character、Faction、Location、Plot、Outline、Volume、Chapter、Blueprint。
2. 每一步只依赖明确输入，缺失时由 `OneClickBookDependencyChecker` 输出补全计划。
3. `OneClickBookCheckpointStore` 每步开始/完成/失败都写 checkpoint。
4. fake AI 下先跑通完整 10 步，真实 AI 只做手工烟测。
5. 内容提炼工坊第一版支持导入文本，输出世界/角色/势力/地点/剧情五类候选数据。
6. 提炼结果先进入 staged design changes，由 M6.7 的确认机制落盘。
7. 短篇蓝图作为独立路径，不要求完整长篇分卷。

测试：

- `OneClickBookPipelineTests`：10 步顺序、失败停止、断点续跑。
- `OneClickBookDependencyCheckerTests`：跳步补全。
- `ContentExtractionWorkshopTests`：文本到结构化候选，依赖缺失报告。
- `ShortBlueprintGeneratorTests`：短篇路径不依赖分卷。

退出标准：

- fake AI 下可以从空项目跑完 10 步并生成 checkpoint。
- 中途失败后重启能从失败步骤继续。
- 提炼结果不会直接覆盖正式数据，必须确认。

Commit：`feat(core): add one-click book pipeline`。

### M6.9 打包预检 + 备份（~1.5 天）

目标：收尾 M6 产物，让项目在写作、打包、迁移、恢复时都能解释状态并避免静默覆盖。

1. 打包前完整性扫描和报告。
2. 模块启用/禁用与引导文件生成。
3. 章节导出 ZIP、全量备份、恢复 plan。
4. 测试：缺失项报告、备份恢复 dry-run、模块禁用不进入 prompt。

改动面：

- `src/Tianming.ProjectData/Packaging/`
  - `PackagePrecheckService.cs`
  - `PackagePrecheckIssue.cs`
  - `PackageReportBuilder.cs`
- `src/Tianming.ProjectData/Backup/`
  - `ChapterExportService.cs`
  - `FullBackupService.cs`
  - `BackupManifest.cs`
  - `RestorePlanner.cs`
  - `RestoreDryRunResult.cs`

步骤：

1. 复用现有 `FilePackageBuilder` / `FilePackageManifestStore` / `FileModuleEnabledStore`，不要重写打包核心。
2. precheck 扫描缺失设计数据、空章节、未提交 WAL、未确认 staged edits、校验高危 issue。
3. package report 输出模块清单、缺失项、禁用项、引导文件路径、统计摘要。
4. 章节导出 ZIP 只导出正文和章节元数据。
5. 全量备份包含项目数据、章节、索引、配置、checkpoint、WAL committed 状态摘要。
6. restore 第一版只做 dry-run plan，真实恢复需要用户确认。

测试：

- `PackagePrecheckServiceTests`：缺失项、未提交 WAL、staged edits。
- `PackageReportBuilderTests`：报告内容稳定。
- `ChapterExportServiceTests`：ZIP 内容和路径。
- `FullBackupServiceTests`：manifest 完整。
- `RestorePlannerTests`：dry-run 不写盘。

退出标准：

- 打包前能清楚列出“为什么还不适合打包”。
- 备份恢复 plan 可预览，不会静默覆盖。
- 模块禁用后不会进入 prompt/package。

Commit：`feat(core): add package precheck and backup`。

## 4. 测试

- 每个 M6 子模块先写 portable 单测，UI 只做 ViewModel 绑定测试。
- 旧 M1-M5 测试必须全过；M6 不允许破坏 M4 手工创作闭环。
- 每个涉及写盘的能力都要覆盖坏 JSON、半写入、重复执行、取消/失败降级。
- AI 真实调用只做手工烟测；单测用 fake client / fake middleware。

## 5. 风险

| ID | 风险 | 缓解 |
|---|---|---|
| R1 | M6 面太大，拖慢可用写作 | 严格按 M6.1-M6.9 串行，每段可独立交付 |
| R2 | 拆 ContextService 导致行为漂移 | 保留旧 facade，先内部拆分，旧调用和测试不变 |
| R3 | WAL 引入重复应用或脏状态 | chapterId + generationId 幂等键，committed WAL 不再应用 |
| R4 | Agent 写入误改数据 | staged changes + 用户确认 + 可回滚备份 |
| R5 | 多模型路由配置复杂 | M4.6 默认只暴露任务类型槽位，M6 给推荐默认值 |

## 6. 验收

1. `dotnet build Tianming.MacMigration.sln` → 0 Error。
2. `dotnet test Tianming.MacMigration.sln` → 全过。
3. 章节生成链路支持：FactSnapshot → 生成 → Humanize → CHANGES Canonicalizer → WAL 保存 → 派生索引。
4. 模拟生成中断后重启，pipeline 页能发现未提交 WAL 并恢复或丢弃。
5. 校验能按抽样策略跑，并给出章节/段落定位。
6. AI 模型能按 Chat / Writing / Polish / Validation 任务类型路由，限速时能换 Key 或降级。
7. Agent 对数据/正文的写入必须先进入待确认变更，拒绝时不落盘，确认后可追踪。
8. 一键成书 10 步管线至少能在 fake AI 下断点续跑完成一次。
9. 打包前预检能报告未提交 WAL / staged edits / 高危校验问题，备份恢复 dry-run 不写盘。

完成后，macOS 自用版不只是“迁移可用”，而是补齐 v2.8.7 对长篇稳定性、生成质量和 Agent 动手能力的核心承诺。

## 7. Mac_UI 视觉真值源

M6 9 个子里程碑对应参考图：

| 子里程碑 | 参考图 | 伪代码 |
|---|---|---|
| M6.1 Tracking 债务 + FactSnapshot 拆分 | （新建无对应图，参考 06 章节生成管道侧栏） | — |
| M6.2 HumanizeRules + CHANGES Canonicalizer | `Mac_UI/images/06-chapter-generation-pipeline.png` (CHANGES 区) | `Mac_UI/pseudocode/06-chapter-generation-pipeline.md` |
| M6.3 WAL + 生成恢复 | `Mac_UI/images/06-chapter-generation-pipeline.png` (进度区) | 同上 |
| M6.4 校验分层 + 向量定位 | `Mac_UI/images/08-unified-validation-report.png` | `Mac_UI/pseudocode/08-unified-validation-report.md` |
| M6.5 ContextService 拆分 | （无 UI，纯 service） | — |
| M6.6 AI middleware + 多模型路由 | `Mac_UI/images/09-ai-models-api-key-usage.png` (用量统计/路由) | `Mac_UI/pseudocode/09-ai-models-api-key-usage.md` |
| M6.7 Agent 插件 + ToolCallCard 待确认 | `Mac_UI/images/07-ai-conversation-panel.png` (Agent 模式) | `Mac_UI/pseudocode/07-ai-conversation-panel.md` |
| M6.8 一键成书 + 内容提炼 | （新建无对应图） | — |
| M6.9 打包预检 + 备份 | （新建无对应图） | — |

