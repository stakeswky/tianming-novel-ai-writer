# Round 3 返工 — Lane B / C Codex 派单提示词集

> 配套：
> - 原始 Round 3 派单：`Docs/superpowers/plans/2026-05-14-round-3-codex-prompts.md`
> - Lane B 原 plan：`Docs/superpowers/plans/2026-05-14-tianming-m4-5-plus-conversation-completion.md`
> - Lane C 原 plan：`Docs/superpowers/plans/2026-05-14-tianming-m6-1-tracking-debts.md`

---

## 背景

Round 3 执行完成后第三方 review（main thread + 3 个 review subagent）发现：

| Lane | 状态 | 风险 |
|---|---|---|
| Lane A (M5 macOS) | ✅ 已 merge 到 main (`9445b0e`) | 仅 docs，无源码改动 |
| Lane B (M4.5+ 对话面板) | ⚠️ 待返工 | catch-all 异常静默回退 demo；`@` 候选 hard-coded 三条样本；5/6 手工 checklist 未实跑 |
| Lane C (M6.1 Tracking Debts) | ⚠️ 待返工 | `LoadPledgeGuideAsync` / `LoadSecretGuideAsync` 硬返 `null`，Pledge + SecretReveal 两 detector 在 production 是事实死代码 |

返工目标：把 B / C 的实质风险关掉再合 main。代码层接线和测试基本都是真的，只有以下精确点需要改。

---

## 共同前置（沿用 Round 3）

仓库根：`/Users/jimmy/Downloads/tianming-novel-ai-writer`
主分支：`main`（已含 Lane A 的 merge commit `9445b0e`）
Worktree 复用：
- Lane B：`/Users/jimmy/Downloads/tianming-m4-5-plus`（分支 `m4-5-plus-conversation-completion`，**直接在原分支上追加 commit**，不要新开分支）
- Lane C：`/Users/jimmy/Downloads/tianming-m6-1`（分支 `m6-1-tracking-debts`，**直接在原分支上追加 commit**，不要新开分支）

返工前必跑：

```bash
cd <worktree>
git status                       # 必须 clean
git log --oneline -3             # 确认是 Round 3 的最后 commit
dotnet test --nologo -v q 2>&1 | tail -10   # 必须全绿
```

---

## 共同工作规范（沿用 `2026-05-14-round-3-codex-prompts.md`）

1. **TDD**：每条返工先写复现测试（红）→ 改代码（绿）→ commit
2. **commit message** 沿用 Round 3 格式（含 Constraint / Rejected / Confidence / Scope-risk / Tested / Not-tested）
3. **commit 粒度**：每个返工点至少一个 commit，可以拆，不要合
4. **build / test 验证**：每个返工点结束跑 `dotnet build` + `dotnet test`，0 Warning / 0 Error + 全绿才能进下一个
5. **禁止**：`git push`、`--no-verify`、修改本返工点以外的代码、跨 lane 改动
6. **遇到与本返工点无关的发现**：记在最终输出的"附带发现"段，不要顺手改

---

## Lane B 返工：M4.5+ 对话面板真接线兜底 + 真数据候选 + 实机验证

### 给 codex 的提示词

```
你的任务：把 Lane B (m4-5-plus-conversation-completion 分支) 的三处实质风险关掉，使其可合入 main。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Worktree：/Users/jimmy/Downloads/tianming-m4-5-plus（已存在，**直接复用原分支追加 commit**）
Plan：Docs/superpowers/plans/2026-05-14-tianming-m4-5-plus-conversation-completion.md

前置确认：
1. cd /Users/jimmy/Downloads/tianming-m4-5-plus && git status 必须 clean
2. git log --oneline -3 应看到 2f7ffbe / 284484e / 6dc1346 等 Round 3 最后几个 commit
3. dotnet test --nologo -v q 全绿（基线 Avalonia 205 / Framework 791 / AI 162 / ProjectData 273）

需要返工的三处（**完全限定在这三处，不扩大 scope**）：

【返工点 B-1】删除 catch-all 静默 demo 回退

  文件：src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ConversationPanelViewModel.cs
  位置：SendAsync 方法 L113-116（具体行号以你看到为准）
  现状：
    catch (Exception)
    {
        await SendLocalDemoAsync(input);
    }
  问题：orchestrator 任意异常都被吞掉，UI 看到的是预览串，让"send 没崩=功能可用"的判断失效

  修法（提案 A，优先）：把异常作为 system 角色 bubble 渲染出来，让用户看见
    catch (Exception ex)
    {
        SampleBubbles.Add(new ConversationBubbleVm
        {
            Role = ConversationRole.Assistant,
            Content = $"[错误] {ex.GetType().Name}: {ex.Message}",
            Timestamp = DateTime.Now,
        });
    }
  保留 OperationCanceledException 的空 catch（cancel 是正常路径）
  保留 L91 那处 `if (_orchestrator == null || _emitter == null) { await SendLocalDemoAsync(input); }` — 那是合法的 DI 未注入兜底（不在本返工范围）

  测试：tests/Tianming.Desktop.Avalonia.Tests/Conversation/ConversationPanelViewModelTests.cs
  新增 1 条：用 mock orchestrator 抛 InvalidOperationException("boom")
    Assert：SampleBubbles 最后一条 Content 包含 "boom"，且不调用 SendLocalDemoAsync 的内部 demo 文案

  TDD：先写测试 → 跑红 → 改实现 → 跑绿 → commit

【返工点 B-2】@ 候选改接真项目数据

  文件：同上 ViewModels/Conversation/ConversationPanelViewModel.cs
  位置：PopulateReferenceCandidates 方法 L246-260
  现状：硬编码 ch-001 / 诸葛清 / 九州大陆 三条样本
  问题：和真实项目数据完全无关，用户实机敲 @九 拿到的也是这三条

  修法：
  1. 注入一个候选 provider 接口（如已有等价的 reference / mention service，复用；否则新建 IReferenceSuggestionSource）
       Task<IReadOnlyList<ReferenceItemVm>> SuggestAsync(string query, CancellationToken ct);
  2. 实现：从 ICurrentProjectService.ProjectRoot 读章节列表 + 角色卡 + 世界设定（已有 ProjectData 解析器，复用，不要自己再写解析）；
     filter by query (OrdinalIgnoreCase Contains)，最多返回 10 条
  3. ConversationPanelViewModel ctor 接受 IReferenceSuggestionSource?（可空，未注入时回退到当前 hard-coded 样本，保持现有测试不破）
  4. AvaloniaShellServiceCollectionExtensions 注册新 provider

  测试：
  - 新增 ReferenceSuggestionSourceTests：mock ICurrentProjectService.ProjectRoot 指向 fixture 目录，断言能拿到 fixture 里章节 + 角色，且 filter by query 工作
  - ConversationPanelViewModelTests 新增 1 条：注入 mock provider 返回 2 条结果，敲 InputDraft="hello @九" → ReferenceCandidates.Count == 2

  TDD：先写测试 → 跑红 → 实现 → 跑绿 → commit

【返工点 B-3】手工 checklist 实机过 5/6

  这步**不写代码**，只是按 plan 11.3 的清单实机跑 + 把结果以 commit 形式写入 `Docs/superpowers/plans/2026-05-14-tianming-m4-5-plus-conversation-completion.md` 的"11.3 验收"段。

  cd /Users/jimmy/Downloads/tianming-m4-5-plus
  dotnet run --project src/Tianming.Desktop.Avalonia

  逐项验证（每项要么 ✓ 要么写明实际现象）：
  1. Ask 模式默认选中
  2. 切换到 Plan、Agent 模式，按钮状态正确
  3. 在输入框输入 "hello" 并点击发送，看到流式回复
  4. 关闭应用，重新打开，历史抽屉里有上一次会话
  5. 输入框输入 "测试 @九" → 出现 @九... 提示候选
  6. 点击候选，输入框自动补全为完整引用

  如果某项失败，截屏 + 在 plan 验收段写明"❌ <一句根因猜测>"，不要在本返工 commit 里去修——记到"附带发现"

  commit 这份文档更新

完成后跑：
- dotnet build Tianming.MacMigration.sln --nologo -v minimal （0 W / 0 E）
- dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10 （全绿；Avalonia 应至少 +2，达到 ≥207）

完成后输出：
- 新增的 commit 列表（短哈希 + 一行标题）
- 三个返工点完成状态（B-1 / B-2 / B-3 各 ✓/⚠️/✗ + 一句说明）
- 11.3 手工 checklist 6 项实跑结果
- 附带发现（如有，本返工 commit 没修的事）
- 全量 build/test 尾部 10 行

不 push，不动 Lane C worktree，不动 main。
```

---

## Lane C 返工：Pledge / Secret guide 数据源装配

### 给 codex 的提示词

```
你的任务：把 Lane C (m6-1-tracking-debts 分支) 的 Pledge / SecretReveal 两 detector 从"事实死代码"接通到真实数据源。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Worktree：/Users/jimmy/Downloads/tianming-m6-1（已存在，**直接复用原分支追加 commit**）
Plan：Docs/superpowers/plans/2026-05-14-tianming-m6-1-tracking-debts.md

前置确认：
1. cd /Users/jimmy/Downloads/tianming-m6-1 && git status 必须 clean
2. git log --oneline -3 应看到 0646c90 / e5542c5 / d978589 等 Round 3 最后几个 commit
3. dotnet test --nologo -v q 全绿（基线 ProjectData 293 / AI 162 / Framework 791 / Avalonia 197）

问题精确定位：
  src/Tianming.ProjectData/Generation/ChapterGenerationPipeline.cs:205-212
    private Task<PledgeGuide?> LoadPledgeGuideAsync() => Task.FromResult<PledgeGuide?>(null);
    private Task<SecretGuide?> LoadSecretGuideAsync() => Task.FromResult<SecretGuide?>(null);
  整个仓库无 production 路径生产 PledgeGuide / SecretGuide 实例
  → PledgeDetector / SecretRevealDetector 永远 early-return，不触发
  → commits 1574519 (Pledge) + d2e8040 (SecretReveal) 在 production 是死代码

需要返工（**完全限定在以下两步，二选一**）：

【方案 A，推荐】接通文件加载

  类似于 FactSnapshot / ForeshadowingStatusGuide 的现有加载模式（参考 PortableFactSnapshotExtractor 或 ChapterGenerationPipeline 里已有的 LoadFooAsync 实现），把 LoadPledgeGuideAsync / LoadSecretGuideAsync 改为从约定路径读 JSON：

  约定路径（参考 GuideModels.cs:211/228 里 PledgeGuide / SecretGuide 的 Module 字段命名）：
    <projectRoot>/<volume>/guides/PledgeGuide.json
    <projectRoot>/<volume>/guides/SecretGuide.json

  实现：
  1. 文件不存在 → 仍然 return null（保留现有 detector early-return 语义，文件存在才检测）
  2. 文件存在 → JsonSerializer.Deserialize<PledgeGuide>，失败时 log 警告 + return null（容错）
  3. 复用 ChapterGenerationPipeline 已有的 IFileSystem / IPathResolver 抽象（如有）；没有就用 File.ReadAllTextAsync

  测试（新增至少 4 条，加到 tests/Tianming.ProjectData.Tests/Generation/ChapterGenerationPipelineDebtTests.cs 或同类文件）：
  - LoadPledgeGuide_returns_null_when_file_missing
  - LoadPledgeGuide_returns_guide_when_file_present
  - LoadSecretGuide_returns_null_when_file_missing
  - LoadSecretGuide_returns_guide_when_file_present
  以及至少 1 条端到端：fixture 包含 PledgeGuide.json with 一条 IsFulfilled=false 且 DeadlineChapter 已过 → 跑 pipeline → tracking_debts_volN.json 应含 Pledge 类型债务

  TDD：先写测试 → 跑红 → 实现加载 → 跑绿 → commit

【方案 B，备选 — 仅当方案 A 不可行】明确隔离死代码

  如果 PledgeGuide / SecretGuide 的数据来源还没设计好（例如等 M6.x 的 guide editor），那就：
  1. 在 AvaloniaShellExt（或 detector 注册的 DI 容器配置）里**注释掉** PledgeDetector / SecretRevealDetector 的注册
  2. 在原 plan 文件 Task 4d / Task 4e 段头加 "⏸️ 暂未启用 — 待 guide 数据源 ready 后启用，见 TODO #<...>"
  3. 在 README 或 plans 索引加 TODO

  这种情况下不动 detector 代码本身（保留测试），只动注册 + 文档。

选哪个？

  默认走方案 A。仅当：
  - 读盘约定路径与项目方实际数据组织冲突；或
  - 该 guide 数据应由 LLM 生成而非人写（需要单独设计） 
  才用方案 B。决定写在 commit message 的 Rejected: 字段说明放弃方案 A 的理由。

附带建议返工（小）：

  src/Tianming.ProjectData/Tracking/.../<sink>.cs 的 volN_chN 正则
    Regex (?:vol|v)(\d+)_(?:ch|c)(\d+)|^(\d+)_(\d+) 解析失败 fallback (0,0)
  → 现在格式漂移会静默归到 vol0/ch0 串错
  请改为 throw InvalidOperationException("Unrecognized chapter id format: <id>")
  并新增 1 条测试断言不合法格式抛异常

  这步可独立 commit。

完成后跑：
- dotnet build Tianming.MacMigration.sln --nologo -v minimal （0 W / 0 E）
- dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -10 （全绿；ProjectData 应至少 +5，达到 ≥298）

完成后输出：
- 新增 commit 列表（短哈希 + 一行标题）
- 选了方案 A 还是 B + 理由
- LoadPledge / LoadSecret 端到端测试：fixture 含 PledgeGuide.json + 过期 pledge → tracking_debts_volN.json 是否含 Pledge 类型债务（贴实际 JSON 片段）
- volN_chN 严格解析变更：是否做了，新增测试结果
- 附带发现（如有）
- 全量 build/test 尾部 10 行

不 push，不动 Lane B worktree，不动 main。
```

---

## 派发顺序建议

两条 lane 独立，可**并行**派给两个 codex 实例：

| Lane | 工作量预估 | 风险 |
|---|---|---|
| Lane B 返工 | 4-6 小时（B-1 半小时、B-2 真实数据接线 2-3 小时、B-3 手工 1-2 小时）| 中（PopulateReferenceCandidates 接真数据要看现有 ProjectData API）|
| Lane C 返工 | 2-4 小时（方案 A 加载 + 测试 + 解析严格化） | 低（路径已经精确定位）|

返工完成后我（主 thread）会：
1. 在两个 worktree 各跑一次 build/test 复核
2. Lane B 用 review subagent 重新跑一遍 "B-1 / B-2 / B-3 是否真修了" 的核查
3. Lane C 用 review subagent 跑 "PledgeGuide 端到端是否真打通 + 死代码风险是否解除" 的核查
4. 两条都通过 → 顺序 merge 入 main（同 Lane A 的 `--no-ff` 风格）

---

## 不在本轮返工范围（已记入"已知遗留债"）

- Lane A 的主题"Light 占位"问题（`App.axaml:16` + `ThemeBridge.cs:45/54`）— 另开 M5.x
- Lane B 的 Ask/Plan/Agent 模式下游行为差异（mode 枚举传下去后 orchestrator 怎么用）— 属于 M4.5+ 范畴外的 orchestrator 行为，待 M6.6 AI middleware router 一并处理
