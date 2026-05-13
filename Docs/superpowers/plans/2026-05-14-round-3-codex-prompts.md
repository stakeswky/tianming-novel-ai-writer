# Round 3 三 Lane 并行 — Codex 任务提示词集

> 配套 plan：
> - **M5**：`Docs/superpowers/plans/2026-05-12-tianming-m5-macos-platform.md`（54 task 已 step-level）
> - **M4.5+**：`Docs/superpowers/plans/2026-05-14-tianming-m4-5-plus-conversation-completion.md`（11 task / 36 step）
> - **M6.1**：`Docs/superpowers/plans/2026-05-14-tianming-m6-1-tracking-debts.md`（8 task / 28 step）

---

## 共同前置（三条 lane 都要满足）

**仓库根**：`/Users/jimmy/Downloads/tianming-novel-ai-writer`
**主分支**：`main`
**必须先做**：把 m4-4 / m4-5 / m4-6 三条 lane merge 入 main（或者保证 main 已经含等价 commit），否则三条 worktree 都基不上正确的 base。

确认方法（在派 codex 前我会跑）：
```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer
git log main --oneline | head -10  # 应看到 Merge Lane M4.4/M4.5/M4.6 三条
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10  # 全过
```

---

## 共同工作规范（每条 lane 都嵌入到 prompt）

1. **严格按 plan 的 step 顺序执行**：不跳步、不合并 step、不省略已写出的代码块
2. **TDD**：每个 task 先写测试（plan 已给出测试代码）→ 跑确认红 → 实现 → 跑确认绿 → commit
3. **commit 粒度**：每个 step 5 的"Commit"动作就 commit 一次。不要把多个 step 合并到一个 commit
4. **commit message**：沿用 Round 2 codex 的格式：
   ```
   <action title>

   <一行简述>

   Constraint: <plan 里的约束或现有代码约束>
   Rejected: <尝试过但放弃的方案> | <理由>
   Confidence: <high/medium/low>
   Scope-risk: <narrow/moderate/broad>
   Tested: <跑过的命令>
   Not-tested: <未覆盖的场景，如有>
   ```
5. **build / test 验证**：每个 task 结束前跑 `dotnet build` + `dotnet test --filter <new tests>`，确认 0 Error 0 Warning + 测试全绿才能进下一个 task
6. **遇到 plan 偏离**：如果 plan 写的代码与当前真实 API 冲突（如某个字段不存在、签名不一致），**先以真实 API 为准修正**，并在 commit message 的 `Constraint:` 字段说明
7. **禁止**：
   - `git push`（除非用户后续明确要求）
   - 修改任何与本 lane 无关的代码（保持 blast radius 最小）
   - 跳过任何 plan 里的测试（如果 plan 写的测试编译不过就先修测试本身让它编过，再跑红→绿）
   - `--no-verify` / 跳过 hook

---

## Lane A：M5 macOS 平台

### 给 codex 的提示词

```
你的任务：按 plan 执行 M5 macOS 平台 lane（Keychain + 系统代理装配 + NativeMenu + 主题占位）。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Plan 文件：Docs/superpowers/plans/2026-05-12-tianming-m5-macos-platform.md（已是 step-level，54 task）

前置确认：
1. cd 到仓库根，git log main 应看到 "Merge Lane M4.4"、"Merge Lane M4.5"、"Merge Lane M4.6" 三条 merge commit（即 Round 2 已合）；如未合并，停下汇报，不要开始
2. dotnet test Tianming.MacMigration.sln --nologo -v q 应全过

工作流：
1. 开 worktree + 分支：
   git worktree add /Users/jimmy/Downloads/tianming-m5 -b m5-macos-platform main
   cd /Users/jimmy/Downloads/tianming-m5
2. 严格按 plan 的 Task 0 → Task 6 顺序执行，每个 step 都跑 plan 里写的命令
3. 注意：plan 的 Task 0.1 提到 ".worktrees/m2-parallel"，这是旧路径；按上面我给的新 worktree 路径执行即可
4. 每个 step 的"Commit"动作 commit 一次；commit message 用 Round 3 共同工作规范的格式
5. 重要发现：通过前期调研，以下能力**已经实装在 main**（不需要重写，只需确认存在 + 在 plan 对应 task 标 "已实装，跳过实现，但仍要写测试"）：
   - 系统代理：`AvaloniaSystemHttpProxy` / `MacOSSystemProxyService` / `ProcessScutilCommandRunner` 已在 src/Tianming.Framework/Platform/ 和 Avalonia/Infrastructure/
   - HttpClient 装配：AvaloniaShellServiceCollectionExtensions.cs:66-76 已注册 named client "tianming"
   - 主题轮询：Theme/ThemeBridge.cs + MacOSSystemAppearanceMonitor 3 秒轮询已实装（但 UI 当前 hardcoded Light）
   - Keychain：src/Tianming.AI/Core/ApiKeySecretStore.cs 完整
6. **真正缺的硬骨头**（plan Task 4）：App.axaml 缺 <NativeMenu.Menu> 整段 XAML，MainWindowViewModel 命令已绑但菜单 XAML 未挂
7. 完成后跑：
   dotnet build Tianming.MacMigration.sln --nologo -v minimal
   dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
   两者全过才算完成
8. 不 push、不动其他 lane 的文件

完成后输出：
- 列出本 lane 创建的全部 commit（短哈希 + 一行标题）
- 列出 plan 中 task 0-6 的完成状态（每个一行 ✓/✗）
- 如果 plan 任何 step 与真实代码冲突，列出冲突 + 你如何处置
- 全量 build/test 输出尾部 10 行

如遇阻塞：停下汇报，不要自行扩大 scope。
```

---

## Lane B：M4.5+ AI 对话面板补完

### 给 codex 的提示词

```
你的任务：按 plan 把 m4-5 demo 切片升级成真对话面板（DI 注册 + 16ms BulkEmitter + 4 个新控件 + 接真 Orchestrator + 历史抽屉 + @query）。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Plan 文件：Docs/superpowers/plans/2026-05-14-tianming-m4-5-plus-conversation-completion.md（11 task / 36 step）

前置确认：
1. git log main 应看到 Round 2 三条 merge commit
2. m4-5 分支上 ConversationOrchestrator / IFileSessionStore / IConversationTool 三套类型已存在（合并自 m4-5 lane）
3. dotnet test Tianming.MacMigration.sln 全过

工作流：
1. 开 worktree：
   git worktree add /Users/jimmy/Downloads/tianming-m4-5-plus -b m4-5-plus-conversation-completion main
   cd /Users/jimmy/Downloads/tianming-m4-5-plus
2. 严格按 plan Task 0 → Task 11 顺序执行
3. 每个 step 的"Commit"动作 commit 一次；commit message 用 Round 3 共同工作规范的格式
4. 注意点：
   - **Task 1.3 DI 注册**：plan 给的 using 路径 `TM.Services.Framework.AI.SemanticKernel.*` 是真实命名空间（已核实），直接复制
   - **Task 4.3 IConversationOrchestrator**：plan 要求把现有 ConversationOrchestrator 抽接口；这是 m4-5 分支的代码，不是 main 上完全新写，**注意修改而非重写**
   - **Task 5 RightConversationViewModel**：原 ctor 是 `()`（无参），改为接受 3 个 DI 参数后，确认 AvaloniaShellServiceCollectionExtensions 里的 `s.AddSingleton<RightConversationViewModel>()` 现在能 resolve；如有旧的手动 new 行，需要删
   - **Task 10.4 替换 RightConversationView**：原 RightConversationView.axaml 内有 ItemsControl/StackPanel 段，整段替换；保留 UserControl 根元素和 xmlns
   - **重要：ICurrentProjectService.ProjectRoot 必须确认存在**；如果它的属性名不同（例如 `CurrentRoot`），按真实 API 调整 plan 的 Task 1.3 注册代码
5. 完成后跑：
   dotnet build Tianming.MacMigration.sln --nologo -v minimal
   dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
6. 手工启动验证（plan Task 10.5 + 11.3）：
   dotnet run --project src/Tianming.Desktop.Avalonia
   按 plan Task 11.3 的 6 项 checklist 验证；如 Avalonia 启动失败截屏汇报
7. 不 push、不动 M5 / M6 lane 文件

完成后输出：
- 全部 commit（短哈希 + 一行标题）
- plan Task 0-11 完成状态（每个 ✓/✗）
- 11.3 的 6 项手工 checklist 状态（哪些通过、哪些有问题）
- plan 偏离点（如有）+ 你的处置
- 全量 build/test 尾部 10 行

如遇阻塞或类型签名与 plan 不符：停下汇报，不要自行扩大 scope。
```

---

## Lane C：M6.1 Tracking 债务

### 给 codex 的提示词

```
你的任务：按 plan 引入 5 类追踪债务（EntityDrift / Omission / Deadline / Pledge / SecretReveal）+ 检测器 + 持久化 + Pipeline 集成。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Plan 文件：Docs/superpowers/plans/2026-05-14-tianming-m6-1-tracking-debts.md（8 task / 28 step）

前置确认：
1. git log main 应看到 Round 2 三条 merge commit
2. src/Tianming.ProjectData/Tracking/ 目录存在以下文件（plan 依赖）：
   - FactSnapshot.cs（含 class FactSnapshot + CharacterCoreDescription 等）
   - GuideModels.cs（含 ForeshadowingStatusGuide / ForeshadowingStatusEntry）
   - GateModels.cs（含 ChapterChanges / CharacterStateChange 等）
   - ChapterTrackingDispatcher.cs（含 IChapterTrackingSink 接口）
   - FileChapterTrackingSink.cs
   - PortableFactSnapshotExtractor.cs
3. dotnet test Tianming.MacMigration.sln 全过

工作流：
1. 开 worktree：
   git worktree add /Users/jimmy/Downloads/tianming-m6-1 -b m6-1-tracking-debts main
   cd /Users/jimmy/Downloads/tianming-m6-1
2. 严格按 plan Task 0 → Task 8 顺序执行
3. 每个 step 的"Commit"动作 commit 一次；commit message 用 Round 3 共同工作规范的格式
4. 关键注意：
   - **Plan Task 4b（EntityDriftDetector）**：plan 假设 CharacterStateChange 有 `FieldChanges` 字典，但**这个字段可能不存在**！先打开 src/Tianming.ProjectData/Tracking/GateModels.cs 查 CharacterStateChange 的真实字段
     - 如果有 NewState / OldState 等字段（包含 HairColor / EyeColor / Appearance），重写 EntityDriftDetector 用真实字段对比 previousSnapshot.CharacterDescriptions[id].HairColor vs change.NewState.HairColor
     - 如果什么都没有，按 plan 的"重要"提示在 GateModels.cs 给 CharacterStateChange 加 `FieldChanges` 字段（向后兼容默认 new()）
     - 一切 **以真实代码为准修正 plan**，并在 commit message Constraint 字段说明
   - **Plan Task 6 FileChapterTrackingSink**：检查它的 ctor 和已有 `_volume` 字段。如果它本来不带 volume，需要适配（参考 VolumeFile() 模式）
   - **Plan Task 7 Pipeline 集成**：SaveGeneratedChapterStrictAsync 签名复杂，**不要拆它重写**；只在它现有 Dispatch 步骤后追加 `if (_debtRegistry != null) { ... }` 段
   - **章节 ID 比较**：plan Task 4d PledgeDetector 用 string.Compare 默认假设 `ch-{N:D3}` 格式；如果真实项目用别的格式（如 `ch{N}` 无前导 0），改用 parse 出数字后比较
5. 完成后跑：
   dotnet build Tianming.MacMigration.sln --nologo -v minimal
   dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10
   两者全过；新增 ≥15 条测试
6. 不 push、不动 M5 / M4.5+ lane 文件、不动 src/Tianming.Desktop.Avalonia 除了 Task 8 的 DI 注册行

完成后输出：
- 全部 commit（短哈希 + 一行标题）
- plan Task 0-8 完成状态（每个 ✓/✗）
- 5 个 Detector 的测试通过数（EntityDrift / Omission / Deadline / Pledge / SecretReveal 各几条）
- plan 偏离点 + 处置
- 全量 build/test 尾部 10 行

如遇阻塞（如 CharacterStateChange / FileChapterTrackingSink 签名实在不能适配）：停下汇报，不要扩大 scope。
```

---

## 派发顺序建议

三条 lane 相互独立（不同 worktree、不同分支、不同代码区），可**完全并行**派给 codex：

| Lane | 工作量 | 风险 |
|---|---|---|
| M5 | ~1.5 天（绝大部分已实装，主要补 NativeMenu XAML + 测试 + 验收文档）| 低 |
| M4.5+ | ~2 天（11 task 含真实 UI 改动 + DI + 手工验收）| 中（Avalonia binding 易出 runtime 错）|
| M6.1 | ~3 天（plan Task 4 5 个 detector 是核心，Task 7 Pipeline 集成最有风险）| 中-高（plan 对真实 ChapterChanges/Sink 签名做了假设）|

派给 codex 前，我会再跑一次 main build/test 确认绿，然后把上面三段 prompt 分别发给 3 个 codex 实例。完成后我会合并审查（同 Round 2 的方式）。
