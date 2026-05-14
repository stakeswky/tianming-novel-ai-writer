# 天命 macOS/Avalonia Computer Use 功能巡检记录

日期：2026-05-14 15:15-15:25 CST
工作区：`/Users/jimmy/Downloads/tianming-novel-ai-writer`
测试方式：终端 build/test 基线 + Computer Use 真实窗口手测

## 目标与验收口径

本轮目标是启动并手动测试 Tianming Novel AI Writer macOS/Avalonia 版本，确认主要可见功能是否正常工作，并记录明确证据与后续风险。

验收项：

- build/test 基线必须先过。
- macOS/Avalonia app 必须能启动到真实窗口，并可被 Computer Use 读取和操作。
- 左侧导航主模块需要逐项检查可见页面或明确未实现状态。
- 右侧 AI 对话面板需要检查 mode 切换、输入、发送、历史、新会话。
- AI 管理页、打包/备份等项目文件相关流程只做安全读/可见性检查，不输入真实密钥，不触发 destructive 操作。
- 记录所有失败、弱验证或未覆盖项。

## 环境与基线

- `.NET SDK`: 8.0.420
- runtime: Microsoft.NETCore.App 8.0.26
- OS: Mac OS X 26.0, `osx-arm64`
- Git 状态：测试前 `main...origin/main`，工作树干净；记录生成前 `git status --short` 为空。

命令证据：

```text
dotnet build Tianming.MacMigration.sln
Build succeeded.
0 Warning(s)
0 Error(s)
Time Elapsed 00:00:08.01
```

```text
dotnet test Tianming.MacMigration.sln
Tianming.ProjectData.Tests: 405 passed
Tianming.AI.Tests: 185 passed
Tianming.Framework.Tests: 791 passed
Tianming.Desktop.Avalonia.Tests: 234 passed
Total: 1615 passed, 0 failed, 0 skipped
```

## 启动方式

直接 `dotnet run --project src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj` 可以启动进程，但 macOS 给出的 visible process 没有 `CFBundleIdentifier`，Computer Use 无法 attach。

为完成真实 UI 手测，本轮在 `/tmp/TianmingDev.app` 临时封装 Debug 输出，并以 bundle id `dev.tianming.avalonia.manualtest` 启动。Computer Use 随后成功读取窗口：

```text
App=dev.tianming.avalonia.manualtest
Window: "天命", App: Avalonia Application.
```

状态栏可见：

- `.NET 8.0.26`
- `本地写作模式`
- `Keychain`
- `ONNX`

## 手测结果

| 区域 | 操作 | 结果 |
| --- | --- | --- |
| 启动/壳层 | 打开 app 窗口 | 通过。左侧导航、右侧 AI 对话面板、底部状态栏可见。 |
| Welcome | `Cmd+N` / `Cmd+O` | 通过。均导航到欢迎页；欢迎页显示新建项目、打开项目、最近项目和默认存储路径。 |
| Dashboard | 点击「仪表盘」 | 通过。显示项目标题、统计卡、章节进度环、近期活动。 |
| Editor | 点击「草稿」 | 通过。显示章节列表、元数据和编辑区域，占位章节为「未命名章节」。 |
| 设计页 | 点击「世界观」「角色」 | 通过。显示对应规则页、分类/条目/表单三列结构。未执行新增/删除。 |
| 生成页 | 点击「战略大纲」 | 通过。显示大纲数据页和对应字段表单。 |
| 章节生成管道 | 点击「章节生成管道」 | 通过可见性检查。显示 5 段管道布局、开始生成按钮和 disabled 的应用到章节按钮。未触发生成。 |
| AI 模型 | 点击「模型」 | 部分通过。页面显示添加模型和已配置模型区；Provider 下拉打开后没有可见选项，见风险 R2。 |
| API 密钥 | 点击「API 密钥」 | 部分通过。页面显示 Provider/API Key/保存 Key；未输入真实密钥。Provider 选择同样疑似无选项，见风险 R2。 |
| 提示词 | 点击「提示词」 | 通过。页面显示提示词模板区和「新建」按钮；未创建模板。 |
| 使用统计 | 点击「使用统计」 | 通过。显示今日请求/Tokens/费用/成功率、7 天趋势、模型汇总。 |
| 一键成书 | 点击「一键成书」 | 失败。breadcrumb 变为 `book.pipeline`，但中心内容仍停留在「使用统计」页，见风险 R1。 |
| 打包 | 点击「打包」 | 通过可见性检查。显示预检提示、导出 ZIP、备份+恢复入口；未导出、不创建快照。 |
| 设置 | 点击「设置」/ `Cmd+,` | 通过当前占位状态。中心显示「此页面将在 M4 实装。」 |
| 右侧对话 mode | 点击 `Plan` | 通过。Segment 视觉状态切换。 |
| 右侧输入/发送 | 输入 `manual qa ping` 后发送 | 可交互链路通过；由于没有已启用模型配置，返回可读错误：`InvalidOperationException: No enabled UserConfiguration matches purpose Writing and no enabled default-purpose configuration was found.` |
| 右侧历史 | 点击「历史」 | 通过可见性检查。会话历史区域打开，当前无持久化历史条目。 |
| 右侧新会话 | 点击「新会话」 | 通过。当前消息列表清空，输入框复位。 |
| @ 引用 | 新会话后输入 `@ch` | 弱验证/未通过。未显示引用建议；可能是当前项目无匹配数据，也可能是建议源未返回 UI 候选，见风险 R3。 |

## 风险与后续修复项

R1. `book.pipeline` 导航状态与中心内容不同步。

- 复现：先进入「使用统计」，再点左侧「一键成书」。
- 观察：breadcrumb 更新为 `book.pipeline`，但中心内容仍是使用统计页。
- 影响：一键成书入口不可用或展示错误页面。
- 建议：检查 `NavigationService` 的页面切换绑定、`BookPipelineView`/`BookPipelineViewModel` 注册、以及内容宿主是否复用旧 DataContext。
- Lane R 处理：已修复/锁定（commit `f13be48`）。新增测试确认 `AIUsage -> BookPipeline` 后中心区为 `BookPipelineViewModel`；实际代码层中心切换已通过，补充修复了 breadcrumb raw id，`book.pipeline` 改为用户可见「一键成书」。

R2. AI Provider 下拉无可见选项。

- 复现：进入「模型」页，点击 Provider 下拉；进入「API 密钥」页也有同类 Provider 控件。
- 观察：下拉弹层出现，但没有可选 provider 文本。
- 影响：用户无法配置模型或密钥，因此对话发送只能返回「无启用模型配置」错误。
- 建议：检查 `ModelManagementViewModel` / `ApiKeysViewModel` 的 provider source 初始化与 ComboBox Items 绑定。
- Lane R 处理：已修复（commit `fd25784`）。空模型库目录下 `ModelManagementViewModel.ProviderIds` 和 `ApiKeysViewModel.Providers` 会回退到内置 provider 列表，首屏即可看到 OpenAI/Anthropic/Google/Azure OpenAI/DeepSeek/Cherry Studio 选项。

R3. 右侧对话 `@` 引用建议未显示。

- 复现：新会话后在输入框输入 `@ch`。
- 观察：输入保留，但未出现引用候选。
- 影响：项目数据引用入口可能不可见；本轮不能确认是无匹配数据还是弹层/数据源问题。
- 建议：增加一个已知 fixture 或使用现有项目中文关键词复测，并确认 `ReferenceSuggestionSource.SuggestAsync` 是否返回候选。
- Lane R 处理：代码层已排除数据源问题（commit `6422510`）。新增 fixture 测试确认项目内存在 `ch001` 章节时，`ReferenceSuggestionSource.SuggestAsync("ch")` 返回 Chapter 候选；原巡检仍需要用同类 fixture 项目做 Computer Use 实机复跑，以区分无匹配数据、输入法候选条遮挡或弹层视觉问题。

### 平台已知限制：macOS 中文输入法候选条

- 症状：右侧对话框输入 `@` 或拼音片段时，macOS 中文输入法候选条可能覆盖 Avalonia 弹层区域，使引用建议看起来没有出现；这与 `ReferenceSuggestionSource` 是否返回候选是两个独立问题。
- 临时 workaround：做 Computer Use 回归时切换到英文输入法，或先关闭中文输入法候选条，再输入 `@ch` / fixture 关键词复测。
- 长期跟踪：复查 Avalonia 11 在 macOS 下的 IME composition event 与 popup/dropdown layering 行为，确认引用建议弹层是否需要避让 IME candidate window。

## 未覆盖项

- 未测试真实 AI provider 调用：本机没有已启用 Writing/default 配置，且本轮不输入真实 API Key。
- 未测试 Keychain 持久化：没有写入真实密钥。
- 未执行导出 ZIP / 创建快照 / 新建项目 / 删除或清除最近记录，避免修改项目数据或本地状态。
- 未点击 `Cmd+Q` 退出；关闭由测试进程清理完成即可。

## 结论

本轮 macOS/Avalonia 版本可以启动，build/test 基线全绿，主要壳层、欢迎页、仪表盘、草稿、设计页、生成页、AI 管理页、打包页与右侧对话面板都有可见交互路径。

不应标记为「各种功能都正常」：至少 `一键成书` 导航内容不同步、AI provider 下拉无选项、`@` 引用建议未显示/未证实，是需要进入后续修复或复测的风险。
