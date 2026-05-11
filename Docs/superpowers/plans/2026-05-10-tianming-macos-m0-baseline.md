# Tianming macOS M0 Baseline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Establish a repeatable macOS migration baseline for Tianming before any business-code porting begins.

**Architecture:** M0 is documentation, tooling, and evidence only. It records the current WPF/Windows state, verifies local .NET toolchain availability, makes Windows-specific bindings measurable, and prepares the first safe extraction lane for later cross-platform service work.

**Tech Stack:** .NET SDK, zsh/bash, ripgrep, Markdown, existing WPF/.NET 8 project files.

---

## Scope Check

The full objective spans UI migration, platform abstraction, AI services, project data, editor workflows, system integration, packaging, and optional service-backed account features. This M0 plan does not implement the macOS app. It creates the factual baseline and gates needed before M1 service-layer extraction.

## Files

- Existing: `Docs/macOS迁移/功能对齐审计与迁移设计草案.md`
  - Owns architecture recommendation, platform facts, migration phases, and risk boundaries.
- Existing: `Docs/macOS迁移/功能对齐矩阵.md`
  - Owns feature-by-feature parity status.
- Existing: `Scripts/check-windows-bindings.sh`
  - Owns repeatable scanning for Windows/WPF-specific references.
- Create: `Docs/macOS迁移/M0-环境与阻塞基线.md`
  - Records toolchain state, build attempt results, scan totals, and immediate blockers.
- Create: `Docs/macOS迁移/M0-服务层抽离候选清单.md`
  - Records the first service/model files that should move into cross-platform class libraries in M1.

## Task 1: Verify Toolchain Baseline

**Files:**
- Create: `Docs/macOS迁移/M0-环境与阻塞基线.md`

- [ ] **Step 1: Check whether .NET is available**

Run:

```bash
command -v dotnet || true
dotnet --info || true
```

Expected on the current machine before toolchain setup:

```text
dotnet not found
```

- [ ] **Step 2: Check common macOS .NET install paths**

Run:

```bash
for p in /usr/local/share/dotnet /usr/local/bin/dotnet /opt/homebrew/bin/dotnet /opt/homebrew/share/dotnet "$HOME/.dotnet/dotnet"; do
  if [ -e "$p" ]; then
    ls -ld "$p"
  fi
done
```

Expected on the current machine before toolchain setup:

```text
no output
```

- [ ] **Step 3: Record the baseline**

Create `Docs/macOS迁移/M0-环境与阻塞基线.md` with:

```markdown
# M0 环境与阻塞基线

日期：2026-05-10

## 目标

记录 macOS 迁移开始前的本机工具链、当前项目构建阻塞、Windows/WPF 绑定规模和下一步解除条件。

## 工具链状态

- `dotnet`：当前 shell 未检测到。
- 常见安装路径检查：未发现 `/usr/local/share/dotnet`、`/usr/local/bin/dotnet`、`/opt/homebrew/bin/dotnet`、`/opt/homebrew/share/dotnet` 或 `$HOME/.dotnet/dotnet`。
- 影响：当前无法执行 `dotnet build Core/App/天命.csproj -c Debug` 或后续跨平台类库构建验证。

## 当前构建阻塞

- 原项目是 WPF Windows 桌面项目：`Core/App/天命.csproj`
- 当前目标框架：`net8.0-windows10.0.19041.0`
- 当前 UI 框架：`UseWPF=true`
- 根据 Microsoft WPF 文档，WPF 只运行在 Windows；因此 macOS 不能直接运行当前 WPF UI。

## Windows/WPF 绑定扫描

- 扫描脚本：`Scripts/check-windows-bindings.sh`
- 当前匹配数：1289
- 当前涉及文件数：439

## 解除 M0 工具链阻塞的条件

- `dotnet --info` 可执行并显示 SDK 信息。
- 可以创建并构建一个最小 `net8.0` classlib。
- 可以记录原 WPF 项目在 macOS 上的真实构建失败输出。

## 进入 M1 的条件

- 工具链可用。
- Windows/WPF 绑定扫描脚本可运行。
- 功能对齐矩阵覆盖 README 与 `Docs/本地部署/开源说明.md` 中列出的主要功能。
- 服务层抽离候选清单已经明确首批文件。
```

- [ ] **Step 4: Verify the doc has no placeholder text**

Run:

```bash
rg -n "TBD|TODO|待补|占位|以后再" Docs/macOS迁移/M0-环境与阻塞基线.md
```

Expected:

```text
no matches
```

## Task 2: Verify Windows Binding Scanner

**Files:**
- Modify: `Scripts/check-windows-bindings.sh`
- Reference: `/tmp/tianming-windows-bindings.txt`

- [ ] **Step 1: Run the scanner**

Run:

```bash
Scripts/check-windows-bindings.sh > /tmp/tianming-windows-bindings.txt
tail -80 /tmp/tianming-windows-bindings.txt
```

Expected:

```text
Summary by file:
```

The summary should include files from `Core`, `Framework`, `Modules`, and `Services`.

- [ ] **Step 2: Count total matches and files**

Run:

```bash
pattern='net8\.0-windows|UseWPF|WindowsDesktop|System\.Windows|Microsoft\.Win32|Registry\.|RegistryKey|RegistryValueKind|OpenSubKey|CreateSubKey|DeleteSubKeyTree|DllImport|user32\.dll|kernel32\.dll|ntdll\.dll|wininet\.dll|dwmapi\.dll|TMProtect\.dll|System\.Management|System\.Speech|NAudio|WebView2\.Wpf|Microsoft\.Web\.WebView2|Microsoft\.Toolkit\.Uwp\.Notifications|Windows\.Forms|ProtectedData|Emoji\.Wpf|DiffPlex\.Wpf|Markdig\.Wpf'
printf 'total_matches='
rg -n --glob '*.cs' --glob '*.xaml' --glob '*.csproj' --glob '*.props' "$pattern" Core Framework Modules Services | wc -l | tr -d ' '
printf '\nfiles='
rg -l --glob '*.cs' --glob '*.xaml' --glob '*.csproj' --glob '*.props' "$pattern" Core Framework Modules Services | wc -l | tr -d ' '
printf '\n'
```

Expected current baseline:

```text
total_matches=1289
files=439
```

- [ ] **Step 3: If counts change, update `M0-环境与阻塞基线.md`**

Use the exact values from Step 2. Do not round the counts.

## Task 3: Create Service-Layer Extraction Candidate List

**Files:**
- Create: `Docs/macOS迁移/M0-服务层抽离候选清单.md`

- [ ] **Step 1: Generate candidate file lists**

Run:

```bash
{
  echo "# generation-validation-tracking"
  rg -l "class .*Service|interface I" Services/Modules/ProjectData -g '*.cs' | sort
  echo
  echo "# module-services"
  rg -l "class .*Service" Modules/Design Modules/Generate Modules/Validate -g '*.cs' | sort
  echo
  echo "# ai-core"
  rg -l "class .*Service|interface I" Services/Framework/AI Modules/AIAssistant -g '*.cs' | sort
} > /tmp/tianming-service-candidates.txt
sed -n '1,220p' /tmp/tianming-service-candidates.txt
```

Expected:

```text
# generation-validation-tracking
```

The output should include `Services/Modules/ProjectData/Implementations/Generation/GenerationGate.cs`.

- [ ] **Step 2: Create the candidate document**

Create `Docs/macOS迁移/M0-服务层抽离候选清单.md` with:

```markdown
# M0 服务层抽离候选清单

日期：2026-05-10

## 目的

为 M1 跨平台类库抽离选择第一批低风险文件。优先选择纯模型、纯 JSON 文件服务、生成门禁、校验、追踪和打包服务。暂不迁移 WPF View、WPF ViewModel、Windows 系统集成、登录保护系统和 WebView2 爬虫。

## 首批优先抽离

| 组 | 文件或目录 | 理由 | M1 验证方式 |
|---|---|---|---|
| 模型 | `Services/Modules/ProjectData/Models` | 多数是纯数据结构 | 新 classlib 编译通过 |
| 接口 | `Services/Modules/ProjectData/Interfaces` | 可先稳定服务边界 | 新 classlib 编译通过 |
| 生成门禁 | `Services/Modules/ProjectData/Implementations/Generation/GenerationGate.cs` | 核心闭环关键逻辑 | 用 fake CHANGES 输入做通过/失败测试 |
| AI 输出解析 | `Services/Modules/ProjectData/Implementations/Generation/AIOutputParser.cs` | 可独立测试协议解析 | 测试 `---CHANGES---` 与 JSON 缺失场景 |
| 事实快照 | `Services/Modules/ProjectData/Implementations/Tracking` | 维持章节状态连续性的核心 | 用最小章节变更数据测试状态更新 |
| 统一校验 | `Services/Modules/ProjectData/Implementations/Validation` | 核心质量门禁 | 用最小项目数据测试错误报告 |
| 索引服务 | `Services/Modules/ProjectData/Implementations/Indexing` | 多为文件/文本索引逻辑 | 用临时项目目录测试索引输出 |
| 打包发布 | `Services/Modules/ProjectData/Implementations/Packaging` | 生成任务包和发布包关键 | 用临时项目目录测试 manifest/历史输出 |
| 版本追踪 | `Services/Modules/VersionTracking` | 文件注册表逻辑相对独立 | 用临时项目目录测试 registry 读写 |

## 暂缓抽离

| 文件或目录 | 暂缓原因 |
|---|---|
| `Core/App` | WPF 应用生命周期和窗口强绑定 |
| `Framework/UI` | WPF 控件、窗口、工作区强绑定 |
| `Framework/Common/Controls` | WPF 控件和 XAML 强绑定 |
| `Framework/Common/ViewModels` | 大量调度和 UI 状态依赖，需要逐类判断 |
| `Framework/Notifications/SystemNotifications` | 注册表、UWP 通知和 Windows 系统集成 |
| `Services/Framework/SystemIntegration` | 托盘、Windows 通知、Win32 API |
| `Framework/Common/Services/ProtectionService.cs` | Windows 反调试、WMI、TMProtect.dll |
| `Framework/Common/Services/SslPinningHandler.cs` | TMProtect.dll 绑定，需要重写 |
| `Modules/Design/SmartParsing/BookAnalysis/Crawler` | WebView2.Wpf 绑定，需要替换 |

## M1 第一条实现线

1. 新建 `src/Tianming.ProjectData` 类库。
2. 迁移 `Services/Modules/ProjectData/Models` 和 `Services/Modules/ProjectData/Interfaces`。
3. 引入 `IProjectStorage` 抽象，替换直接依赖 `StoragePathHelper` 的服务。
4. 迁移 `AIOutputParser` 和 `GenerationGate`。
5. 新建测试项目，覆盖协议解析、引用校验和最小生成门禁失败路径。
```

- [ ] **Step 3: Verify the candidate document mentions concrete first-line files**

Run:

```bash
rg -n "GenerationGate|AIOutputParser|IProjectStorage|Tianming.ProjectData" Docs/macOS迁移/M0-服务层抽离候选清单.md
```

Expected:

```text
Docs/macOS迁移/M0-服务层抽离候选清单.md
```

## Task 4: Complete M0 Self-Review

**Files:**
- Review: `Docs/macOS迁移/功能对齐审计与迁移设计草案.md`
- Review: `Docs/macOS迁移/功能对齐矩阵.md`
- Review: `Docs/macOS迁移/M0-环境与阻塞基线.md`
- Review: `Docs/macOS迁移/M0-服务层抽离候选清单.md`
- Review: `Scripts/check-windows-bindings.sh`

- [ ] **Step 1: Check required M0 files exist**

Run:

```bash
for f in \
  "Docs/macOS迁移/功能对齐审计与迁移设计草案.md" \
  "Docs/macOS迁移/功能对齐矩阵.md" \
  "Docs/macOS迁移/M0-环境与阻塞基线.md" \
  "Docs/macOS迁移/M0-服务层抽离候选清单.md" \
  "Scripts/check-windows-bindings.sh"; do
  test -f "$f" && echo "ok $f"
done
```

Expected:

```text
ok Docs/macOS迁移/功能对齐审计与迁移设计草案.md
ok Docs/macOS迁移/功能对齐矩阵.md
ok Docs/macOS迁移/M0-环境与阻塞基线.md
ok Docs/macOS迁移/M0-服务层抽离候选清单.md
ok Scripts/check-windows-bindings.sh
```

- [ ] **Step 2: Scan M0 docs for placeholder language**

Run:

```bash
rg -n "TBD|TODO|待补|占位|以后再" Docs/macOS迁移 docs/superpowers/plans/2026-05-10-tianming-macos-m0-baseline.md
```

Expected:

```text
no matches
```

- [ ] **Step 3: Check scanner is executable**

Run:

```bash
test -x Scripts/check-windows-bindings.sh && echo executable
```

Expected:

```text
executable
```

- [ ] **Step 4: Record git status**

Run:

```bash
git status --short
```

Expected current changed files:

```text
?? Docs/macOS迁移/
?? Scripts/check-windows-bindings.sh
?? docs/superpowers/plans/2026-05-10-tianming-macos-m0-baseline.md
```

## Spec Coverage Self-Review

- The objective requires a macOS version with original feature parity. This plan does not claim completion; it creates the baseline needed to safely start that migration.
- The feature parity requirement is covered by `Docs/macOS迁移/功能对齐矩阵.md`.
- The platform feasibility requirement is covered by `Docs/macOS迁移/功能对齐审计与迁移设计草案.md`.
- The measurable Windows-binding baseline is covered by `Scripts/check-windows-bindings.sh` and `M0-环境与阻塞基线.md`.
- The first implementation lane is covered by `M0-服务层抽离候选清单.md`.
- Known gap: actual macOS build verification is blocked until the .NET SDK is installed or made available in PATH.

## Execution Handoff

Continue with inline execution for M0 because the tasks are documentation and local evidence gathering. Use subagent-driven development when M1 starts and independent extraction/test lanes can run in parallel.

