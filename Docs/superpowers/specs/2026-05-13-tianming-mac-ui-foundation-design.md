# 天命 macOS 迁移 — Mac UI 视觉基建（Sub-plan 1）设计

日期：2026-05-13
分支：`m2-m6/specs-2026-05-12`（同步副本入 `main`）
依据：`Mac_UI/` 视觉参考素材（10 张参考图 + pseudocode），`Docs/superpowers/specs/2026-05-12-tianming-m3-avalonia-shell-design.md`
定位：**个人自用 macOS** 项目；Mac UI 视觉对齐 3 分块计划里的第一块 — 视觉基建

## 0. 上下文与范围

### 0.1 整体计划（3 个 Sub-plan）

1. **Sub-plan 1（本 spec）— 视觉基建**：design tokens、自定义 chrome、底部状态栏、NativeMenu stub、14 个 primitives、3 个 NuGet 依赖整合、Mac_UI 入仓
2. **Sub-plan 2（待 brainstorm）— M3 视觉重做**：基于 Sub-plan 1 的 tokens + primitives 重写 `WelcomeView` / `LeftNav` / Dashboard center / `RightConversation`，让 M3 跑起来 = 参考图 01/02
3. **Sub-plan 3（待 brainstorm）— Docs + Plans 对齐**：把 `Mac_UI/` 嵌入 M3/M4/M5/M6 spec 与 M4/M5 plan，作为视觉真值源

本 spec 只覆盖 Sub-plan 1。Sub-plan 2、3 在本 sub-plan 完工后各自 brainstorm。

### 0.2 视觉保真度目标

**像素级**。所有 design tokens、间距、圆角、阴影、字体、组件结构都要按 `Mac_UI/images/*.png` 还原。色值采样精度 ≤ ΔE5；间距偏差 ≤ 4px。验证方式为人工逐项比对（Sub-plan 2 Done Definition）。

### 0.3 决策汇总（已 brainstorm 锁定）

| 编号 | 决策 | 理由 |
|---|---|---|
| Q1 | 亮色 only，删 `PalettesDark.axaml` | 参考图全亮色，个人自用 macOS 主用亮色，工作量砍半 |
| Q2 | 图表用 LiveCharts2（NuGet） | 进度环 / 饼图 / 柱状 / heatmap / stacked bar 全覆盖；SkiaSharp 已是 Avalonia 隐式依赖 |
| Q3 | Chrome 用 `ExtendClientAreaToDecorationsHint` | macOS pixel-level 上唯一不违反 HIG 的方案，保留原生 traffic light 行为 |
| Q4 | NativeMenu stub 在 Sub-plan 1 挂 | 让顶部 macOS 菜单栏立即可见，命令绑定留给 M5 |
| Q5 | Mac_UI 两边入仓（specs + main） | 实施时 main 上需直接读参考图；spec 引用时也方便 |
| Q6 | 14 个 primitives 一次性出全套 | pixel-level 一致性要求"先有 primitive"，避免后续每个页面重写造成样式漂移 |
| Q7 | primitives 用 `TemplatedControl` + ControlTheme | 比 UserControl 轻、纯样式驱动、对 binding 友好 |
| Q8 | 图标用 Lucide（`Projektanker.Icons.Avalonia.Lucide`） | 走 string token，所有 primitive 的 IconGlyph 统一接口 |
| Q9 | CodeViewer 用 AvaloniaEdit 包 | M4.3 章节编辑器迟早要装这个包，提前装减少未来 NuGet diff |
| Q10 | 不引入 visual regression 自动化测试 | 个人自用，macOS Retina 截图字节级 diff 噪音太大；肉眼比对更靠谱 |

## 1. 架构与文件布局

```
src/Tianming.Desktop.Avalonia/
├── Theme/
│   ├── DesignTokens/
│   │   ├── Colors.axaml         从参考图采样的色板（亮色）
│   │   ├── Typography.axaml     字号 / 行高 / 字重 / 字族
│   │   ├── Spacing.axaml        4/8/12/16/24/32 间距体系
│   │   ├── Radii.axaml          4/6/8/12/16 圆角体系
│   │   └── Shadows.axaml        卡片阴影 3 档（sm/md/lg）
│   ├── ControlStyles/
│   │   ├── Button.axaml         Primary/Secondary/Ghost/Icon 4 个 Style
│   │   ├── TextBox.axaml
│   │   ├── ComboBox.axaml
│   │   ├── ListBox.axaml
│   │   ├── DataGrid.axaml
│   │   ├── ScrollBar.axaml      macOS 风薄滚动条
│   │   └── LiveCharts.axaml     LiveCharts2 默认配色 / 字体
│   ├── PalettesLight.axaml      亮色 only，删原 PalettesDark.axaml
│   ├── CommonStyles.axaml       引入上述所有
│   └── AvaloniaEditBootstrap.cs AvaloniaEdit 字体 / 高亮初始化
├── Controls/                    14 个自定义 primitives
│   ├── StatsCard.axaml(.cs)
│   ├── BadgePill.axaml(.cs)
│   ├── SectionCard.axaml(.cs)
│   ├── NavRail.axaml(.cs)
│   ├── BreadcrumbBar.axaml(.cs)
│   ├── StatusBarItem.axaml(.cs)
│   ├── ProjectCard.axaml(.cs)
│   ├── SearchBox.axaml(.cs)
│   ├── SegmentedTabs.axaml(.cs)
│   ├── SidebarTreeItem.axaml(.cs)
│   ├── ConversationBubble.axaml(.cs)
│   ├── ToolCallCard.axaml(.cs)
│   ├── CodeViewer.axaml(.cs)
│   └── DataGridRowCell.axaml(.cs)
├── Shell/
│   ├── AppChrome.axaml(.cs)     自定义窗口 chrome 顶栏
│   ├── AppChromeViewModel.cs
│   ├── AppStatusBar.axaml(.cs)  底部状态栏容器
│   ├── AppStatusBarViewModel.cs
│   └── AppNativeMenu.axaml      NativeMenu stub（包含在 App.axaml）
├── Infrastructure/              已有 AppLifecycle / AppPaths / WindowStateStore / DispatcherScheduler
│   ├── IBreadcrumbSource.cs                  ← 新增
│   ├── NavigationBreadcrumbSource.cs         ← 新增
│   ├── IRuntimeInfoProvider.cs               ← 新增
│   ├── RuntimeInfoProvider.cs                ← 新增
│   ├── IKeychainHealthProbe.cs               ← 新增
│   ├── KeychainHealthProbe.cs                ← 新增
│   ├── IOnnxHealthProbe.cs                   ← 新增
│   └── OnnxHealthProbe.cs                    ← 新增
├── ViewModels/                  现有 + MainWindowViewModel 扩展加 Chrome / StatusBar 属性
├── Views/                       现有 MainWindow.axaml 重写为新 chrome 结构
├── App.axaml                    引用 Theme/* + NativeMenu stub + LucideIconStyles
├── App.axaml.cs                 启动时 LiveChartsSettings + AvaloniaEditBootstrap
└── Program.cs                   保持不变

Mac_UI/                          入 m2-m6/specs（首发）+ main（同步副本）
├── README.md                    更新最后一句加 M6 spec 链接
├── images/01-10.png             已存在
├── pseudocode/01-10.md          已存在
└── sampled-tokens.json          新增，§4.4 取色脚本输出

tests/Tianming.Desktop.Avalonia.Tests/
├── Controls/                    14 个 primitive × 至少 3 测试 ≈ 42 测试
├── Shell/
│   ├── AppChromeTests.cs
│   └── AppStatusBarTests.cs
└── Theme/
    └── DesignTokensTests.cs     每个 token 文件 1 个加载测试
```

**关键决策**：

- **Theme 拆 5 个 token 文件 + 1 个 PalettesLight**：分离色彩 / 字号 / 间距 / 圆角 / 阴影，让"调一个圆角"不污染调色板 diff
- **Controls 全用 `TemplatedControl` + ControlTheme**：比 UserControl 轻；纯样式驱动；对 binding 友好；测试覆盖更直接
- **Shell 跟 Controls 分目录**：chrome / status bar / menu 是窗口级容器，不在别处复用，单独放
- **Mac_UI 两边入仓**：specs 分支首发，main 上走 `git checkout specs -- Mac_UI/` 单向同步；不双向编辑

## 2. 设计 token

### 2.1 Colors（采样自 `Mac_UI/images/`，估计值 → 实施时校准）

```xaml
<!-- Theme/DesignTokens/Colors.axaml -->
<ResourceDictionary>
  <!-- Brand / Accent —— 天命 logo 的青蓝 -->
  <Color x:Key="AccentBase">#06B6D4</Color>
  <Color x:Key="AccentHover">#0891B2</Color>
  <Color x:Key="AccentPressed">#0E7490</Color>
  <Color x:Key="AccentSubtle">#CFFAFE</Color>
  <Color x:Key="AccentForeground">#FFFFFF</Color>

  <!-- Neutral 层级 -->
  <Color x:Key="SurfaceBase">#FFFFFF</Color>
  <Color x:Key="SurfaceCanvas">#F8FAFC</Color>
  <Color x:Key="SurfaceSubtle">#F1F5F9</Color>
  <Color x:Key="SurfaceMuted">#E2E8F0</Color>
  <Color x:Key="BorderSubtle">#E5E7EB</Color>
  <Color x:Key="BorderStrong">#CBD5E1</Color>

  <!-- 前景 -->
  <Color x:Key="TextPrimary">#0F172A</Color>
  <Color x:Key="TextSecondary">#475569</Color>
  <Color x:Key="TextTertiary">#94A3B8</Color>
  <Color x:Key="TextOnAccent">#FFFFFF</Color>

  <!-- 语义色 -->
  <Color x:Key="StatusSuccess">#10B981</Color>
  <Color x:Key="StatusSuccessSubtle">#D1FAE5</Color>
  <Color x:Key="StatusWarning">#F59E0B</Color>
  <Color x:Key="StatusWarningSubtle">#FEF3C7</Color>
  <Color x:Key="StatusDanger">#EF4444</Color>
  <Color x:Key="StatusDangerSubtle">#FEE2E2</Color>
  <Color x:Key="StatusInfo">#3B82F6</Color>
  <Color x:Key="StatusInfoSubtle">#DBEAFE</Color>
  <Color x:Key="StatusNeutral">#6B7280</Color>
  <Color x:Key="StatusNeutralSubtle">#F3F4F6</Color>

  <!-- 对应 *Brush —— 实施时按 *Color 对照导出 SolidColorBrush -->
</ResourceDictionary>
```

### 2.2 Typography

```xaml
<!-- Theme/DesignTokens/Typography.axaml -->
<FontFamily x:Key="FontUI">PingFang SC, SF Pro Text, -apple-system, sans-serif</FontFamily>
<FontFamily x:Key="FontMono">SF Mono, Menlo, Consolas, monospace</FontFamily>

<x:Double x:Key="FontSizeDisplay">28</x:Double>     <!-- WelcomeView 主标题 -->
<x:Double x:Key="FontSizeH1">22</x:Double>          <!-- 页面级 H1 -->
<x:Double x:Key="FontSizeH2">18</x:Double>          <!-- StatsCard 大数字 -->
<x:Double x:Key="FontSizeH3">15</x:Double>          <!-- section 标题 -->
<x:Double x:Key="FontSizeBody">13</x:Double>        <!-- 正文 -->
<x:Double x:Key="FontSizeSecondary">12</x:Double>   <!-- 副文 -->
<x:Double x:Key="FontSizeCaption">11</x:Double>     <!-- 标签 / breadcrumb -->

<FontWeight x:Key="FontWeightRegular">400</FontWeight>
<FontWeight x:Key="FontWeightMedium">500</FontWeight>
<FontWeight x:Key="FontWeightSemibold">600</FontWeight>
<FontWeight x:Key="FontWeightBold">700</FontWeight>

<x:Double x:Key="LineHeightTight">1.2</x:Double>
<x:Double x:Key="LineHeightNormal">1.5</x:Double>
<x:Double x:Key="LineHeightRelaxed">1.7</x:Double>
```

### 2.3 Spacing / Radii / Shadows

```xaml
<!-- Spacing.axaml — 4px 基础栅格 -->
<x:Double x:Key="Space1">4</x:Double>
<x:Double x:Key="Space2">8</x:Double>
<x:Double x:Key="Space3">12</x:Double>
<x:Double x:Key="Space4">16</x:Double>
<x:Double x:Key="Space5">20</x:Double>
<x:Double x:Key="Space6">24</x:Double>
<x:Double x:Key="Space8">32</x:Double>
<x:Double x:Key="Space10">40</x:Double>
<Thickness x:Key="PaddingCard">16</Thickness>
<Thickness x:Key="PaddingPage">24</Thickness>

<!-- Radii.axaml -->
<CornerRadius x:Key="RadiusSm">4</CornerRadius>
<CornerRadius x:Key="RadiusMd">6</CornerRadius>
<CornerRadius x:Key="RadiusLg">8</CornerRadius>
<CornerRadius x:Key="RadiusXl">12</CornerRadius>
<CornerRadius x:Key="RadiusFull">9999</CornerRadius>

<!-- Shadows.axaml -->
<BoxShadows x:Key="ShadowSm">0 1 2 0 #0F172A14</BoxShadows>
<BoxShadows x:Key="ShadowMd">0 4 12 0 #0F172A1F</BoxShadows>
<BoxShadows x:Key="ShadowLg">0 12 32 0 #0F172A29</BoxShadows>
```

### 2.4 取色校准

- 实施 step 用 Python PIL（或 `sips`）对每张参考图采样关键 anchor 像素（logo / accent button / card bg / text），output `Mac_UI/sampled-tokens.json`
- 用 sampled 值覆盖 §2.1 估计值
- spec 给的是 token 名称 + 估计值 + 校准方法，**不固化精确 HEX**

## 3. 窗口 chrome + 底部状态栏 + NativeMenu stub

### 3.1 自定义 chrome（36px）

```xaml
<!-- Views/MainWindow.axaml -->
<Window x:Class="Tianming.Desktop.Avalonia.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="{Binding Title}"
        Width="1280" Height="820"
        MinWidth="960" MinHeight="600"
        Background="{DynamicResource SurfaceCanvasBrush}"
        ExtendClientAreaToDecorationsHint="True"
        ExtendClientAreaChromeHints="PreferSystemChrome"
        ExtendClientAreaTitleBarHeightHint="36">
  <Grid RowDefinitions="36, *, 28">
    <shell:AppChrome    Grid.Row="0" DataContext="{Binding Chrome}"/>
    <ContentControl     Grid.Row="1" Content="{Binding CurrentRoot}"/>
    <shell:AppStatusBar Grid.Row="2" DataContext="{Binding StatusBar}"/>
  </Grid>
</Window>
```

**AppChrome 内部布局**：

```
| 78px 留给 traffic light | breadcrumb (BreadcrumbBar primitive) | 自适应 spacer | 右侧 icon 按钮组 | 12px |
```

```csharp
public sealed class AppChromeViewModel : ObservableObject
{
    public ObservableCollection<BreadcrumbSegment> Segments { get; }
    public IRelayCommand<BreadcrumbSegment> NavigateUpCommand { get; }
    public IRelayCommand OpenSearchCommand { get; }
    public IRelayCommand OpenNotificationsCommand { get; }
    public IRelayCommand OpenProfileCommand { get; }
}
public sealed record BreadcrumbSegment(string Label, object? Tag);
```

### 3.2 底部状态栏（28px）

```
| .NET 8 | 本地写作模式 | Keychain ✓ | ONNX ✓ |    spacer    | 项目路径 ~/Doc... |
```

```csharp
public sealed class AppStatusBarViewModel : ObservableObject
{
    public string DotNetRuntime { get; }
    public StatusIndicator LocalMode { get; }
    public StatusIndicator KeychainStatus { get; set; }
    public StatusIndicator OnnxStatus { get; set; }
    public string? CurrentProjectPath { get; set; }
}
public sealed record StatusIndicator(string Label, StatusKind Kind, string? Tooltip = null);
public enum StatusKind { Success, Warning, Danger, Info, Neutral }
```

### 3.3 NativeMenu stub

```xaml
<!-- App.axaml -->
<NativeMenu.Menu>
  <NativeMenu>
    <NativeMenuItem Header="天命">
      <NativeMenu>
        <NativeMenuItem Header="关于天命"/>
        <NativeMenuItemSeparator/>
        <NativeMenuItem Header="偏好…" Gesture="Cmd+OemComma"/>
        <NativeMenuItemSeparator/>
        <NativeMenuItem Header="隐藏天命" Gesture="Cmd+H"/>
        <NativeMenuItem Header="退出"     Gesture="Cmd+Q"/>
      </NativeMenu>
    </NativeMenuItem>
    <NativeMenuItem Header="文件">
      <NativeMenu>
        <NativeMenuItem Header="新建项目…" Gesture="Cmd+N"/>
        <NativeMenuItem Header="打开项目…" Gesture="Cmd+O"/>
        <NativeMenuItemSeparator/>
        <NativeMenuItem Header="保存"     Gesture="Cmd+S"/>
      </NativeMenu>
    </NativeMenuItem>
    <NativeMenuItem Header="编辑">
      <NativeMenu>
        <NativeMenuItem Header="撤销" Gesture="Cmd+Z"/>
        <NativeMenuItem Header="重做" Gesture="Cmd+Shift+Z"/>
        <NativeMenuItemSeparator/>
        <NativeMenuItem Header="查找" Gesture="Cmd+F"/>
      </NativeMenu>
    </NativeMenuItem>
    <NativeMenuItem Header="视图"/>
    <NativeMenuItem Header="窗口"/>
    <NativeMenuItem Header="帮助"/>
  </NativeMenu>
</NativeMenu.Menu>
```

所有 `NativeMenuItem` 的 Command 留空 / FallbackValue=null，由 M5 plan 绑定真实命令。

## 4. 14 个 primitives API surface

| Primitive | 用途 / 主要消费页面 | 关键 StyledProperty |
|---|---|---|
| **StatsCard** | 数字 + label + 趋势/sparkline；02/04/08/09 | `Label`、`Value`、`Caption`、`TrendKind`、`AccessoryContent` |
| **BadgePill** | 圆角 pill 状态徽章；所有页面 | `Text`、`Kind`、`ShowDot` |
| **SectionCard** | 白底圆角 + 标题 + body slot；所有页面 | `Title`、`Subtitle`、`HeaderActions`、`Content` |
| **NavRail** | 左侧分组导航容器；02/03/04 起所有页面 | `Groups`、`ActiveKey`、`NavigateCommand` |
| **BreadcrumbBar** | 顶部 chrome 中部面包屑；所有页面 | `Segments`、`NavigateCommand` |
| **StatusBarItem** | 底部状态栏的圆点 + label；01/02 | `Label`、`Kind`、`Tooltip` |
| **ProjectCard** | 封面 + 名称 + 元信息 + 进度；01 最近项目 | `ProjectName`、`Cover`、`LastOpenedText`、`ChapterProgress`、`ProgressPercent`、`OpenCommand` |
| **SearchBox** | 圆角 + search icon + clear；02/04/08 | `Text`、`Placeholder`、`SubmitCommand` |
| **SegmentedTabs** | Ask/Plan/Agent、网格/列表；01/07 | `Items`、`SelectedKey`、`SelectCommand` |
| **SidebarTreeItem** | 可折叠树节点；03/04/05 | `Label`、`IconGlyph`、`Trailing`、`IsExpanded`、`IsSelected`、`Depth`、`Children` |
| **ConversationBubble** | 聊天气泡（user/assistant）；07/M4.5/M6.7 | `Role`、`Content`、`ThinkingBlock`、`References`、`IsStreaming`、`Timestamp` |
| **ToolCallCard** | Agent 工具调用三态卡；07/M6.7 | `ToolName`、`ArgumentsPreview`、`State`、`ApproveCommand`、`RejectCommand` |
| **CodeViewer** | JSON / Markdown 只读高亮；06 | `Code`、`Language`、`ShowLineNumbers`、`WordWrap` |
| **DataGridRowCell** | 表格行内 cell 统一样式；03/04/08 | `Kind`、`Content`、`BadgeKind`、`ClickCommand` |

每个 primitive：
- `.axaml` ControlTheme + `.cs` TemplatedControl
- 至少 3 个测试：默认值正确 / property 触发视觉变化 / edge case
- IconGlyph 走 Lucide string token（`"home"`/`"search"`/`"folder-open"`）
- 不直接依赖业务 service，纯 styled property 驱动

## 5. NuGet 依赖整合

```xml
<!-- src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj -->
<ItemGroup>
  <!-- 已有：Avalonia 11.0.10、CommunityToolkit.Mvvm 8.2.2、M.E.DI 8.0.1 / .Logging 8.0.0 -->
  <PackageReference Include="LiveChartsCore.SkiaSharpView.Avalonia" Version="2.0.2" />
  <PackageReference Include="Avalonia.AvaloniaEdit"                 Version="11.0.6" />
</ItemGroup>
```

> 包 id / 版本（基于真实 nuget.org 探测，2026-05-13 校准）：
> - **LiveCharts2 = `LiveChartsCore.SkiaSharpView.Avalonia 2.0.2`** stable（依赖 Avalonia 11.0.0，与我们 11.0.10 兼容）
> - **AvaloniaEdit 包 id = `Avalonia.AvaloniaEdit 11.0.6`**（bare `AvaloniaEdit` id 只有 0.10.x 旧版）
> - **Lucide 图标包基建不引入**：探测了 3 个候选都不兼容 Avalonia 11.0.10 + net8.0：
>   - `Projektanker.Icons.Avalonia.Lucide` 不存在；`Projektanker.Icons.Avalonia 9.4.1` 要 Avalonia 11.1.3+
>   - `Lucide.Avalonia` (0.1.0–0.2.6) 全部要 Avalonia >= 11.2.2，0.2.4+ 还只支持 net10.0
>   - `IconPacks.Avalonia.Lucide` (1.0–2.0) 全部要 Avalonia >= 11.0.13（小步升级可达，但本基建不做）
>   - **决策**：基建保留 primitives `IconGlyph: string` API 契约（Lucide 名 `"home"`/`"search"`），渲染暂用 `TextBlock`（AppChrome 已用 emoji 占位）；Sub-plan 2 真消费图标时单独决策（升 Avalonia 11.0.13 + IconPacks.Avalonia.Lucide / 切 net10.0 + Lucide.Avalonia / Unicode 占位）

### 5.1 LiveCharts2 全局样式（启动时一次）

```csharp
// App.axaml.cs 的 OnFrameworkInitializationCompleted 之前
LiveChartsSettings.ConfigureTheme(t => t
    .AddPalette(
        SKColors.Parse("#06B6D4"),
        SKColors.Parse("#10B981"),
        SKColors.Parse("#F59E0B"),
        SKColors.Parse("#3B82F6"),
        SKColors.Parse("#94A3B8"))
    .HasGlobalSKTypeface(SKTypeface.FromFamilyName("PingFang SC"))
);
```

### 5.2 AvaloniaEdit 整合

`Theme/AvaloniaEditBootstrap.cs`：
- 启动时注册 JSON / Markdown 高亮（已内置）
- 默认字体走 `FontMono` token
- 行号走 `TextTertiaryBrush`
- 滚动条走我们覆盖样式

### 5.3 Lucide Icons 整合

```xaml
<!-- App.axaml -->
<Application.Resources>
  <ResourceDictionary>
    <ResourceDictionary.MergedDictionaries>
      <!-- 1. 设计 tokens（顺序重要：先 token 后样式） -->
      <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Colors.axaml"/>
      <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Typography.axaml"/>
      <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Spacing.axaml"/>
      <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Radii.axaml"/>
      <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/DesignTokens/Shadows.axaml"/>
      <!-- 2. 控件样式覆盖 -->
      <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/CommonStyles.axaml"/>
    </ResourceDictionary.MergedDictionaries>
  </ResourceDictionary>
</Application.Resources>

<Application.Styles>
  <FluentTheme/>
</Application.Styles>
```

> **Lucide 用法**（`Lucide.Avalonia 0.2.6`）：包用 `LucideIcon` 控件 + `LucideIconKind` enum。每个 primitive 的 `IconGlyph: string` 属性接 Lucide 名称（如 `"home"` / `"search"` / `"folder-open"`），primitive 的 ControlTemplate 内部把字符串映射到 `LucideIcon`。具体映射方式（直接 attached property / converter / enum lookup）在 Phase 9 第一个用到 icon 的 primitive 实施时定型，spec 仅约定 string token 接口。

## 6. DI 装配与状态来源

### 6.1 ServiceCollection 扩展

```csharp
// AvaloniaShellServiceCollectionExtensions.cs（扩展现有）
services.AddSingleton<AppChromeViewModel>();
services.AddSingleton<AppStatusBarViewModel>();
services.AddSingleton<IBreadcrumbSource, NavigationBreadcrumbSource>();
services.AddSingleton<IRuntimeInfoProvider, RuntimeInfoProvider>();
services.AddSingleton<IKeychainHealthProbe, KeychainHealthProbe>();
services.AddSingleton<IOnnxHealthProbe, OnnxHealthProbe>();
```

### 6.2 Breadcrumb 数据来源

```csharp
public interface IBreadcrumbSource
{
    IObservable<IReadOnlyList<BreadcrumbSegment>> Segments { get; }
}

// 订阅 INavigationService.CurrentPageChanged：
// - "天命" 永远是 root
// - 项目名（当前项目存在时）
// - 当前 PageKey 的友好名（PageRegistry 元数据）
internal sealed class NavigationBreadcrumbSource : IBreadcrumbSource { /* … */ }
```

### 6.3 StatusBar 数据来源

| 字段 | 来源 | 实现 |
|---|---|---|
| `.NET 8.0.x` | `RuntimeInformation.FrameworkDescription` | `IRuntimeInfoProvider`，启动时取一次 |
| 本地写作模式 | 硬编码 `true` | `IRuntimeInfoProvider.IsLocalMode` |
| Keychain ✓/✗ | `IApiKeySecretStore` 探测 | `IKeychainHealthProbe.ProbeAsync()` 启动时异步跑 |
| ONNX ✓/✗ | 检查模型文件存在性 | `IOnnxHealthProbe.ProbeAsync()` 启动时异步跑 |
| 项目路径 | 当前项目存在时 | 订阅当前项目变化 |

```csharp
public interface IKeychainHealthProbe { Task<StatusIndicator> ProbeAsync(CancellationToken ct = default); }
public interface IOnnxHealthProbe    { Task<StatusIndicator> ProbeAsync(CancellationToken ct = default); }
public interface IRuntimeInfoProvider
{
    string FrameworkDescription { get; }
    bool IsLocalMode { get; }
}
```

VM 在构造时同步取 RuntimeInfo + 异步跑两个 probe（用 `DispatcherScheduler`），失败显示 `Danger` 状态而不是崩溃。

### 6.4 跨进程边界 API 兼容性确认

实施 step 开工前必须用 Explore subagent 核对（避免 spec 与真实仓状态漂移）：

- `IApiKeySecretStore` 真实公开方法签名（是否有可用作 health probe 的方法）
- `OnnxEmbedderFactory` 暴露的 "模型存在性" 判断方式

若现有 API 不直接支持探测，**基建 step 允许向已落地的 `Tianming.AI` 加一个最小 `IsAvailableAsync()` 方法**（probe 是基建的天然边界）。

### 6.5 启动顺序

```
Program.Main
  ↓
LiveChartsSettings.ConfigureTheme       （§5.1）
  ↓
AvaloniaEditBootstrap.Initialize         （§5.2）
  ↓
AppHost.Configure(services => …)
  ↓
ServiceProvider 构建
  ↓
MainWindow 显示
  ↓
AppChromeVM / AppStatusBarVM 异步 fire probes
  ↓
NativeMenu 已绑（stub），快捷键可见
```

## 7. 测试策略

### 7.1 Theme tokens 加载

```csharp
public class DesignTokensTests
{
    [Fact]
    public void Colors_AllExpectedKeysResolve()
    {
        var keys = new[] { "AccentBase", "SurfaceBase", "TextPrimary", "StatusSuccess", /* … */ };
        // resolve 每个 key、断言类型 + 非 null
    }
    // Typography / Spacing / Radii / Shadows 同样模式
}
```

### 7.2 Primitives 单元测试（Avalonia.Headless）

每个 primitive 至少 3 个测试：
- 默认值正确
- set property → 视觉状态变化（StyledProperty 触发 invalidation）
- edge case（StatsCard 无 Caption / NavRail 空 Groups / BadgePill 各 StatusKind）

工程量：14 × 3 ≈ 42 个测试。`Avalonia.Headless` + `Avalonia.Headless.XUnit` 已有。

### 7.3 Shell 组装测试

- `AppChromeTests`：breadcrumb 渲染、左边距 ≥ 78px、icon 按钮存在
- `AppStatusBarTests`：所有 indicator 渲染、probe 失败显示 Danger

### 7.4 像素级保真验证（不自动化）

接受标准在 Sub-plan 2 Done Definition 里：基建 + Sub-plan 2 完成后并排打开 `Mac_UI/images/01-welcome-project-selector.png` 与运行中应用，人工逐项比对，差距 > 4px 或颜色 ΔE > 5 算未通过。本 sub-plan 不引入自动化 visual regression。

### 7.5 现有测试不退化

每个 step 完成后跑 `dotnet test Tianming.MacMigration.sln`，1156 基线 + 新增测试全过。

## 8. Mac_UI 入仓与同步

### 8.1 入仓步骤

1. **在当前分支 `m2-m6/specs-2026-05-12`**：
   ```
   git add Mac_UI/
   git commit -m "docs(ui): Mac_UI 视觉参考素材入仓（10 张参考图 + pseudocode）"
   ```

2. **同步到 `main`**：
   ```
   git checkout main
   git checkout m2-m6/specs-2026-05-12 -- Mac_UI/
   git add Mac_UI/
   git commit -m "docs(ui): 从 specs 分支取得 Mac_UI 视觉参考素材"
   git checkout m2-m6/specs-2026-05-12
   ```

3. **同步约定**：Mac_UI **只在 specs 分支编辑**，main 上的副本永远走 "从 specs 拉过来" 的单向同步。

### 8.2 README 更新

`Mac_UI/README.md` 最后段补一句："M6 已写 spec：`Docs/superpowers/specs/2026-05-12-tianming-m6-v287-core-upgrade-design.md`"（M6 不是被删，是 v2.8.7 写作内核升级）。

## 9. Out of Scope + Done Definition

### 9.1 Sub-plan 1 不做

- 任何业务页面 view（WelcomeView 重写 / Dashboard center 装填 / LeftNav 重写都属于 Sub-plan 2）
- 具体图表实例 view（基建只产 LiveCharts2 全局 palette + font）
- 真实 chat / AI 调用接线（ConversationBubble / ToolCallCard 只是空控件，无 ChatService binding）
- PageRegistry 扩展（现有 PageKeys 不动）
- NativeMenu 命令真绑定（M5 plan 接）
- 暗色 palette（删 `PalettesDark.axaml`）
- 多语言（所有中文硬编码可接受）
- AppHost.cs 之外的 DI 重组（只新增基建相关服务）
- 像素级 visual regression 自动化测试
- 修复 M3 已发现的视觉问题（归 Sub-plan 2）

### 9.2 Done Definition

Sub-plan 1 完成 = 全部下面都通过：

1. **入仓**：Mac_UI 在 `m2-m6/specs` 与 `main` 都可见；`sampled-tokens.json` 已生成
2. **依赖**：3 个 NuGet 包（LiveCharts2 / AvaloniaEdit / Lucide）已加；`dotnet restore` + `dotnet build Tianming.MacMigration.sln` 0 error 0 warning
3. **Theme**：5 个 token 文件 + CommonStyles.axaml + ControlStyles/* 全部就位；`PalettesDark.axaml` 已删；亮色锁定
4. **Chrome**：MainWindow 以 ExtendClientArea 启动，自定义 breadcrumb + 原生 traffic light 共存；breadcrumb 默认显示"天命"一段
5. **Status bar**：底部 28px 状态栏可见；.NET 8 / 本地写作模式立即显示；Keychain / ONNX probe 完成后填充
6. **NativeMenu**：macOS 顶部菜单栏出现 天命 / 文件 / 编辑 / 视图 / 窗口 / 帮助 六个一级菜单，快捷键文字可见
7. **14 个 primitives**：每个都有 `.axaml` ControlTheme + `.cs` TemplatedControl + 至少 3 测试；都能在 designer / preview 渲染
8. **测试**：`dotnet test Tianming.MacMigration.sln` 1156 基线 + 新增 ≈ 1200 全过
9. **冒烟**：`dotnet run --project src/Tianming.Desktop.Avalonia` 启动成功，看到 chrome + status bar + 已有 WelcomeView（暂未重做，但显示在新 chrome 里没崩溃）
10. **commit**：基建按 step 拆 commit，每 step 一个清晰 message；最终入 `m2-m6/specs`，Mac_UI 单向同步到 `main`

### 9.3 不在 Done Definition 的 "漂亮" 加分项

- HotReload 支持（Avalonia 11 有，但 token hot reload 偶尔抽风，不强求）
- 屏幕缩放适配（macOS Retina 自动处理）
- Accessibility 标签（自用跳过）

## 10. 后续 Sub-plan 引用本基建

Sub-plan 2（M3 视觉重做）和 Sub-plan 3（docs+plans 对齐）都基于本基建：
- **Sub-plan 2** 用本基建产出的 14 个 primitives + design tokens + chrome + status bar 重做 M3 4 个 view（WelcomeView / DashboardView / LeftNavView / RightConversationView）。Lucide 图标包延后由 Sub-plan 2 真消费时决策。
- **Sub-plan 3** 在 M3/M4/M5/M6 spec 与 M4/M5 plan 中嵌入 Mac_UI 参考图作为视觉真值源。
