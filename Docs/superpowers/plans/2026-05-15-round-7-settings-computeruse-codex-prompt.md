# Round 7 Settings 真机巡检 Codex 派单提示词

> **目标**：codex 用 Computer Use 在 macOS Avalonia 真窗口逐项手测 Round 7 落地的 Settings 中心（4 个子导航：外观主题 / 跟随系统 / 通知 / 系统），并记录可见 bug / 实施级别现实落差到一份巡检文档。
>
> **配套**：
> - Round 7 全 Lane 落地状态：matrix D 区块 27 项已全部至少有 UI 入口（18 完整 + 9 read-only/占位）
> - 已知工具流：`Scripts/build-dev-bundle.sh` + `Docs/macOS迁移/manual-test-howto.md`（Lane R / Lane 0 落地）
> - Lane R 真机巡检模板：`Docs/macOS迁移/M5-ComputerUse-功能巡检-2026-05-14.md`（输出参考）

---

## 仓库根 + 启动准备

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer
git log -1 --oneline main      # 应看到 7442ac9 Merge Lane C 或更新
```

main 状态前置确认：
- `dotnet build Tianming.MacMigration.sln --nologo -v minimal` → 0 W / 0 E
- `dotnet test Tianming.MacMigration.sln --nologo -v minimal` → 1650 passed / 0 failed

启动 bundle：
```bash
bash Scripts/build-dev-bundle.sh
plutil -p /tmp/TianmingDev.app/Contents/Info.plist | head -10
codesign -dvv ~/Applications/TianmingDev.app 2>&1 | head -6
open ~/Applications/TianmingDev.app
```

Computer Use attach gate：
```
list_apps  → 应看到 "Avalonia Application — dev.tianming.avalonia.manualtest"
get_app_state("dev.tianming.avalonia.manualtest") → 应返回 accessibility 树
```

若 `get_app_state` 仍 `appNotFound`，按 `Docs/macOS迁移/manual-test-howto.md` 的 "Fresh profile 兜底" 段先跑 lsregister。

---

## 巡检范围（必须全覆盖）

### 0. 入口可达性（前置）
- 启动 → 默认页（Welcome / Dashboard）正常可见
- 点 LeftNav "工具" 组 → "设置" 子项 → 中心区切到 SettingsShellView
- 验证 SettingsShellView 左侧子导航 4 项可见：外观主题 / 跟随系统 / 通知 / 系统
- 默认 SelectedItem = 外观主题 → 右侧 ContentControl 渲染 ThemeSettingsPage

### 1. 外观主题（ThemeSettingsPage，Lane A）
- ComboBox 显示 3 选项：Auto / Light / Dark
- 当前选中应等于 PortableThemeStateController.CurrentTheme（默认 Light）
- **关键交互**：选 Dark → 点"应用" 按钮 → 窗口主题变深色（Lane 0 ThemeBridge 真切）
- 切回 Auto → 应跟随系统外观（macOS 系统外观当前是什么就用什么）
- "内置主题预设" SectionCard 占位文字可见（read-only）

### 2. 跟随系统（ThemeFollowSystemPage，Lane A）
- 两段 SectionCard 可见：
  - "跟随系统外观：已启用..."
  - "定时主题：未启用..."
- read-only 文本可读，无可交互按钮（这是 MVP placeholder，lib 缺 toggle API）

### 3. 通知（NotificationsPage，Lane B）
7 个 SectionCard，逐项验证：

**#15 通知历史**
- 显示空状态 "还没有通知记录"（如果 history 为空）
- 点"刷新" → 不应报错
- 点"清空" → 不应报错

**#13 系统通知**
- 显示 "当前 sink: MacOSNotificationSink"
- **关键**：点"发测试通知" → macOS 通知中心应弹出 "Tianming Test" + "测试通知 @ HH:mm:ss"
- 同时 #15 通知历史应自动出现一条新记录

**#16 勿扰**
- 默认状态 "免打扰已关闭"
- 点"切换" → 状态变 "免打扰已启用"
- 再点 → 切回 关闭

**#17 声音方案**
- 显示 "当前方案: default"
- 占位文案可见（read-only）

**#18 语音播报**
- "启用语音播报" checkbox
- 速度 NumericUpDown (默认 0)
- 音量 NumericUpDown (默认 100)
- 修改任意字段不应报错（写回到 data class）

**#12 Toast 样式**
- 圆角 NumericUpDown (默认 8)
- 显示 "屏幕位置: BottomRight" read-only

**#14 系统集成**
- 3 个 checkbox：启用系统通知 / 显示菜单栏图标 / 开机自动启动
- 各 checkbox 默认状态从 lib 加载
- 点切换不应报错

### 4. 系统（SystemSettingsPage，Lane C）
3 个 Expander 分组：

**显示与区域**（默认展开）
- #10 UI 分辨率：宽度/高度/缩放 NumericUpDown 显示默认值（1920/1080/100）
- #11 加载动画：状态文本可见
- #26 显示偏好：功能栏 checkbox + 列表密度 read-only
- #27 语言区域：4 个字段（语言/时区/日期格式/24 小时制）

**网络与日志**（默认折叠）
- 点击 Expander Header → 展开
- #19 代理：状态文本可见
- #20 日志：路径 + 后续提示

**系统状态**（默认折叠）
- 展开后看到 5 个 Card：
- #21 数据清理：扫描结果文本 + "扫描" 按钮
  - 点"扫描" → 文本应变为 "扫描完成：..."（占位实现）
- #22 系统信息：操作系统 / 机器名 / CPU 核心数（真 macOS 数据）+ 刷新按钮
- #23 运行环境：.NET 版本 / Framework / RID
- #24 诊断信息：进程内存 / 线程数 / Gen 0 GC + 刷新按钮
- #25 系统监控：状态文本

---

## 已知遗留（不在巡检范围，提到不算 bug）

参考 Round 7 各 Lane commit 已明确 deferred：
- 主题切换后控件 ControlTheme 真跟随（PalettesLight 仍硬编码，Lane A 已记）
- UIResolution 真生效到 Avalonia Window 尺寸（Lane C 已记）
- DataCleanup 真执行 / Log 完整编辑器 / Proxy chain 切换 / SystemInfo macOS sysprofiler probe（Lane C 已记）
- 字体 / 配色取色 / AI 配色 / 配色历史（Lane A 阶段 2 deferred）
- 主题过渡动画 / 完整 schedule 编辑器（Lane A deferred）

---

## 巡检顺带回归（确保 Lane R 修复仍生效）

Lane R 修复了 3 个真机回归 bug，本次巡检顺带确认仍 OK：

1. **R1 一键成书**：LeftNav 点 "一键成书" → breadcrumb 显示 "一键成书"（不是 raw "book.pipeline"）+ 中心区切到 BookPipelinePage
2. **R2 AI Provider 下拉**：LeftNav AI 管理 → "模型" → Provider 下拉应能看到至少 6 个 provider（OpenAI/Anthropic/Google/...）
3. **R3 @ 引用建议**：右侧对话面板 + 当前项目有"第 N 章"内容时，输入 `@ch` 应能弹出候选（若 macOS 中文 IME 候选条遮挡，记为已知平台限制）

---

## 巡检文档产物

**输出位置**：`Docs/macOS迁移/M7-Settings-ComputerUse-巡检-2026-05-15.md`

**结构**（参考 Lane R 同款文档结构）：

```markdown
# 天命 macOS/Avalonia M7 Settings Computer Use 真机巡检

日期：2026-05-15 HH:MM CST
工作区：/Users/jimmy/Downloads/tianming-novel-ai-writer
main tip：[当时 git log -1]

## 环境与基线
- .NET SDK / runtime / OS / git status

## 启动方式
- Scripts/build-dev-bundle.sh 输出
- plutil + codesign 关键字段
- Computer Use list_apps / get_app_state 返回片段

## 手测结果
| 区域 | 操作 | 结果 |
|---|---|---|
| Settings 入口 | 点 LeftNav "设置" | 通过/失败 + 现象 |
| 外观主题 ComboBox | 显示 3 选项 | 通过/失败 + 现象 |
| ... 完整覆盖 4 个子项的所有可观察点 ... |

## 风险与后续修复项
（每项一段：复现 / 观察 / 影响 / 建议）

## 未覆盖项
（明确说明哪些没测 + 原因）

## 结论
（一段总结：哪些通过 / 哪些有问题 / 整体可用性评级）
```

---

## 验收

巡检产物质量要求：
- ≥30 行手测结果表（4 Settings 子项 + 入口可达性 + 顺带回归 = 至少 30 个可观察点）
- 每个 ❌/⚠️ 项有具体复现步骤 + 现象描述
- 不要伪造 ✓ — Computer Use 工具闭环失败时（如 list_apps 看到但 get_app_state appNotFound）必须诚实标"工具未闭环，依赖 Accessibility 只读确认"
- 已知遗留项不计为 bug（在文档明确标）

不要 push。完成后输出：
- 巡检文档路径 + 一行总结
- 发现的 N 个 bug + 严重度评级
- main 是否需要顺手修（极小修可顺带 fix；中等 / 大 fix 留下次 sub-plan）

---

## 上下文：Round 7 Settings 总览

| Lane | 子导航条目 | sub-page | Card 数 | 数据源 |
|---|---|---|---|---|
| Lane A | 外观主题 | ThemeSettingsPage | 2 cards (ComboBox + 预设占位) | PortableThemeStateController |
| Lane A | 跟随系统 | ThemeFollowSystemPage | 2 cards (read-only) | placeholder |
| Lane B | 通知 | NotificationsPage | 7 cards | dispatcher / sink / sound options / dnd / toast / sysIntegration |
| Lane C | 系统 | SystemSettingsPage | 11 cards in 3 Expanders | UIResolution / LoadingAnim / Display / Locale / RuntimeEnv / SystemMonitor |

共 4 sub-page + 22 SectionCard 需巡检。
