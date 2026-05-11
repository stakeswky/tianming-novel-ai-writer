# Tianming AI M1 Core Implementation Note

日期：2026-05-10

## 目标

把原版 `Services/Framework/AI/Core/ApiKeyRotationService.cs` 的多 Key 轮换核心逻辑、`Services/Framework/AI/Monitoring/StatisticsService.cs` 的用量统计核心逻辑、`AIService` 中的模型库/用户配置数据层、API Key 真值外置存储边界和 OpenAI-compatible HTTP 调用子集、`PromptService` 的提示词文件存储子集、`PromptGenerationService` 的可移植核心、`VersionTestingService` 的版本测试文件存储子集、`ModelNameSanitizer` 的模型名泄露过滤逻辑、`SessionManager` 的会话文件存储逻辑、Ask/Plan/Agent 模式配置与消息映射、thinking/answer 展示分流、执行轨迹采集、项目上下文引用解析，以及 Plan/章节指令解析、归一化和摘要逻辑抽到 macOS 可构建的 `net8.0` 类库中，并用自动化测试证明不依赖 WPF、`TM.App`、`StoragePathHelper` 或 Windows API。

## 已实现

- 新增 `src/Tianming.AI/Tianming.AI.csproj`
- 新增统一迁移验证入口 `Tianming.MacMigration.sln`
- 新增 `src/Tianming.AI/Core/ApiKeyRotationService.cs`
- 新增 `src/Tianming.AI/Core/ApiKeySecretStore.cs`
- 新增 `src/Tianming.AI/Core/ConfiguredAITextGenerationService.cs`
- 新增 `src/Tianming.AI/Core/FileAIConfigurationStore.cs`
- 新增 `src/Tianming.AI/Core/ModelNameSanitizer.cs`
- 新增 `src/Tianming.AI/Core/OpenAICompatibleChatClient.cs`
- 新增 `src/Tianming.AI/Monitoring/FileUsageStatisticsService.cs`
- 新增 `src/Tianming.AI/PromptManagement/FilePromptTemplateStore.cs`
- 新增 `src/Tianming.AI/PromptManagement/FilePromptVersionStore.cs`
- 新增 `src/Tianming.AI/PromptManagement/PromptGenerationCoreService.cs`
- 新增 `src/Tianming.AI/PromptManagement/PromptVersionTestRunner.cs`
- 新增 `src/Tianming.AI/SemanticKernel/FileSessionStore.cs`
- 新增 `src/Tianming.AI/SemanticKernel/ChatHistoryCompressionService.cs`
- 新增 `src/Tianming.AI/SemanticKernel/Embedding/ITextEmbedder.cs`
- 新增 `src/Tianming.AI/SemanticKernel/Embedding/HashingTextEmbedder.cs`
- 新增 `src/Tianming.AI/SemanticKernel/FileVectorChapterIndexAdapter.cs`
- 新增 `src/Tianming.AI/SemanticKernel/FileVectorSearchService.cs`
- 新增 `src/Tianming.AI/SemanticKernel/References/ReferenceCatalog.cs`
- 新增 `src/Tianming.AI/SemanticKernel/References/ReferenceExpansionService.cs`
- 新增 `src/Tianming.AI/SemanticKernel/References/ReferenceParser.cs`
- 新增 `src/Tianming.AI/SemanticKernel/ExecutionEvent.cs`
- 新增 `src/Tianming.AI/SemanticKernel/Conversation/Config/*`
- 新增 `src/Tianming.AI/SemanticKernel/Conversation/Mapping/AskModeMapper.cs`
- 新增 `src/Tianming.AI/SemanticKernel/Conversation/Mapping/PlanModeMapper.cs`
- 新增 `src/Tianming.AI/SemanticKernel/Conversation/Mapping/AgentModeMapper.cs`
- 新增 `src/Tianming.AI/SemanticKernel/Conversation/Mapping/IConversationMessageMapper.cs`
- 新增 `src/Tianming.AI/SemanticKernel/Conversation/Models/ConversationMessage.cs`
- 新增 `src/Tianming.AI/SemanticKernel/Conversation/Helpers/ConversationSummarizer.cs`
- 新增 `src/Tianming.AI/SemanticKernel/Conversation/Helpers/ExecutionTraceCollector.cs`
- 新增 `src/Tianming.AI/SemanticKernel/Conversation/Helpers/SingleChapterTaskDetector.cs`
- 新增 `src/Tianming.AI/SemanticKernel/Conversation/Helpers/ThinkingBlockParser.cs`
- 新增 `src/Tianming.AI/SemanticKernel/Conversation/Models/ThinkingBlock.cs`
- 新增 `src/Tianming.AI/SemanticKernel/Conversation/Models/MessagePayload.cs`
- 新增 `src/Tianming.AI/SemanticKernel/Conversation/Parsing/*`
- 新增 `src/Tianming.AI/SemanticKernel/Conversation/Thinking/*`
- 复用原版：
  - `Services/Framework/AI/Core/AICategory.cs`
  - `Services/Framework/AI/Core/AIProvider.cs`
  - `Services/Framework/AI/Core/AIModel.cs`
  - `Services/Framework/AI/Core/ApiKeyEntry.cs`
  - `Services/Framework/AI/Core/KeyRotationTypes.cs`
  - `Services/Framework/AI/Core/UserConfiguration.cs`
  - `Services/Framework/AI/Monitoring/ApiCallRecord.cs`
  - `Modules/AIAssistant/PromptTools/PromptManagement/Models/PromptCategory.cs`
  - `Modules/AIAssistant/PromptTools/PromptManagement/Models/PromptTemplateData.cs`
  - `Modules/AIAssistant/PromptTools/VersionTesting/Models/TestVersionData.cs`
  - `Framework/Common/Helpers/AI/IPromptGenerationService.cs`
  - `Framework/Common/Helpers/AI/PromptGenerationContext.cs`
  - `Framework/Common/Helpers/AI/PromptGenerationResult.cs`
  - `Framework/Common/Helpers/Id/ShortIdGenerator.cs`
- 新增 `tests/Tianming.AI.Tests/ApiKeyRotationServiceTests.cs`
- 新增 `tests/Tianming.AI.Tests/ConfiguredAITextGenerationServiceTests.cs`
- 新增 `tests/Tianming.AI.Tests/FileAIConfigurationStoreTests.cs`
- 新增 `tests/Tianming.AI.Tests/FileUsageStatisticsServiceTests.cs`
- 新增 `tests/Tianming.AI.Tests/FilePromptTemplateStoreTests.cs`
- 新增 `tests/Tianming.AI.Tests/FilePromptVersionStoreTests.cs`
- 新增 `tests/Tianming.AI.Tests/PromptGenerationCoreServiceTests.cs`
- 新增 `tests/Tianming.AI.Tests/PromptVersionTestRunnerTests.cs`
- 新增 `tests/Tianming.AI.Tests/ModelNameSanitizerTests.cs`
- 新增 `tests/Tianming.AI.Tests/MacOSKeychainApiKeySecretStoreTests.cs`
- 新增 `tests/Tianming.AI.Tests/OpenAICompatibleChatClientTests.cs`
- 新增 `tests/Tianming.AI.Tests/FileSessionStoreTests.cs`
- 新增 `tests/Tianming.AI.Tests/ConversationParsingTests.cs`
- 新增 `tests/Tianming.AI.Tests/ConversationModeProfileCatalogTests.cs`
- 新增 `tests/Tianming.AI.Tests/ConversationModeMapperTests.cs`
- 新增 `tests/Tianming.AI.Tests/PlanModeMapperTests.cs`
- 新增 `tests/Tianming.AI.Tests/ExecutionTraceCollectorTests.cs`
- 新增 `tests/Tianming.AI.Tests/PlanStepNormalizerTests.cs`
- 新增 `tests/Tianming.AI.Tests/ConversationSummarizerTests.cs`
- 新增 `tests/Tianming.AI.Tests/ThinkingParsingTests.cs`
- 新增 `tests/Tianming.AI.Tests/ChatHistoryCompressionServiceTests.cs`
- 新增 `tests/Tianming.AI.Tests/FileVectorChapterIndexAdapterTests.cs`
- 新增 `tests/Tianming.AI.Tests/FileVectorSearchServiceTests.cs`
- 新增 `tests/Tianming.AI.Tests/ReferenceCatalogTests.cs`
- 新增 `tests/Tianming.AI.Tests/ReferenceExpansionServiceTests.cs`

## 覆盖行为

- 只在启用且非空 Key 之间轮换。
- 支持调用方传入排除 Key 集合。
- `RateLimited` 会临时禁用 Key。
- `AuthFailure` / `Forbidden` / `QuotaExhausted` 会永久禁用 Key 并触发 `KeyStateChanged`。
- 连续 `ServerError` 到阈值后进入冷却。
- `GetPoolStatus` 可返回 active / temporarily disabled / permanently disabled 状态和失败统计。
- 模型库支持 wrapped JSON 与数组 JSON 两种格式。
- 缺少 `models.json` 时可从 `ProviderModels/*.models.json` 加载模型。
- 用户配置支持增删改查、单 active 配置、重复 Provider/Model 覆盖更新。
- 用户配置持久化会清空 `ApiKey`，避免把明文 Key 写入 `user_configurations.json`。
- `IApiKeySecretStore` 支持 API Key 真值外置；`MacOSKeychainApiKeySecretStore` 封装 macOS `/usr/bin/security` 的 add/find/delete generic password 命令；配置重载会按配置 Id 回填 secret，删除配置会清理 secret。
- OpenAI-compatible HTTP 调用子集支持 `/v1/chat/completions` URL 规范化、Bearer / `X-Api-Key` / Cherry Studio 兼容头、非流式 message content/usage 解析、SSE `data:` delta content 流式解析、HTTP error payload 映射、模型名前缀与 `-free` 清理。
- `ConfiguredAITextGenerationService` 已把 active configuration、Provider endpoint / CustomEndpoint、Model name、DeveloperMessage、ApiKey 和可选多 Key 轮换串到非流式与流式文本生成；非流式/流式生成已覆盖轮换 Key 使用、成功上报和 429 后排除当前 Key 重试。
- `OpenAICompatiblePromptTextGenerator` 可作为 `IPromptTextGenerator` 接到提示词生成核心服务。
- 生成统计可通过显式路径写入和重载 `generation_statistics.json`，并支持 reset 与坏 JSON 恢复。
- 校验报告可通过显式 reports 目录保存、按章节读取、获取最新状态、删除和忽略坏 JSON。
- 卷级校验汇总可通过显式 data 目录加载、按卷覆盖保存、绑定分卷分类、删除、解析卷号和忽略坏 JSON。
- 单章 AI 校验 prompt 输入可由 `ChapterValidationPromptInputBuilder` 合并章节元数据、设计侧上下文、生成侧上下文与已知结构性问题，再交给 `ChapterValidationPromptComposer` 输出最终提示词。
- AI 校验上下文源到 prompt/executor 的接线已由 `ChapterValidationAIWorkflow` 端口，复用 `ChapterAIValidationExecutor` 的 AI 失败和异常降级逻辑。
- 单章 `ValidationContext` / `ContextIdCollection` 加载到 AI prompt/executor 的编排已由 `ValidationContextAIWorkflow` 端口。
- 单章校验控制流已由 `ChapterValidationProcessor` 抽成可注入委托的跨平台编排器，覆盖章节 ID 解析、缺正文错误、规则层到 AI 层顺序和总状态判定。
- 卷级校验控制流已由 `VolumeValidationProcessor` 抽成跨平台编排器，覆盖目标卷过滤、抽样、2 章批次、批次异常降级、结果排序和卷级汇总组装。
- `IUnifiedValidationService` 的跨平台包装层已由 `PortableUnifiedValidationService` 端口，负责章节报告保存、卷级汇总保存和 `NeedsRepublishAsync` 委托。
- 文件报告/汇总落盘接线已由 `FileValidationPersistenceAdapter` 端口，可直接把 `PortableUnifiedValidationService` 的保存委托接到 `FileValidationReportStore` / `FileValidationSummaryStore`。
- 原版 `ValidationContext` 到 AI prompt sources 的映射已由 `ValidationContextPromptSourceMapper` 端口，并把相关设计/生成纯模型纳入 `src/Tianming.ProjectData` 编译面。
- 事实快照抽取器已由 `PortableFactSnapshotExtractor` / `IFactSnapshotGuideSource` 端口普通章节上一章状态抽取、未来状态隔离、卷末全量最新状态抽取、角色/地点描述、世界观硬规则抽取、PlotPoints 相关情节点搜索、近期活跃实体补入和原版活跃实体/势力/物品/时间线/PlotPoints 注入限额；`FileFactSnapshotGuideSource` 已接入原版 guide/design JSON 文件布局读取与分卷聚合。
- 一致性调和的 staging/bak 恢复、tmp 清理、孤立备份目录清理、guide JSON 损坏检测与 bak 恢复、孤立摘要清理、缺失摘要补建、卷里程碑重建回调、关键词索引缺失章节 best-effort 补建、`vector_degraded.flag` 自愈清理/保留、追踪缺口检测、孤立追踪清理、追踪缺口摘要补建和已完成卷 fact archive 回补已由 `PortableConsistencyReconciler` / `IChapterSummaryRepairStore` / `IChapterKeywordIndexRepairStore` / `IChapterVectorIndexRepairStore` / `IChapterTrackingRepairStore` / `IVolumeFactArchiveRepairStore` 端口。
- 章节修复问题清单展平与按章节 hints 构建已由 `ChapterRepairProblemHintBuilder` 端口；修复提示词构建已由 `ChapterRepairPromptComposer` 端口，覆盖有效 CHANGES 剥离、非法协议保留原文、原文截断和问题清单编号；`PortableChapterRepairService` 已端口可注入的修复编排边界，覆盖缺失 `FactSnapshot` 阻断、修复 hints 传递、保存快照门禁和下一章衔接提示。
- API 调用记录可落盘并重载。
- `ConfiguredAITextGenerationService` 非流式/流式生成可选回写成功/失败调用、耗时、token 与错误信息到 `FileUsageStatisticsService`。
- 汇总统计覆盖成功/失败、平均响应时间、输入/输出 token、首末调用时间。
- 每日统计、按模型统计、最近记录、过期裁剪和清空统计可独立验证。
- 提示词分类/模板原模型、用户模板文件存储、内置模板递归加载、用户模板覆盖内置模板、内置模板不可改删、模板 CRUD 与变更事件已端口。
- AI 生成提示词输入校验、元提示词构建、AI 失败映射、JSON/纯文本返回解析、tags 解析已端口。
- 提示词版本测试原模型、`test_versions.json` 加载、按创建时间倒序、版本测试增删改查、测试结果字段更新、清空和临时文件保存已端口；单版本 AI 测试执行、成功/失败状态写回、同输入双提示词对比执行已端口。
- 模型名/Provider 泄露过滤、Kimi/Qwen 自称替换和 chunk 过滤已端口，可用可选日志回调替代 `TM.App.Log`。
- 会话索引、消息 JSON 存储、创建/删除/重命名、模式更新、当前会话回落和空会话清理已端口。
- 对话压缩 90% 上下文触发、rolling memory 生成、结构化记忆合并、最近轮次保留和摘要失败硬截断已端口。
- 向量搜索 `ITextEmbedder` 边界、纯托管 hashing embedder、本地相似度搜索、无模型/无依赖 keyword fallback、章节 Markdown 分块、相关度排序、按章节取片段、删除后缓存失效已端口；生成流水线可通过 `IChapterDerivedIndex` / `FileVectorChapterIndexAdapter` 更新与清理向量缓存。
- 项目上下文引用下拉类型目录、候选项按名称/ID 过滤、`@续写/@重写/@仿写` token 构建、英文别名解析、英文/中文冒号和空格分隔解析、标点边界截断以及从后向前替换引用已端口到 `ReferenceCatalog` / `ReferenceParser`；章节摘要 context block、标题 HTML 转义、topK 关键片段截断、缺章节 ID/缺上下文提示和仿写引用保留已端口到 `ReferenceExpansionService`。
- Ask/Plan/Agent 模式配置目录、payload target、raw content 显示策略、执行引擎需求和未知模式回落已端口。
- Ask/Plan/Agent streaming message mapper、轻量 `ConversationMessage` / `ConversationRole`、thinking 分析块、Plan payload/summary/parse-failed 和 Agent payload 映射已端口。
- 中文数字解析、计划步骤解析、步骤详情绑定、计划步骤计数、`@续写/@重写` 章节指令解析、单章任务合并、多章范围拆分、明确多章节计划保持已端口。
- `PlanModeMapper` 已支持显式 guides 目录下 `content_guide*.json` 的快速计划，覆盖按章节号筛选、`@续写` 定位下一章、未匹配章节提示和基于纲要的伪 thinking。
- 计划生成摘要、执行完成摘要、执行失败摘要和执行轨迹摘要已端口。
- 执行事件、工具调用 start/completed/failed 聚合、执行轨迹按步骤排序、耗时统计和常见失败原因用户友好格式化已端口。
- 待办执行顺序运行、事件发布、重试、不可重试失败中断、后台启动、拒绝重入和取消复位已端口到 `TodoExecutionService`。
- thinking/answer 标签流式分流、未闭合标签分片保留、噪声行过滤和分析块标题拆分已端口。

## 验证

```bash
dotnet test tests/Tianming.AI.Tests/Tianming.AI.Tests.csproj -v minimal
```

结果：133 个测试全部通过。

统一 solution 验证：

```bash
dotnet test Tianming.MacMigration.sln -v minimal
dotnet build Tianming.MacMigration.sln -v minimal
```

结果：ProjectData 218 个测试、AI 133 个测试、Framework 781 个测试全部通过；solution build 为 0 Warning / 0 Error。

## 后续边界

- Key 编辑 UI 接线和真实 macOS Keychain 人工验收仍需迁移。
- 模型管理 UI、用量统计 UI、提示词管理 UI、提示词版本测试 UI 仍需迁移。
- 完整 `AIService` / `SKChatService` 的 SemanticKernel 多协议 provider、Agent plugin、项目上下文引用真实 Guide/向量搜索委托接线和业务会话状态仍需迁移。
