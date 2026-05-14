# Round 7 Lane C — 系统设置 page 覆盖矩阵 D #10/#11/#19-#27

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans

**Goal:** 在 Avalonia Settings Shell 加 "系统" 子导航项，建一个 SystemSettingsPage 含 11 个 SectionCard 覆盖矩阵 D 区块剩余 11 项框架能力。

**Architecture:** 沿用 Lane B 模式（1 page + 多 SectionCard）。VM ctor 注入 7-10 个 lib 类（settings data + store + 部分 controller），多数 Card 做 read-only 显示或简单 toggle；危险 / 复杂操作 (DataCleanup execute / Log pipeline / Proxy chain switch) deferred。

**Tech Stack:** Avalonia 11, CommunityToolkit.Mvvm, .NET 8, xUnit.

**Worktree:** `.claude/worktrees/m7-lane-c-system` (branch `worktree-m7-lane-c-system`)

---

## Scope (按 lib 能力分级)

| # | 项 | 实施级别 | Card 内容 |
|---|---|---|---|
| #10 UI 分辨率 | 完整 | UsePreset / WindowWidth / WindowHeight / ScalePercent toggle |
| #11 加载动画 | 简单 toggle | Enabled / Type / Position read/write |
| #19 代理配置 | read-only | 当前代理路由表条数 + 当前链 IP（probe）|
| #20 日志系统 | read-only | 当前日志级别 / 日志目录路径 |
| #21 数据清理 | placeholder | "扫描清理项" 按钮 + 风险提示 (不执行) |
| #22 系统信息 | read-only refresh | macOS 型号 / CPU / 内存 / 显示 (sysprofiler) |
| #23 运行环境 | read-only | .NET 版本 / RID / GC 模式 |
| #24 诊断信息 | read-only | 进程内存 / GC Gen0/1/2 / 线程数 |
| #25 系统监控 | 完整 read | CPU% / 内存使用率 / 磁盘 / 电池 (Lane 0 已 DI) |
| #26 显示偏好 | 完整 | ShowFunctionBar / ListDensity / UiScalePercent toggle |
| #27 语言区域 | 完整 | Language / TimeZone / DateFormat read/write |

**不做**：DataCleanup 真执行 / Log pipeline 编辑器 / Proxy chain 切换 UI / SystemInfo probe 自定义实现 — 全部 deferred 到后续 sub-plan，Card 内文字明确标注 "完整编辑器后续提供"。

---

## File Structure

### 新增
```
src/Tianming.Desktop.Avalonia/
  ViewModels/Settings/SystemSettingsViewModel.cs        # 主 VM
  Views/Settings/SystemSettingsPage.axaml(.cs)          # 11 SectionCard

tests/Tianming.Desktop.Avalonia.Tests/
  ViewModels/Settings/SystemSettingsViewModelTests.cs   # smoke + 写回测试
```

### 修改
```
Navigation/PageKeys.cs                            # +SettingsSystem
ViewModels/Settings/SettingsShellViewModel.cs     # 子导航加 "系统"
AvaloniaShellServiceCollectionExtensions.cs       # 补 lib DI + Page Register + AddTransient VM
App.axaml                                         # +DataTemplate
```

---

## Task 1: 补 lib DI 注册（10 类）

约定：每个 Settings 走 Singleton + 默认值 CreateDefault；每个 Store 接 AppPaths.AppSupportDirectory 子路径；Controller 走 sp factory。

```csharp
// Lane C: 系统设置相关 settings / store / controller，Singleton 持有用户配置
s.AddSingleton(_ => PortableUIResolutionSettings.CreateDefault());
s.AddSingleton(_ => PortableLoadingAnimationSettings.CreateDefault());
s.AddSingleton(_ => PortableDisplaySettings.CreateDefault());
s.AddSingleton(_ => PortableLocaleSettings.CreateDefault());
s.AddSingleton(_ => PortableRuntimeEnvironmentSettings { /* defaults */ });
s.AddSingleton<PortableDisplayController>(sp =>
    new PortableDisplayController(sp.GetRequiredService<PortableDisplaySettings>()));
s.AddSingleton<PortableLocaleController>(sp =>
    new PortableLocaleController(sp.GetRequiredService<PortableLocaleSettings>()));
```

Store 实例化（暂不强制 LoadAsync，让 settings 走 default 即可，启动时不阻塞）— 简化策略。

---

## Task 2: SystemSettingsPage VM + axaml + 注册

**VM 字段（按 11 项分组）：**
- #10/#26 显示组：WindowWidth, WindowHeight, ScalePercent, ShowFunctionBar, ListDensity
- #11 加载动画：LoadingEnabled, LoadingType
- #19 代理（read-only）：ProxyStatusText
- #20 日志（read-only）：LogLevelText, LogDirectoryPath
- #21 数据清理：HasScanResult, ScanResultText（ScanCommand）
- #22 系统信息：MacOsModel, ChipModel, MemorySize (RefreshCommand)
- #23 运行环境：RuntimeVersion, ClrVersion, GcMode
- #24 诊断：ProcessMemoryMb, ThreadCount, Gen0Count
- #25 系统监控：CpuUsage, MemUsage (RefreshCommand)
- #27 语言：Language, TimeZoneId, DateFormat

axaml 用 Expander 分 3 组：
- 显示与区域 (#10/#11/#26/#27)
- 网络与日志 (#19/#20)
- 系统状态 (#21/#22/#23/#24/#25)

---

## Task 3: 测试

5+ 条 smoke：
- Ctor_loads_initial_state
- Setting_ShowFunctionBar_writes_back_to_DisplaySettings
- Setting_Language_writes_back_to_LocaleSettings
- ScanDataCleanupCommand_populates_ScanResultText（用 fake controller / 简单 verify）
- RefreshSystemMonitorCommand_updates_CpuUsage（用 stub probe）

---

## Self-Review

- Spec coverage: 11/11 项有 SectionCard，符合 Lane C 派单要求
- Placeholder scan: 明确标 "deferred" 的项不算 placeholder，是 scope 决定
- Type consistency: ctor 签名 + 字段名都已 grep 验证（Display/Locale Controller 接 settings + clock；FileLocale/Display Store 接 path + clock；UIResolution/LoadingAnimation Store 接 filePath）
- Step granularity: Task 1 (~10 DI) + Task 2 (VM + axaml + DataTemplate + Register + DI VM) + Task 3 (~5 测试)，合理可控
