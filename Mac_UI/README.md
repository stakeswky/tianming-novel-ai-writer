# Mac UI 界面参考

这些图片是 macOS / Avalonia 迁移阶段的界面参考图。每张图都对应 M3-M6 计划里的实际页面或明确预留入口，伪代码放在 `pseudocode/` 目录。

| 序号 | 界面 | 图片 | 伪代码 | 对应计划 |
|---|---|---|---|---|
| 01 | 欢迎 / 项目选择 | `images/01-welcome-project-selector.png` | `pseudocode/01-welcome-project-selector.md` | M3 WelcomeView |
| 02 | 主工作区三栏 | `images/02-main-workspace-three-column.png` | `pseudocode/02-main-workspace-three-column.md` | M3 MainWindow / ThreeColumnLayout |
| 03 | 设计模块数据编辑 | `images/03-design-module-data-editor.png` | `pseudocode/03-design-module-data-editor.md` | M4.1 CategoryDataPageView |
| 04 | 生成规划 | `images/04-generation-planning.png` | `pseudocode/04-generation-planning.md` | M4.2 Outline / Volume / Chapter / ContentConfig |
| 05 | 章节 Markdown 编辑器 | `images/05-chapter-markdown-editor.png` | `pseudocode/05-chapter-markdown-editor.md` | M4.3 EditorWorkspaceView |
| 06 | 章节生成管道 | `images/06-chapter-generation-pipeline.png` | `pseudocode/06-chapter-generation-pipeline.md` | M4.4 + M6.2/M6.3 预留 |
| 07 | AI 对话面板 | `images/07-ai-conversation-panel.png` | `pseudocode/07-ai-conversation-panel.md` | M4.5 + M6.7 预留 |
| 08 | 统一校验报告 | `images/08-unified-validation-report.png` | `pseudocode/08-unified-validation-report.md` | M6.4 Validation |
| 09 | AI 模型 / API Key / 用量 | `images/09-ai-models-api-key-usage.png` | `pseudocode/09-ai-models-api-key-usage.md` | M4.6 + M5 Keychain + M6.6 |
| 10 | macOS 偏好 / 平台 | `images/10-macos-preferences-platform.png` | `pseudocode/10-macos-preferences-platform.md` | M5 Platform |

说明：

- 图片是视觉参考，不代表所有按钮在当前阶段都可用。
- 伪代码里的 `disabledUntil(...)` 表示该控件可作为占位显示，但功能要等对应里程碑完成。
- M3-M5 优先保证 macOS 自用闭环；M6 再补 v2.8.7 写作内核能力。

> M6 已写 spec（v2.8.7 写作内核升级，**不是发布级，是后续版本更新**）：`Docs/superpowers/specs/2026-05-12-tianming-m6-v287-core-upgrade-design.md`。06 / 07 / 08 / 09 图里 `disabledUntil(M6)` 占位都对应 M6 实装能力，不是"以后删掉的功能"。
