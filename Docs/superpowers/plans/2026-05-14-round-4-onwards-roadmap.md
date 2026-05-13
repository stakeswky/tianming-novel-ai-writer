# Round 4+ Roadmap — M6.2 ~ M6.9 派单与排期

> 配套 Round 3 文档：`2026-05-14-round-3-codex-prompts.md`（M5 / M4.5+ / M6.1 三 lane）
>
> 本文档覆盖 M6 v2.8.7 写作内核升级**剩余 8 子里程碑**的 plan + codex 派单提示词，按"依赖链 + 工作量"分 3 个 Round 派发。

---

## 全景：M6 9 个子里程碑现状

| 子里程碑 | Plan 文件 | 派单 Round | 依赖 |
|---|---|---|---|
| M6.1 Tracking 债务 | 2026-05-14-tianming-m6-1-tracking-debts.md | **R3** | Round 2 |
| M6.2 HumanizeRules + CHANGES Canonicalizer | 2026-05-14-tianming-m6-2-humanize-changes-canonicalize.md | **R4** | R3 完成 |
| M6.3 WAL + 生成恢复 | 2026-05-14-tianming-m6-3-wal-recovery.md | **R4** | R3 完成 |
| M6.4 校验分层 + 向量定位 | 2026-05-14-tianming-m6-4-validation-layers-vector-locator.md | **R4** | R3 + M6.1 |
| M6.5 ContextService 拆分 | 2026-05-14-tianming-m6-5-context-service-split.md | **R5** | R4 完成 |
| M6.6 AI middleware + 多模型路由 | 2026-05-14-tianming-m6-6-ai-middleware-router.md | **R5** | R4 完成 |
| M6.7 Agent 插件 + Staged Changes | 2026-05-14-tianming-m6-7-agent-plugins-staged-changes.md | **R6** | R5 + M4.5+ |
| M6.8 一键成书 | 2026-05-14-tianming-m6-8-one-click-book.md | **R6** | R5 + M6.3 |
| M6.9 打包 + 备份 | 2026-05-14-tianming-m6-9-packaging-backup.md | **R6** | R5 完成 |

---

## 共同工作规范（嵌入每段 prompt）

参见 `2026-05-14-round-3-codex-prompts.md` 的"共同工作规范"小节。**核心 7 条**：
1. 严格按 plan step 顺序执行（不跳步、不合并）
2. TDD：测试先红→实现→绿→commit
3. 每个 step 5 的 Commit 动作就 commit 一次
4. commit message 用 Constraint / Rejected / Confidence / Scope-risk / Tested / Not-tested 格式
5. 每个 task 结束前 build + test 验证 0 Error 0 Warning + 测试全绿
6. plan 偏离时以真实 API 为准修正，在 commit message Constraint 字段说明
7. 不 push、不动其他 lane 文件、不 --no-verify

---

## Round 4 — 写作内核基础（M6.2 / M6.3 / M6.4）

R3（M5 / M4.5+ / M6.1）完成合并后启动。三 lane 完全并行（不同 worktree + 分支）。

### 🅰️ Lane A — M6.2 HumanizeRules + CHANGES Canonicalizer

```
你的任务：按 plan 实现章节生成 pipeline 中的 Humanize（去 AI 味）+ CHANGES Canonicalizer（多模型 JSON 规范化）两层处理。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Plan：Docs/superpowers/plans/2026-05-14-tianming-m6-2-humanize-changes-canonicalize.md（9 task）

前置确认：
1. git log main --oneline | head -10 应看到 R3 三条 merge commit（M5 / M4.5+ / M6.1）
2. dotnet test Tianming.MacMigration.sln 全过

工作流（共同规范见 round-3-codex-prompts.md）：
1. 开 worktree：git worktree add /Users/jimmy/Downloads/tianming-m6-2 -b m6-2-humanize-canonicalize main
2. 严格按 plan Task 0 → Task 9 执行；每个 step 的 commit 单独提交
3. 关键注意：
   - Task 8 集成 ContentGenerationPreparer：先 grep 真实 PrepareStrictAsync 签名，按真实代码插入 humanize/canonicalize 两步
   - ChangesProtocolParser 已是生产级，不修改，只在它前面调 Canonicalizer
4. 完成后跑全量 build + test，全过
5. 不 push、不动其他 lane

完成后输出：commit 列表 + plan task 完成状态 + 偏离点处置 + build/test 尾部 10 行。
```

### 🅱️ Lane B — M6.3 WAL + 生成恢复

```
你的任务：按 plan 实现 WAL（jsonl append-only）+ 生成恢复服务（启动 hook 扫描未完成的章节）。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Plan：Docs/superpowers/plans/2026-05-14-tianming-m6-3-wal-recovery.md（6 task）

前置确认：同 Lane A。

工作流：
1. 开 worktree：git worktree add /Users/jimmy/Downloads/tianming-m6-3 -b m6-3-wal-recovery main
2. 严格按 plan Task 0 → Task 6 执行
3. 关键注意：
   - Task 3 改 FileChapterTrackingSink.SaveAsync 为 atomic temp+rename：先 grep 真实 SaveAsync 方法体，最小侵入修改
   - Task 4 接入 ChapterGenerationPipeline：在每个流程节点之间 Append，Done 后 ClearAsync；不破坏现有签名（用可选参数）
   - Task 6 启动 hook：在 App.axaml.cs OnFrameworkInitializationCompleted 或 ServiceLocator 初始化后调 ReplayAsync
4. 完成后跑全量 build + test

完成后输出：commit 列表 + plan task 完成状态 + 偏离点处置 + build/test 尾部 10 行。
```

### 🅲 Lane C — M6.4 校验分层 + 向量定位

```
你的任务：按 plan 把 LedgerConsistencyChecker 拆 5 层 + 用 FileVectorSearchService 给每个 ConsistencyIssue 定位 chunk。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Plan：Docs/superpowers/plans/2026-05-14-tianming-m6-4-validation-layers-vector-locator.md（7 task）

前置确认：同 Lane A。**额外**：M6.1 TrackingDebt 已合并（plan Task 1 不与 M6.1 字段冲突）。

工作流：
1. 开 worktree：git worktree add /Users/jimmy/Downloads/tianming-m6-4 -b m6-4-validation-layers main
2. 严格按 plan Task 0 → Task 7 执行
3. 关键注意：
   - Task 3 五层拆分：打开 LedgerConsistencyChecker.cs，把 ValidateForeshadowing / ValidateConflicts / ValidateCharacters / ValidateTimeline / ValidateMovements / ValidateItems / ValidateRelationships 等子方法**按规则类型**映射到 5 层（不要重新发明规则）
   - Task 5 IVectorSearchService：FileVectorSearchService 加 interface 时不破坏现有 ctor 和方法签名
   - Task 6 GenerationGate 接入：用可选注入，不影响现有 ValidateAsync 主路径
4. 完成后跑全量 build + test

完成后输出：commit 列表 + 5 层各自测试通过数 + plan task 状态 + build/test 尾部 10 行。
```

---

## Round 5 — 基础设施增强（M6.5 / M6.6）

R4 完成合并后启动。两 lane 并行。

### 🅰️ Lane A — M6.5 ContextService 拆分

```
你的任务：按 plan 创建 4 个职责单一的 ContextService（Design / Generation / Validation / Packaging）。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Plan：Docs/superpowers/plans/2026-05-14-tianming-m6-5-context-service-split.md（5 task）

前置确认：R4 完成（M6.2/M6.3/M6.4 全合并 main）。

工作流：
1. 开 worktree：git worktree add /Users/jimmy/Downloads/tianming-m6-5 -b m6-5-context-service-split main
2. 严格按 plan Task 0 → Task 5 执行
3. 关键注意：
   - 不破坏现有 ChapterValidationPromptContextResolver / LookupDataTool（M6.5 是"新增并行实现"，不重写现有调用方）
   - Task 5 DI 注册时如 ICurrentProjectService.ProjectRoot 属性名不同，按真实 API 调整
4. 完成后跑全量 build + test

完成后输出：commit + plan task 状态 + 偏离点 + build/test 尾部 10 行。
```

### 🅱️ Lane B — M6.6 AI Middleware + 多模型路由

```
你的任务：按 plan 引入 AITaskPurpose 枚举 + IAIModelRouter + RoutedChatClient，让 ConversationOrchestrator 按用途分流模型。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Plan：Docs/superpowers/plans/2026-05-14-tianming-m6-6-ai-middleware-router.md（5 task）

前置确认：R4 完成。

工作流：
1. 开 worktree：git worktree add /Users/jimmy/Downloads/tianming-m6-6 -b m6-6-ai-middleware main
2. 严格按 plan Task 0 → Task 5 执行
3. 关键注意：
   - Task 1 UserConfiguration.Purpose 字段：用 string 默认 "Default"，保持 JSON 向后兼容
   - Task 4 ConversationOrchestrator：router 可选注入，未注入时 fallback 到现有 active config（不破坏现有测试）
   - Task 5 ModelManagementPage 加 Purpose 列：用 ComboBox 5 选 1
4. 完成后跑全量 build + test

完成后输出：commit + plan task 状态 + UserConfiguration 序列化兼容性确认（旧 JSON 能读）+ build/test 尾部 10 行。
```

---

## Round 6 — 高级能力（M6.7 / M6.8 / M6.9）

R5 完成合并后启动。三 lane 并行。

### 🅰️ Lane A — M6.7 Agent 插件 + Staged Changes

```
你的任务：按 plan 引入 3 个写工具 + Staged Changes 流程 + ConversationPanelViewModel 接入 Approve/Reject 命令。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Plan：Docs/superpowers/plans/2026-05-14-tianming-m6-7-agent-plugins-staged-changes.md（6 task）

前置确认：R5 完成（M6.5 / M6.6 + 之前所有合并到 main）。**额外**：M4.5+ ConversationPanelViewModel 已支持 IConversationOrchestrator 注入。

工作流：
1. 开 worktree：git worktree add /Users/jimmy/Downloads/tianming-m6-7 -b m6-7-agent-staged-changes main
2. 严格按 plan Task 0 → Task 6 执行
3. 关键注意：
   - Task 3 三个写工具按相同模板：DataEditTool 的 TargetId 用 "category:dataId" 拼接（plan Task 4 解析时按 ':' split）
   - Task 4 StagedChangeApprover 的三个 handler 委托：DI 注册时具体接 ChapterContentStore / ModuleDataAdapter / File.WriteAllText
   - Task 5 ConversationPanelViewModel：approver 可选注入，未注入时 ApproveCommand 不报错（直接返回）
4. 完成后跑全量 build + test，手工验证（启动 demo 看 ToolCallCard 的 Approve/Reject 按钮可用）

完成后输出：commit + plan task 状态 + 手工 demo 状态（Approve/Reject 是否真切到文件）+ build/test 尾部 10 行。
```

### 🅱️ Lane B — M6.8 一键成书

```
你的任务：按 plan 实现 BookGenerationOrchestrator + 10 个 step + BookPipelinePage。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Plan：Docs/superpowers/plans/2026-05-14-tianming-m6-8-one-click-book.md（5 task）

前置确认：R5 完成。**额外**：M6.3 WAL 已在 main（步骤状态持久化思路相同）。

工作流：
1. 开 worktree：git worktree add /Users/jimmy/Downloads/tianming-m6-8 -b m6-8-one-click-book main
2. 严格按 plan Task 0 → Task 5 执行；Task 4 的 10 个 step 各自一个 commit
3. 关键注意：
   - 10 个 step 实现可以非常薄（返回 Success=true 即可）；M6.8 重点是骨架，每步细节后续 lane 再深化
   - Task 5 BookPipelinePage 的 SkipStep / ResetStep 命令通过 ItemsControl ancestor binding 传 stepName
4. 完成后跑全量 build + test，手工启动 demo（左导航点"一键成书"应能打开 BookPipelinePage）

完成后输出：commit + plan task 状态 + UI demo 状态（10 step 列表可见、Start 按钮可点）+ build/test 尾部 10 行。
```

### 🅲 Lane C — M6.9 打包 + 备份

```
你的任务：按 plan 实现 ZIP 导出 + 全量备份 + 预检 + PackagingPage。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Plan：Docs/superpowers/plans/2026-05-14-tianming-m6-9-packaging-backup.md（4 task）

前置确认：R5 完成。

工作流：
1. 开 worktree：git worktree add /Users/jimmy/Downloads/tianming-m6-9 -b m6-9-packaging-backup main
2. 严格按 plan Task 0 → Task 4 执行
3. 关键注意：
   - Task 2 ZipBookExporter：用 System.IO.Compression.ZipArchive，已在 .NET 8 标准库；CompressionLevel.Optimal
   - Task 3 FileProjectBackupService：复制时排除 .backups（避免递归无限）、bin/obj/.git
   - Task 4 LeftNav 启用"打包"：把 IsEnabled: false 改成默认 enabled
4. 完成后跑全量 build + test，手工启动 demo（左导航"打包"可进入页面，导出 ZIP 可用）

完成后输出：commit + plan task 状态 + 导出 ZIP 实际验证（unzip -l 看内容是否含章节 + 设计、不含 .staged/.wal）+ build/test 尾部 10 行。
```

---

## 派单流程

每个 Round 启动前我会跑一遍：
```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer
git log main --oneline | head -10
dotnet build Tianming.MacMigration.sln --nologo -v minimal
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
```

确认 main 状态后，把上面对应 Round 的三段 prompt（M6.2/3/4 或 M6.5/6 或 M6.7/8/9）分别发给 3 个 codex 实例并行执行。完成后我合并审查（沿用 Round 2 流程）。

## 全部 plan 文件位置

```
Docs/superpowers/plans/
├── 2026-05-13-tianming-m4-4-chapter-pipeline-closure.md           [已合]
├── 2026-05-13-tianming-m4-5-ai-conversation-panel.md              [已合]
├── 2026-05-13-tianming-m4-6-ai-management.md                      [已合]
├── 2026-05-14-tianming-m4-5-plus-conversation-completion.md       [R3 待执行]
├── 2026-05-14-tianming-m6-1-tracking-debts.md                     [R3 待执行]
├── 2026-05-12-tianming-m5-macos-platform.md                       [R3 待执行]
├── 2026-05-14-tianming-m6-2-humanize-changes-canonicalize.md      [R4 待执行]
├── 2026-05-14-tianming-m6-3-wal-recovery.md                       [R4 待执行]
├── 2026-05-14-tianming-m6-4-validation-layers-vector-locator.md   [R4 待执行]
├── 2026-05-14-tianming-m6-5-context-service-split.md              [R5 待执行]
├── 2026-05-14-tianming-m6-6-ai-middleware-router.md               [R5 待执行]
├── 2026-05-14-tianming-m6-7-agent-plugins-staged-changes.md       [R6 待执行]
├── 2026-05-14-tianming-m6-8-one-click-book.md                     [R6 待执行]
├── 2026-05-14-tianming-m6-9-packaging-backup.md                   [R6 待执行]
├── 2026-05-14-round-3-codex-prompts.md                            [R3 提示词]
└── 2026-05-14-round-4-onwards-roadmap.md                          [R4-R6 提示词（本文）]
```
