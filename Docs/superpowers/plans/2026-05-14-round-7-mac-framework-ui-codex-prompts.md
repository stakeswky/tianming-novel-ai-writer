# Round 7 — Mac 框架能力 UI 接线 + Sink DI 修补 + 真机回归修复 Codex 派单提示词

> 配套：
> - 功能对齐矩阵 D 区块：`Docs/macOS迁移/功能对齐矩阵.md` L78-106（共 27 项框架能力）
> - 第三方覆盖核查（2026-05-14）：D 区块 27 项中 **0 项完整接线**、3 项部分接线、24 项仅 lib 无 UI
> - **Computer Use 真机巡检（2026-05-14）**：`Docs/macOS迁移/M5-ComputerUse-功能巡检-2026-05-14.md` — 主壳 / Welcome / Dashboard / 草稿 / 设计 / 生成 / 提示词 / 使用统计 / 打包 / 对话面板可见可交互；但发现 3 个产品级 bug（一键成书导航不同步 / AI Provider 下拉无选项 / @ 引用建议无候选）
> - 关键 red flag：4 个 macOS 平台 sink（`MacOSSystemAppearanceMonitor` / `MacOSNotificationSink` / `MacOSSpeechOutput` / `MacOSSystemMonitorProbe`）**未注册 DI**——即使 UI 接通也跑不起来

---

## 背景

Round 1-6 把 `Tianming.Framework` / `Tianming.AI` / `Tianming.ProjectData` 三个跨平台 lib 推到了几乎完整覆盖原版 Windows 能力；Round 6 完成 v2.8.7 写作内核升级（M6.1-M6.9）并 push 到 main（`81f74dc` → `48a342d`）。

但第三方核查发现 **D 框架能力区块（27 项）几乎没有 Avalonia UI 落地**：

| 状态 | 项数 | 编号 |
|---|---|---|
| 🟢 完整接线 | 0 | — |
| 🟡 部分接线 | 3 | #1 主题底层、#19 代理透明嵌入、#23 运行环境探针 |
| 🔴 仅 lib 无 UI | 24 | 其余 |

具体 red flag：

- `PageKeys.Settings` 在 `Navigation/PageRegistry.cs:434` 路由到 `PlaceholderView` — 用户点"设置"什么都看不到
- ViewModels/ 目录 0 个 `*Settings*` / `*Theme*` / `*Notification*` VM
- 50+ 个 `Portable*` / `MacOS*` lib 类在 Avalonia 端无任何引用（事实死代码）
- 关键 macOS sink 未注册 DI

这一轮（Round 7）目标：**把 Avalonia 端的"设置"区块从占位升级为真实可用的设置中心**，覆盖矩阵 D 区块 27 项；先修真机回归 + sink DI 让平台能力跑起来，然后并行做 3 个主题域的 settings page。

## Round 7 启动前 Computer Use 真机巡检（2026-05-14）

Codex 用 Computer Use 在 macOS Avalonia 窗口（`dev.tianming.avalonia.manualtest`，临时 bundle `/tmp/TianmingDev.app`）逐项手测主导航，发现以下产品级 bug（**M4-M6 已 ship 功能的真实可见缺陷**，不是 lib 缺失）：

| 风险 | 位置 | 现象 | 根因猜测 |
|---|---|---|---|
| **R1** | 一键成书 (BookPipelinePage) | breadcrumb 变 `book.pipeline`，中心内容**仍是上一页（使用统计）** | `NavigationService` 页面切换绑定 / `BookPipelineView` 未注册路由 / 内容宿主复用旧 DataContext |
| **R2** | AI 模型页 + API 密钥页 | Provider 下拉弹层出现但**无可见选项**，导致用户无法配模型 / 配 Key → 对话发送返回 `InvalidOperationException: No enabled UserConfiguration matches purpose Writing` | `ModelManagementViewModel` / `ApiKeysViewModel` 的 provider source 初始化或 ComboBox `ItemsSource` binding |
| **R3** | 右侧对话面板 @ 引用 | 新会话后输入 `@ch` 无候选弹出 | 三种可能：当前项目无匹配数据 / `ReferenceSuggestionSource.SuggestAsync` 不返回 / macOS 中文 IME 拦截（Round 3 已知）—— Round 6 R-2 改完结构化 `StagedId` 后引用候选路径可能被串到 staged path |

巡检中其他可观察的小问题：
- 设置页占位文案 `"此页面将在 M4 实装。"` 已过期（实际 Round 7 才做 Settings Shell）— 由 Lane A 替换路由时自动解决
- `dotnet run` 直接启动的 macOS 进程**没有 `CFBundleIdentifier`**，导致 Computer Use 无法 attach。codex 用 `/tmp/TianmingDev.app` 临时 bundle 解决——这点应入 Round 7 测试工具流（见 Lane R Task 0）

---

## 共同前置

仓库根：`/Users/jimmy/Downloads/tianming-novel-ai-writer`
主分支：`main`（tip `48a342d`，已 push 到 origin）

每条 lane 开自己的 worktree + 分支，**严格顺序**：

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer
# Step 1: Lane R 真机回归修复（最高优先，用户可见的产品 bug）：
git worktree add /Users/jimmy/Downloads/tianming-m7-lane-r-regressions -b m7-lane-r-regressions main

# Step 2: Lane R 完成 + merge 后，做 Lane 0 sink DI：
git worktree add /Users/jimmy/Downloads/tianming-m7-lane0-sinks -b m7-lane0-sinks main

# Step 3: Lane 0 完成 + merge 后，开始 Lane A：
git worktree add /Users/jimmy/Downloads/tianming-m7-lane-a-appearance -b m7-lane-a-appearance main

# Step 4: Lane A 完成 + merge 后，开 Lane B + C 并行：
git worktree add /Users/jimmy/Downloads/tianming-m7-lane-b-notifications -b m7-lane-b-notifications main
git worktree add /Users/jimmy/Downloads/tianming-m7-lane-c-system -b m7-lane-c-system main
```

前置确认（在每个 worktree 跑）：

```bash
git status                                                          # clean
dotnet build Tianming.MacMigration.sln --nologo -v minimal          # 0 W / 0 E
dotnet test Tianming.MacMigration.sln --nologo -v minimal | tail -5 # 1615 passed
```

---

## 共同工作规范（沿用 Round 3 / Round 6）

1. **进 worktree 第一步用 `superpowers:writing-plans` skill 把本 Lane 提示词翻译为 step-level plan**，落到 `Docs/superpowers/plans/2026-05-XX-tianming-m7-<lane>.md`，再开始执行
2. **TDD**：每个 step 先写测试 → 红 → 改实现 → 绿 → commit
3. **commit message** Round 3 同款格式（Constraint / Rejected / Confidence / Scope-risk / Tested / Not-tested）
4. **commit 粒度**：每个 step 5 的 "Commit" 动作 commit 一次，不要合
5. **build / test**：每 step 跑 `dotnet build` + `dotnet test`，0 W/E + 全绿才进下一步
6. **禁止**：`git push`、`--no-verify`、跨 lane 改动、修无关代码
7. **遇到原版无对应 macOS 等价能力**（如 Windows Registry / TMProtect 等）→ 在 plan 标 "替换 / 降级"，不要硬塞
8. **遇到 lib API 与 plan 不符** → 以真实 API 为准修正，commit message Constraint 字段说明
9. **每个 Lane 的 Settings Page 必须接到 `PageKeys.Settings`** 主入口（如果还没有 Settings shell，Lane A 顺手做）

---

## Lane R：Computer Use 真机回归修复（最高优先，先做）

修 codex 真机巡检（2026-05-14）发现的 3 个产品级 bug。这些是 M4-M6 已 ship 功能在 macOS 上的真实可见缺陷，**用户视角阻断当前可用性**：
- R1 一键成书页面打不开（导航 + 内容不同步）
- R2 AI Provider 下拉无选项（用户没法配模型）
- R3 @ 引用建议无候选

### 给 codex 的提示词

```
你的任务：修 Round 7 启动前 Computer Use 真机巡检发现的 3 个产品级 bug，让 macOS Avalonia 版主交互链路在用户视角通畅。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Worktree：/Users/jimmy/Downloads/tianming-m7-lane-r-regressions
分支：m7-lane-r-regressions（基于 main）
巡检证据：Docs/macOS迁移/M5-ComputerUse-功能巡检-2026-05-14.md（必读）

前置确认：
1. git status clean
2. dotnet build 0 W / 0 E
3. dotnet test 1615 passed
4. 浏览巡检文档 R1/R2/R3 三段，认清复现步骤

工作流：

【步骤 0】写 plan
用 superpowers:writing-plans 写 step-level plan：
  Docs/superpowers/plans/2026-05-XX-tianming-m7-lane-r-regressions.md
建议拆分：
  Task 0  测试工具流（macOS bundle 包装脚本入仓 + 文档化 Computer Use attach 套路）
  Task 1  R1 修复：BookPipeline 导航/内容同步
  Task 2  R2 修复：AI Provider 下拉 source 初始化
  Task 3  R3 排查：@ 引用建议 — 先做 fixture 复测分离 IME / 数据 / 代码三类根因，再决定是否改代码

【Task 0】固化 macOS bundle 测试工具流

巡检中 codex 用 /tmp/TianmingDev.app 临时 bundle 让 Computer Use attach。
固化到仓库：
- Scripts/build-dev-bundle.sh：把 dotnet build 输出包装成最小 .app bundle（含 Info.plist + bundle id dev.tianming.avalonia.manualtest），输出到 /tmp/TianmingDev.app
- README 或 Docs/macOS迁移/manual-test-howto.md：记录"如何让 Computer Use attach macOS Avalonia 进程"步骤

这是巡检文档透出的方法论沉淀，不入仓后续每次手测都要重做。

【Task 1】R1 BookPipeline 导航/内容不同步

复现：
1. dotnet run 后点左侧"使用统计"
2. 再点左侧"一键成书"
现象：breadcrumb 变 "book.pipeline"，中心内容仍是 UsageStatisticsView

诊断步骤：
- 看 src/Tianming.Desktop.Avalonia/Navigation/PageRegistry.cs：BookPipelinePage 是否注册了 PageKeys.BookPipeline 路由
- 看 NavigationService 或 ShellViewModel 处理 NavigateTo：是否只更新了 breadcrumb 但没切 ContentControl 的 Content
- 看 BookPipelinePage 的 axaml.cs 是否正常构造（如果 view 注册的 key 与 NavigationService 发出的 key 不一致就会无视图响应）
- 对比同样按钮导航成功的页面（如 AI 模型 → ModelManagementPage）的注册写法，找差异

修法（按真实根因二选一或并取）：
- 如果是 PageRegistry 缺注册 → 加注册行
- 如果是 NavigationService 内容宿主未刷新 → 修事件订阅 / DataContext 更新

测试：
- tests/Tianming.Desktop.Avalonia.Tests/Navigation/ 新增 1 条单测
  - NavigateTo(PageKeys.BookPipeline) 后 ShellViewModel.CurrentPage 必须是 BookPipelinePage 类型（不是上一页）
- 这条测试在修复前必须红，修复后绿

实机验证：
- 重新跑 Scripts/build-dev-bundle.sh + Computer Use 复现一遍巡检 R1 步骤
- 把验证截图或描述写入 commit message Tested 字段

【Task 2】R2 AI Provider 下拉无选项

复现：
1. 点左侧"模型"或"API 密钥"
2. 点 Provider 下拉
现象：弹层打开但无选项文本

诊断：
- 看 src/Tianming.Desktop.Avalonia/ViewModels/AI/ModelManagementViewModel.cs
- 找 Provider 候选 ObservableCollection 的初始化（关键字 Providers / ProviderOptions / AvailableProviders）
- 是空集合？还是有数据但 ItemTemplate 未渲染 DisplayMember？
- 看 ModelManagementPage.axaml 里 ComboBox 的 ItemsSource binding 和 DisplayMemberBinding / ItemTemplate
- 数据来源：是从 FileAIConfigurationStore 加载？该 store 加载用什么 path？默认目录是空的话会返回空集合 — 那需要内置一组默认 provider（OpenAI/Anthropic/Google/...）作为 fallback

ApiKeysViewModel 同款处理（两个页可能共用同一份 provider source）。

修法：
- 如果是空集合：补默认 provider list（fallback 内置 OpenAI/Anthropic/Google/Azure/DeepSeek/Cherry Studio 等，与 Tianming.AI 的 OpenAICompatibleChatClient 已支持的 provider 对齐）
- 如果是 binding：修 ItemTemplate + DisplayMemberBinding

测试：
- tests/Tianming.Desktop.Avalonia.Tests/ViewModels/AI/ModelManagementViewModelTests.cs 新增
  - VM 构造后 Providers 集合至少 ≥1 条
  - Provider 项的 DisplayName 不为空
- 同款 ApiKeysViewModelTests

实机验证：
- 复跑巡检步骤，确认下拉打开后能看到 ≥1 个 provider 选项

【Task 3】R3 @ 引用建议未显示

巡检不能确认根因（无匹配数据 / 数据源未返回 / IME 拦截）。先用代码层排除前两种：

诊断步骤：
- 找 src/Tianming.Desktop.Avalonia/ViewModels/Conversation/ReferenceSuggestionSource.cs（Round 3 Lane B 新建）
- 看 SuggestAsync(query) 的 query=ch 时返回什么：
  - 如果项目根有匹配章节但 SuggestAsync 返回空 → 是数据源 bug，需修
  - 如果项目根没匹配数据 → 用 fixture 项目复测（给 Computer Use 切到含"第 X 章 chxxx"标题的项目）
- 看 Round 6 R-2 commit 13cf6e9 / 545b896 是否把 ReferenceSuggestionSource 的调用路径破坏（grep ReferenceSuggestion 在 ConversationPanelViewModel 看是否还在 PopulateReferenceCandidates）
- 看 InputDraft @ 解析（OnInputDraftChanged）—— @ch 是否走到 PopulateReferenceCandidates

修法（按根因）：
- 数据源问题：修 SuggestAsync 让它能匹配章节 / 角色 / 世界
- 调用路径串到 staged → 恢复 reference 路径独立性
- 如果完全是 IME 拦截 → 当前 round 不修代码，在 巡检文档 R3 段标记"平台已知限制"+ 给出 workaround（先关 IME 候选条或用英文输入）

测试：
- 至少 1 条单测断言 SuggestAsync("ch") 在 fixture 项目下返回 ≥1 条 章节候选

实机验证：
- 复跑巡检 R3 步骤，用 fixture 项目敲 @ch，观察候选弹层

完成验收：
- dotnet build 0 W / 0 E
- dotnet test 全绿，新增 ≥4 条测试（R1 1 / R2 2 / R3 1）
- 巡检文档 R1/R2/R3 三段添加"已修复"标注 + 修复 commit 短哈希
- macOS bundle 脚本（Scripts/build-dev-bundle.sh）+ how-to 文档入仓
- Computer Use 重新跑同样 3 段步骤，全部通过

完成输出：
- 全部 commit + 标题
- Task 0-3 完成状态
- 巡检 R1/R2/R3 实机复跑结果（pass / 仍然 fail + 截图）
- 附带发现（如有）

不 push，不动 Lane 0/A/B/C 代码。
```

---

## Lane 0：4 个 macOS Sink DI 注册（前置，必先做）

### 给 codex 的提示词

```
你的任务：把 4 个未注册 DI 的 macOS 平台 sink 接到 Avalonia DI 容器，让平台能力实际跑起来。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Worktree：/Users/jimmy/Downloads/tianming-m7-lane0-sinks
分支：m7-lane0-sinks（基于 main）

工作流：

【步骤 0】写 plan
用 superpowers:writing-plans 写 step-level plan：
- 落到 Docs/superpowers/plans/2026-05-XX-tianming-m7-lane0-sinks.md
- 标 4 个 sink 各自一个 task

【步骤 1】MacOSSystemAppearanceMonitor 接 ThemeBridge

文件：src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs
- 在主题相关 DI 段（grep "PortableThemeStateController"）后注册：
  s.AddSingleton<MacOSSystemAppearanceParser>();
  s.AddSingleton<MacOSSystemAppearanceProbe>();
  s.AddSingleton<MacOSSystemAppearanceMonitor>();
- 用 AppHost startup 钩子启动 monitor，订阅系统外观变化 → 调 ThemeBridge.ApplyCore()
- ThemeBridge.ApplyCore() 现在硬编码 Light（App.axaml:16 + ThemeBridge.cs:45/54），改成根据 PortableThemeStateController 当前 type 输出 Light/Dark

测试：
- tests/Tianming.Desktop.Avalonia.Tests/Theme/ 新增 fake MacOSSystemAppearanceMonitor 单测
- 断言系统外观变 dark → ThemeBridge 收到事件 → Application.RequestedThemeVariant 切到 Dark

【步骤 2】MacOSNotificationSink 接 PortableNotificationDispatcher

类似步骤 1，注册 sink + 在 dispatcher 里挂接 sink
新增测试：dispatcher 调度通知时 sink 被调用

【步骤 3】MacOSSpeechOutput 接 PortableNotificationSoundPlayer

注册 sink，把 IPortableSpeechOutput 接到它
新增测试：sound player 调度语音播报时 speech output 被调用

【步骤 4】MacOSSystemMonitorProbe 接 PortableSystemMonitorService

注册 probe，让 service 通过 probe 拿真实数据
新增测试：service.RefreshAsync() 调用 probe.CollectAsync()

============== Lane R 复核遗留（必做，否则 Lane A/B/C 验收会反复卡） ==============

【步骤 5】Computer Use 工具闭环排查（关键，阻塞 Lane A/B/C 后续真机验收）

背景：Lane R 完成时 codex 报告 Computer Use 工具闭环失败 — list_apps 能看到
`Avalonia Application — dev.tianming.avalonia.manualtest [frontmost, running]`，
但 get_app_state 对 bundle id / 进程名 / 窗口名均返回 appNotFound。
只能用 macOS Accessibility 只读勉强确认窗口可见，无法包装为"真机复跑通过"。

后续 Lane A/B/C 每条都依赖 Computer Use 验收：必须先解掉这条铁路。

诊断步骤：
1. 确认 Scripts/build-dev-bundle.sh 输出的 .app 当前 Info.plist 字段：
   - CFBundleIdentifier ✓ dev.tianming.avalonia.manualtest（已验证）
   - 缺什么？plutil -p /tmp/TianmingDev.app/Contents/Info.plist
2. 与 Computer Use 能成功 attach 的别的 macOS app（如 Safari / Notes）的 Info.plist 字段对比：
   - LSUIElement / LSBackgroundOnly：是 background app 才需要
   - NSPrincipalClass：可能需要 NSApplication
   - CFBundleSignature / CFBundleDevelopmentRegion：常规字段
   - NSHumanReadableCopyright / CFBundleDisplayName：可读元数据
3. 检查 codesign 状态：Computer Use 可能要求 .app 是 codesign 过的（哪怕 ad-hoc）
   - codesign -dvv /tmp/TianmingDev.app 看签名状态
   - 试 codesign --force --deep -s - /tmp/TianmingDev.app 做 ad-hoc 签名
4. 检查 TCC 权限：Computer Use 需要 Accessibility / Screen Recording 等权限
   - 看 System Settings → Privacy & Security → Accessibility 是否含执行 Computer Use 的进程
5. 检查 Avalonia headless / background mode：
   - Avalonia 11 默认是否启用 NSBackgroundOnly？查 src/Tianming.Desktop.Avalonia/Program.cs
6. 若以上都正常但仍 appNotFound：尝试用 osascript 给 .app 一个非 default 的 window title，让 Computer Use 能匹配

修法（按真实根因）：
- 改 Scripts/build-dev-bundle.sh 加缺失的 Info.plist 字段 + ad-hoc 签名
- 文档化"Computer Use attach macOS Avalonia 应用所需的最少 Info.plist 字段集"到 Docs/macOS迁移/manual-test-howto.md

验收：
- 跑 Scripts/build-dev-bundle.sh 后用 Computer Use get_app_state(bundle id) 真返回 window tree（不是 appNotFound）
- 把 attach 成功的关键 plist 字段 + 签名状态写入 manual-test-howto.md

【步骤 6】NavigationBreadcrumbSource KnownLabels 与 PageRegistry/LeftNavViewModel 三处去重

背景：Lane R f13be48 加了 NavigationBreadcrumbSource.cs:10-33 KnownLabels 硬编码字典；
LeftNavViewModel:68 已有 "一键成书" 字面量；PageRegistry 也有 page 注册。三处重复
意味着加新 page 必须双写/三写，漏改即 breadcrumb fallback 到 raw id（这正是
巡检时 R1 的初始现象）。

修法：
- 在 PageRegistry 上加 DisplayName 属性（如 `Register<TVm, TView>(PageKey, displayName)`）
- NavigationBreadcrumbSource.GetLabel(key) 改为查 PageRegistry.GetDisplayName(key)，去掉 KnownLabels
- LeftNavViewModel 的 hard-code 文案改为引用 PageRegistry.GetDisplayName

新增测试：
- 加新 page A 注册时只填一处 DisplayName → breadcrumb 和 LeftNav 都正确显示该 DisplayName

【步骤 7】ModelManagementPage ComboBox ItemTemplate 显示 DisplayName

背景：Lane R fd25784 让 ApiKeysPage 显示友好 ProviderName 但 ModelManagementPage.axaml:20
ComboBox `ItemsSource="{Binding ProviderIds}"` 显示原始 id "openai"，与 ApiKeysPage 风格不一致。

修法：
- ModelManagementPage.axaml:20 改 ItemsSource 绑到 Providers (含 DisplayName) 而不是 ProviderIds
- 加 ItemTemplate `<TextBlock Text="{Binding DisplayName}" />`
- ModelManagementViewModel 暴露 `ObservableCollection<DefaultAIProviderOption> Providers` 与 ApiKeysViewModel 对齐

新增测试：
- ModelManagementViewModelTests 加 1 条：Providers 包含 DefaultAIProviderOption 且 DisplayName 不为 id

【步骤 8】R3 IME 拦截作为平台已知限制独立标注

背景：Lane R 6422510 只把 IME 拦截作为"待区分项之一"列入巡检文档，未独立标注。下次
Computer Use 仍会在同条件下复现且无法区分。

修法：
- 在 Docs/macOS迁移/M5-ComputerUse-功能巡检-2026-05-14.md R3 段下加独立子段"平台已知限制：macOS 中文输入法候选条"
- 内容包括：
  - 现象描述（IME 候选条可能视觉遮挡应用内 dropdown）
  - workaround：临时切到英文输入或关闭 IME 候选条
  - 长期方案 TODO：检查 Avalonia 11 是否支持 IME composition 事件让应用内 dropdown 显示在 IME 之上

============== 完成验收 ==============

- dotnet build 0 W / 0 E
- dotnet test 全绿，新增 ≥6 条测试（步骤 1-4 共 ≥4 + 步骤 6 至少 1 + 步骤 7 至少 1）
- 启动 app（用 Scripts/build-dev-bundle.sh 包装后启动，不要直接 dotnet run）
- Computer Use get_app_state(dev.tianming.avalonia.manualtest) 真返回 window tree
- Avalonia AppearanceVariant 切换日志可见（说明监听跑起来了）

完成输出：
- 全部 commit + 标题
- step 0-8 完成状态
- Computer Use 工具闭环验证关键截图或文本
- 启动日志关键行
- 附带发现（如有）

不 push，不动 Lane A/B/C 代码。
```

---

## Lane A：外观 / 主题 / 字体 / 配色 Settings（9 项）

涵盖矩阵 D 区块 #1-#9：主题管理 / 主题冲突解析 / 主题过渡 / 主题跟随 / 定时主题 / 字体管理 / 图片取色 / AI 配色 / 配色历史

### 给 codex 的提示词

```
你的任务：建立 Avalonia 端的 Settings Shell + 外观主题区子页，把矩阵 D 区块 #1-#9 共 9 项功能从 lib-only 升级到用户可见可控。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Worktree：/Users/jimmy/Downloads/tianming-m7-lane-a-appearance
分支：m7-lane-a-appearance（基于 Lane 0 merge 后的 main）

工作流：

【步骤 0】写 plan
用 superpowers:writing-plans 拆 step-level plan，落到：
  Docs/superpowers/plans/2026-05-XX-tianming-m7-lane-a-appearance.md

建议拆分：
  Task 1 Settings Shell（PageKeys.Settings 路由 + 子导航容器）
  Task 2 ThemeSettingsPage（含主题切换 / Auto/Light/Dark + 自定义主题选择）
  Task 3 ThemeFollowSystemPage（跟随系统 / 定时主题 / 排除时段）
  Task 4 ThemeTransitionPage（过渡动画设置）
  Task 5 FontSettingsPage（字体管理 + UI/编辑器字体 + 字体回退）
  Task 6 ColorSchemeDesignerPage（图片取色 + AI 配色生成 + 历史）

【步骤 1】Settings Shell（解掉 PlaceholderView）

文件：src/Tianming.Desktop.Avalonia/Navigation/PageRegistry.cs
- PageKeys.Settings 当前路由到 PlaceholderView，改成路由到新建的 SettingsShellView
- SettingsShellView 是个左侧子导航 + 内容容器（类似 ThreeColumnLayout 但更窄）
- 子导航条目暂留 6 个 placeholder（"外观主题" / "字体" / "配色设计器" / "通知" / "声音" / "系统"）—— Lane B/C 完成时填后 3 个

LeftNavView：在已有的"项目数据"组下加 "设置" 入口

【步骤 2】ThemeSettingsPage

VM：ThemeSettingsViewModel
  - ctor 接 PortableThemeStateController + ThemeBridge
  - ObservableProperty: SelectedThemeType (Light/Dark/Auto/Custom)
  - Command: SwitchTheme + ImportCustomTheme + ExportCurrentTheme
View：Views/Settings/ThemeSettingsPage.axaml
  - 三/四个 RadioButton（Auto/Light/Dark/Custom）
  - Custom 模式下显示 ListBox 列自定义主题文件
  - 应用按钮 → controller.RequestSwitch(...)

测试：ThemeSettingsViewModelTests
  - 切换到 Dark 后断言 controller 收到 RequestSwitch(Dark) + ThemeBridge.ApplyCore 被调（mock）

【步骤 3-6】其他 page（ThemeFollowSystem / ThemeTransition / Font / ColorScheme）按相同模式：

每个 page：
  - 新建 VM（ctor 注入对应 Portable*/File* lib）
  - 新建 axaml（按 lib 暴露的 settings 字段渲染 UI 控件）
  - 在 Settings Shell 子导航注册
  - DI 在 AvaloniaShellServiceCollectionExtensions 注册
  - 至少 2 条 VM 单测（一条加载默认设置、一条修改 + 保存）

【特别注意】
- 主题/字体/配色相关 lib 类繁多，**不要每个 Portable* 类都单独建一个 VM**——按"用户视角的 page"分组，VM 内组合多个 lib（如 ThemeFollowSystemViewModel 同时持有 PortableSystemFollowController + PortableThemeScheduleService + PortableHolidayLibrary）
- 主题预览：建议 Custom 主题应用前先 dry-run（不影响当前应用主题），用户确认再 Commit
- 字体页面：先做 UI 字体 + 编辑器字体两类，字体场景预设/性能分析/连字检测等高级功能放后续 milestone（plan 标 "deferred"）

完成验收：
- 启动 app，"设置 / 外观主题"页可见可点
- 切换主题真生效（Lane 0 已让 monitor 跑起来 → ThemeBridge 真切换）
- dotnet build 0 W / 0 E
- dotnet test 全绿，新增 ≥12 条测试（6 个 page × 2）

完成输出：
- 全部 commit + 标题
- Task 1-6 完成状态
- 启动 app 截图或日志关键行（设置入口可达性证明）
- 9 项矩阵能力的覆盖状态（哪几项做了/部分/deferred）
- 附带发现

不 push，不动 Lane B/C 代码。
```

---

## Lane B：通知 / 声音 / 勿扰 Settings（7 项）

涵盖矩阵 D 区块 #12-#18：Toast / 系统通知 / 系统集成 / 通知历史 / 勿扰 / 声音方案 / 语音播报

### 给 codex 的提示词

```
你的任务：建立 Avalonia 端通知/声音区子页，覆盖矩阵 D #12-#18 共 7 项。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Worktree：/Users/jimmy/Downloads/tianming-m7-lane-b-notifications
分支：m7-lane-b-notifications（基于 Lane 0 merge 后的 main）

前置：Lane A 已 merge（建立了 Settings Shell + 子导航容器）。Lane B 在此基础上填"通知"和"声音"两个子导航项。

工作流：

【步骤 0】写 plan
  Docs/superpowers/plans/2026-05-XX-tianming-m7-lane-b-notifications.md

建议拆分：
  Task 1 NotificationSettingsPage（Toast 样式 / 类型开关 / 通知历史 / 系统集成 macOS apply plan）
  Task 2 DoNotDisturbPage（勿扰模式 / 时间窗 / 全屏自动启用 / 例外应用）
  Task 3 SoundSchemePage（声音方案选择 / 内置方案预览 / 自定义音效库）
  Task 4 AudioDevicePage（输出/输入设备选择 / 主音量 / EQ）
  Task 5 VoiceBroadcastPage（语音播报开关 / 速度/音量/音调 / 测试播报）
  Task 6 NotificationHistoryPage（已读/未读 / 删除 / 清空 / 24h 统计）

【步骤 1-6】每个 page 按 Lane A 同款模式建：VM + axaml + 子导航注册 + DI + 测试

特别注意：
- Lane 0 已注册 MacOSNotificationSink + MacOSSpeechOutput → 测试通知/播报时可以验证真发声音（macOS osascript display notification / say 命令）
- 系统集成里的 LaunchAgent / Info.plist URL 协议在矩阵里被标"需平台接线" → 这一轮做"配置数据持久化 + 显示"，真实 LaunchAgent 安装放后续 milestone（plan 标 deferred）
- 通知历史是用户能看到的最容易测试的 → 优先做这个验证整个通知链路

完成验收：
- 设置 / 通知 / 勿扰 / 声音 三个子导航条目都可见可点
- 在通知页测试发一条通知 → Mac 真弹通知 + 通知历史新增一条 → 在通知历史页可见
- 设勿扰 → 同样测试发通知 → 通知被拦截 → 历史里记为 blocked
- dotnet build 0 W / 0 E
- dotnet test 全绿，新增 ≥14 条测试（6 个 page × ~2）

完成输出：同 Lane A 格式。

不 push，不动 Lane A/C 代码。
```

---

## Lane C：系统设置 / 监控 / 显示 / 语言（11 项）

涵盖矩阵 D 区块 #10、#11、#19-#25、#26、#27：
- UI 分辨率与缩放 / 加载动画
- 代理配置（VPN / 测试 / 链路健康）
- 日志系统（格式 / 级别 / 轮转 / 输出目标）
- 数据清理
- 系统信息 / 运行环境 / 诊断信息 / 系统监控
- 显示偏好 / 语言区域偏好

### 给 codex 的提示词

```
你的任务：建立 Avalonia 端系统/显示/语言/监控子页，覆盖矩阵 D #10/#11/#19-#27 共 11 项。

仓库根：/Users/jimmy/Downloads/tianming-novel-ai-writer
Worktree：/Users/jimmy/Downloads/tianming-m7-lane-c-system
分支：m7-lane-c-system（基于 Lane 0 merge 后的 main）

前置：Lane A 已 merge（建立了 Settings Shell + 子导航）。Lane C 填"系统"子导航项下的 N 个 tab。

工作流：

【步骤 0】写 plan
  Docs/superpowers/plans/2026-05-XX-tianming-m7-lane-c-system.md

建议拆分：
  Task 1 DisplaySettingsPage（UI 分辨率 + 加载动画 + 显示偏好 + 语言区域）
  Task 2 ProxyConfigurationPage（代理路由表 / 测试历史 / 链路健康）
  Task 3 LogSettingsPage（格式 / 级别 / 轮转 / 输出目标 + 测试)
  Task 4 DataCleanupPage（清理项扫描 / 选择项 / 执行）
  Task 5 SystemInfoDashboardPage（系统信息 + 运行环境 + 诊断 + 监控 4-tab 合并）

特别注意：
- 这一组每个 lib 数据模型都很大（如 PortableProxyChainSelector / PortableProxyChainAnalyzer / PortableProxyChainHealthController 三个一起；如 PortableLogService + PortableLogMaintenance + PortableLogFormatCore + PortableLogLevelCore + PortableLogRotationCore + PortableLogOutputPipeline 六个一起），建议每个 page 内分 tab 而非每个 lib 一个 page
- Lane 0 已注册 MacOSSystemMonitorProbe → 系统监控页可以显示真实 macOS CPU/内存/温度数据
- 代理已在 main 上"透明嵌入 HttpClient"，Lane C 加 UI 让用户能查看 + 切换代理链 + 测试出口 IP
- 数据清理是危险操作 → UI 必须做二次确认 + 显示"会删除什么"预览
- SystemInfoDashboard 这个 4-in-1 page 是用户最容易感知的 lane 价值产出，建议优先做

完成验收：
- 设置 / 系统 子导航 5 个 tab 都可见可点
- 系统信息页：能看到 macOS 真实硬件（Lane 0 接通 sysprofiler）
- 代理页：能切换代理链 + 一键测试连接
- dotnet build 0 W / 0 E
- dotnet test 全绿，新增 ≥10 条测试

完成输出：同 Lane A 格式。

不 push，不动 Lane A/B 代码。
```

---

## 派发顺序

```
Lane R (真机回归)  →  Merge  →  Lane 0 (sink DI)  →  Merge  →  Lane A (Settings Shell + 外观)  →  Merge  →  Lane B + Lane C 并行  →  Merge  →  push
```

| Lane | 工作量 | 风险 |
|---|---|---|
| Lane R | 2-4 小时 | 中（3 个产品 bug 根因不一定容易找；R3 可能是 IME 平台限制）— **已 merge（commit 9991d26 → main）**|
| Lane 0 | 8-12 小时 | 中（4 个 sink DI + 4 个 Lane R 复核遗留 task；Computer Use 工具闭环排查最不确定）|
| Lane A | 1.5-2 天 | 中（建 Settings Shell + 6 个 page） |
| Lane B | 1-1.5 天 | 中（通知/声音相对独立） |
| Lane C | 1.5-2 天 | 中-高（系统监控/代理/日志数据模型复杂） |

完成后我（主 thread）会：
1. 各 worktree 复跑 build/test
2. 派 review subagent 重新跑 27 项覆盖核查 — 期望 🟢 ≥20 / 🟡 ≤5 / 🔴 ≤2
3. 主 thread 用 Computer Use 重跑巡检 R1/R2/R3 三段步骤 + 抽测 Lane A/B/C 至少 1 个 settings page
4. 顺序 merge（按 Lane R → Lane 0 → A → B → C，同 Round 3/6 模式）
5. 清理 worktree + 本地分支
6. push origin main

---

## 不在 Round 7 范围（明确划线）

- **OAuth / 订阅 / 卡密 / 公告 / SSL pinning / 自动更新**（用户与服务端能力 E 区块）—— 按 user 准则属"真发布级"，v2.8.7 不做
- **M5 后留 M7 的发布级**：URL Scheme / 文件关联 / Sandbox / Notarization / DMG 制作 —— 移到独立 Lane（M8 打包分发）
- **保护系统**（反调试 / TMProtect）—— 已知 macOS 不复用，保持 "替换为空"
- **高级字体功能**（性能分析 / 连字检测 / 场景预设）—— Lane A 内标 deferred
- **真实 LaunchAgent 安装**（系统集成）—— Lane B 内标 deferred

---

## 已知遗留（跨 Round 累计，本轮可顺手解掉的）

- 主题"Light 占位"（App.axaml:16 + ThemeBridge.cs:45/54）—— **Lane 0 必修**
- `MacOSSystemAppearanceMonitor` 未注册 DI —— **Lane 0 必修**
- `OpenAICompatibleChatClient.BuildChatCompletionsUri` 缺 scheme 校验 —— Lane A / B 可顺手提示，但不在本轮 scope
- macOS 中文 IME 拦截 @候选 —— 平台限制，不在本轮 scope
