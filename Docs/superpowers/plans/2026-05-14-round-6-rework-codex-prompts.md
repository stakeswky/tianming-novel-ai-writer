# Round 6 返工 — M6.9 ZIP 排除 / M6.7 Staged-Id 结构化 Codex 派单提示词

> 配套：
> - 原始 Round 6 集成：commit `81f74dc Merge Round 6 advanced authoring capabilities`
> - 加修 commit：`8b1cf01` ToolCallCard 渲染、`9de73d7` 备份排除 .staged

---

## 背景

Round 4-6（M6.2 ~ M6.9）已合入 main 并 push（`origin/main = 81f74dc`），数字对账精确（1610 passed / 0 W / 0 E）。第三方 review 在 codex 主动加修两点上发现：

| 加修点 | 状态 | 发现 |
|---|---|---|
| M6.7 ToolCallCard 真渲染路径 | ✓ 真接通 | 中等脆弱：`StagedId` 通过扫描工具结果文本里 `"stg-"` 前缀提取，约定耦合；改文案会断 |
| M6.9 备份排除 `.staged` | ✓ 属实 | **不完整**：`ZipBookExporter.cs:12-21` 仍漏排 `.staging`，`.staging/old.md` 会被打进 ZIP 发布包 |

本轮返工目标：把这两个点收口。

---

## 共同前置 + 工作规范

仓库根：`/Users/jimmy/Downloads/tianming-novel-ai-writer`
主分支：`main`（已含 Round 6 整体 merge `81f74dc`）

为这两个返工开**一条 worktree + 一条分支**（不分两条，是 Round 6 同主题加修的延续）：

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer
git worktree add /Users/jimmy/Downloads/tianming-round6-rework -b round6-rework main
cd /Users/jimmy/Downloads/tianming-round6-rework
git status                              # 必须 clean
dotnet build Tianming.MacMigration.sln --nologo -v minimal   # 0 W / 0 E
dotnet test Tianming.MacMigration.sln --nologo -v minimal | tail -10   # 1610 passed
```

工作规范沿用 `2026-05-14-round-3-codex-prompts.md`：

1. **TDD**：每条返工先写复现测试 → 红 → 改实现 → 绿 → commit
2. **commit message**：Round 3 同款格式（Constraint / Rejected / Confidence / Scope-risk / Tested / Not-tested）
3. **commit 粒度**：每个返工点至少一个 commit，可拆不可合
4. **build / test**：每条返工结束跑 `dotnet build` + `dotnet test`，0 W/E + 全绿才进下一条
5. **禁止**：`git push`、`--no-verify`、跨返工 scope 改动、修无关代码
6. **遇到与本返工无关的发现**：记在最终输出的"附带发现"段，不要顺手改

---

## 返工点 R-1：ZipBookExporter 补排 `.staging` + 备份侧补测试覆盖（**关键**）

### 问题精确定位

```
src/Tianming.ProjectData/Packaging/ZipBookExporter.cs:12-21
  ExcludedDirectories = { .staged, .wal, .backups, bin, obj, .git, .vs }
  // ❌ 缺 .staging
```

而 `ChapterContentStore.cs:32`、`ContentGenerationCallback.cs:139,328`、`PortableConsistencyReconciler.cs:230` 都在用 `.staging` 写章节中间态。结果：任何跑过 chapter pipeline 的项目，打包后 ZIP 都会包含 `.staging/*` 半成品文件。

第三方 review 用临时 fixture 独立复现：

```
$ unzip -l /tmp/m69-proof.zip
  Generated/chapters/vol1_ch1.md           ✓
  Design/Elements/Characters/char-001.json ✓
  .staging/old.md                          ✗  ← 被打进发布包
```

附带：备份侧 `FileProjectBackupService.cs:13-23` 排了 `.staging` 但**从未在测试里断言**，靠 HashSet 字面值兜底。

### 给 codex 的提示词

```
你的任务：补全 M6.9 排除规则，让 ZIP 发布包不包含 .staging 半成品；同时给备份侧补 .staging / .wal 排除测试。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Worktree：/Users/jimmy/Downloads/tianming-round6-rework（基于 main）
分支：round6-rework

完全限定的改动范围（不扩大 scope）：

【步骤 1】补 ZipBookExporter 排除规则

文件：src/Tianming.ProjectData/Packaging/ZipBookExporter.cs:12-21
改动：在 ExcludedDirectories HashSet 中新增一行 ".staging",
（保持已有的 .staged / .wal / .backups / bin / obj / .git / .vs 不变）

【步骤 2】给 ZipBookExporter 补测试

文件：tests/Tianming.ProjectData.Tests/Packaging/ZipBookExporterTests.cs
新增 1 条测试：
  ExportAsync_excludes_staging_directory
  - 构造 fixture：含 Generated/chapters/vol1_ch1.md + .staging/old.md + Generated/.staging/draft.md
  - 调用 ExportAsync
  - 断言：ZIP 含 vol1_ch1.md
  - 断言：ZIP 不含 .staging/old.md 和 Generated/.staging/draft.md
  - 用现有测试的 fixture/assertion 模式（参考第三方 review 用过的实证路径：
    临时 TempPath + ZipArchive.OpenRead + Entries.Any(e => e.FullName.Contains(...))）

TDD：先写测试 → 跑红（因为 .staging 尚未在 HashSet）→ 改 HashSet → 跑绿 → commit

【步骤 3】给备份侧补 .staging / .wal 排除测试

文件：tests/Tianming.ProjectData.Tests/Backup/FileProjectBackupServiceTests.cs
新增 2 条测试，结构与现有 CreateBackup_excludes_staged_changes_directory 对称：
  CreateBackup_excludes_staging_directory
  CreateBackup_excludes_wal_directory

每条：fixture 含对应目录 + 一个合法源文件 → 跑 CreateBackup → 断言备份目录不含相应排除目录、含合法文件

TDD：先写测试 → 跑绿（因为 .staging 和 .wal 本来就在 HashSet 里）→ commit
这步是补"测试覆盖断言"，不改实现；如果跑红说明 HashSet 实际没生效，那就需要修实现

【独立 ZIP 实证（在 commit 前必做）】

跑：
  dotnet test --filter "ExportAsync_excludes_staging_directory" --nologo -v minimal

然后（如果测试需要落到物理路径就用这个；若用 in-memory stream 则跳过）：
  在 test fixture 目录下手工生成一个真实 zip 到 /tmp/round6-rework-proof.zip
  unzip -l /tmp/round6-rework-proof.zip
  
把 unzip -l 输出贴在最终汇报里作为 ZIP 实证。
完成后清理 /tmp/round6-rework-proof.zip。

完成后跑：
- dotnet build Tianming.MacMigration.sln --nologo -v minimal （0 W / 0 E）
- dotnet test Tianming.MacMigration.sln --nologo -v minimal （全绿，ProjectData 应 +3，达到 ≥405）

完成后输出：
- 新增 commit 列表（短哈希 + 一行标题）
- 步骤 1/2/3 完成状态
- ZIP 实证 unzip -l 输出
- 附带发现（如有）
- 全量 build/test 尾部 10 行

不 push，不动 R-2 范围以外的代码（R-2 的 staged-id 结构化是独立返工）。
```

---

## 返工点 R-2：M6.7 Staged-Id 提取从字符串扫描升级为结构化 payload（中等）

### 问题精确定位

```
src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ConversationPanelViewModel.cs
  ExtractStagedId(result_text) 通过扫描 "stg-" 字符串前缀提取 ID
```

ID 来源是工具结果文本里的中文提示 `"待审核：{id}"`（`ContentEditTool.cs:59` 等 3 处）。这是约定耦合：改文案 → 解析断 → ApproveCommand 拿不到 staged-id → 整条审核链断。

codex 在 `8b1cf01` 的 commit message 已自承："Preserve the staged-id extraction path … or add a structured delta before removing it"。

### 给 codex 的提示词

```
你的任务：把 M6.7 staged-id 从字符串扫描升级到结构化 delta payload，去掉对中文提示文本格式的耦合。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Worktree：/Users/jimmy/Downloads/tianming-round6-rework（与 R-1 同 worktree，**R-1 完成后**做 R-2）
分支：round6-rework

完全限定的改动范围（不扩大 scope）：

【步骤 0】先把 R-1 的 commit 跑完，确认 build/test 0 W / 0 E + 全绿，再开始 R-2

【步骤 1】在 ToolResultDelta（或同等 delta 类型）上加结构化 StagedId 字段

找到 BulkEmitter 解析 tool result 走的 delta 类型（关键字：ToolResultDelta、ConversationStreamDelta、ToolCallDelta）
给它加一个可空字段：
  public string? StagedId { get; init; }
（如果已经存在某个 metadata 字典，用 metadata["StagedId"] 也可，但更建议显式字段——结构化比 dict 强类型）

【步骤 2】让 3 个工具发结构化 StagedId 而不是写文案

文件（基于 M6.7 lane）：
  src/Tianming.AI/.../ContentEditTool.cs:59 附近的 "待审核：{id}" 文本拼接
  src/Tianming.AI/.../DataEditTool.cs（同款）
  src/Tianming.AI/.../WorkspaceEditTool.cs（同款）

改法：
  - 在工具 stage 一条 change 时，产出的 ToolResultDelta 同时填 StagedId = stagedId
  - 保留人类可读的中文文案在 Content 里（这是 UI 显示给用户的），但 ID 解析不再从 Content 来

【步骤 3】BulkEmitter / ConversationPanelViewModel 改读结构化 StagedId

文件：BulkEmitter.cs 或 emit pipeline
  - 把 delta.StagedId 设到对应 ConversationToolCallVm.StagedId
文件：ConversationPanelViewModel.cs
  - 删除 ExtractStagedId / ResolveStagedCommandParameter 里所有 "stg-" 字符串扫描代码
  - ApproveStagedCommand / RejectStagedCommand 直接从 ConversationToolCallVm.StagedId 拿

【步骤 4】更新测试

- BulkEmitterTests.Staged_tool_result_renders_tool_call_card_instead_of_plain_text：现在 ToolCallDelta 要带 StagedId 字段；断言渲染后 vm.StagedId 不为 null
- ConversationPanelViewModelTests.ApproveStagedAsync_updates_tool_call_card_state：用结构化 StagedId 注入，不再写 "stg-foo" 字符串
- 新增 1 条：BulkEmitter_uses_structured_staged_id_not_text_scan
  - delta.Content = "[result]" (不含 "stg-" 字符串)
  - delta.StagedId = "stg-xyz"
  - 断言渲染后 vm.StagedId == "stg-xyz"
- 新增 1 条 negative：BulkEmitter_does_not_extract_staged_id_from_text_when_structured_field_is_missing
  - delta.Content = "stg-fake-not-from-tool"
  - delta.StagedId = null
  - 断言 vm.StagedId == null（不再从文本扫描）

TDD 顺序：先改测试期望 → 跑红 → 改实现 → 跑绿 → commit（每个 step 一个 commit，至少 4 个 commit）

完成后跑：
- dotnet build 0 W / 0 E
- dotnet test 全绿
- 关键校验：grep -rn "stg-" src/Tianming.Desktop.Avalonia/ 应该**不再有**字符串扫描代码（命中只能在测试 fixture 或常量字典里）

完成后输出：
- 新增 commit 列表
- 步骤 1-4 完成状态
- grep "stg-" 命中清单（应该全部是 test fixture 或正当用途）
- 测试结果尾部 10 行
- 附带发现（如有）

不 push，不动 R-1 范围内代码（已 commit 完）。
```

---

## 派发顺序

**串行**（R-1 先 R-2 后），同 worktree 同分支：

| 返工 | 工作量预估 | 风险 |
|---|---|---|
| R-1 ZIP 漏排修复 + 备份测试补充 | 30-45 分钟 | 低（一行 HashSet + 3 个测试） |
| R-2 staged-id 结构化 | 2-3 小时 | 中（涉及 delta schema 改动 + 3 个工具 + VM 同步） |

返工完成后我（主 thread）会：
1. 在 worktree 跑 build/test 复核
2. 派 review subagent 重新跑"R-1 ZIP 真排除 .staging？R-2 真删除字符串扫描？"
3. 通过 → `--no-ff` merge 进 main + push（同 Round 3 / Round 6 模式）
4. 清理 worktree + 本地分支

---

## 已知遗留（不在本轮返工范围）

- `OpenAICompatibleChatClient.BuildChatCompletionsUri` 对空/无 scheme endpoint 缺校验（Round 3 已记，独立 issue）
- macOS 中文 IME 候选条拦截 @候选（Round 3 已记，平台限制）
- 应用启动后真实模型触发 tool call 的 live desktop click-through 验证（Round 6 codex 已诚实标注，平台/实机依赖）
- App.axaml + ThemeBridge 硬编码 Light，dark mode 不生效（Round 3 Lane A 已记，待 M5.x）
