# 天命 macOS 迁移 — M3 Avalonia 基础 Shell 设计（自用版）

日期：2026-05-12
分支：`m2-m6/specs-2026-05-12`
依据：`Docs/superpowers/specs/2026-05-12-tianming-m2-ai-vector-design.md`
定位：**个人自用**，不做公开分发

## 1. 范围与边界

### 1.1 纳入 M3

- **A. Avalonia 桌面项目**：`src/Tianming.Desktop.Avalonia/` + `Program.cs` + `App.axaml` + `MainWindow.axaml`，`dotnet run` 可启动一个空主窗口。
- **B. MVVM + DI**：`CommunityToolkit.Mvvm` 源生成器 + `Microsoft.Extensions.DependencyInjection`；`AppHost.cs` 装配所有 portable service 与 ViewModel。
- **C. 主窗口三栏布局**：`ThreeColumnLayoutView`（左导航 / 中央主区 / 右对话占位），`GridSplitter` 拖宽度并持久化。
- **D. 导航**：`INavigationService` + `PageRegistry`；中央区按 `PageKey` 加载 VM/View。
- **E. 项目生命周期**：启动时读上次项目、无则显示 `WelcomeView`（新建/打开）；切项目广播 `ProjectContextChangedEvent`。
- **F. 主题桥接**：`PortableThemeStateController`（M1 已端口）的 palette 注入 Avalonia `Application.Resources`，Light/Dark 可切。
- **G. 冒烟**：启动即见三栏空壳 → 点"新建项目" → 落盘 → 标题栏显示项目名。

### 1.2 不做

- 基础控件库占位（`TitleBar` / `BusyOverlay` / `ToastHost` / `InlineDialog`）— 等 M4 需要再按需加
- 多 Wave agent 并行编排 — 自用一人写，串行就行
- Avalonia.Headless 集成测试 — VM 单测已够
- FOUC 极致优化、冷启动 < 3 秒指标 — 能跑就行

### 1.3 决策

| 编号 | 决策 |
|---|---|
| Q1 | MVVM：`CommunityToolkit.Mvvm`，非 ReactiveUI |
| Q2 | DI：`Microsoft.Extensions.DependencyInjection` 直接用，不引 `Hosting` 全家桶 |
| Q3 | 日志：`Microsoft.Extensions.Logging.Console` 够用（Serilog 等 M4 再说） |
| Q4 | Avalonia 版本：最新稳定 11.x |
| Q5 | 样式：`Avalonia.Themes.Fluent` 基底 + 简单 accent 覆盖 |
| Q6 | 窗口状态：`~/Library/Application Support/Tianming/window_state.json` 存位置/尺寸/三栏比例 |
| Q7 | 启动项目：有上次打开的就进主窗口；否则 Welcome |
| Q8 | v2.8.7 升级点不塞进 M3；M3 只留下稳定扩展点，后续由 M4/M6 承接 |

## 2. 架构

### 2.1 目录结构

```
src/Tianming.Desktop.Avalonia/
├── Tianming.Desktop.Avalonia.csproj     （net8.0，macOS）
├── Program.cs                            （DI 构建 + Avalonia 启动）
├── App.axaml / App.axaml.cs
├── AppHost.cs                            （ServiceCollection 装配）
├── Views/
│   ├── MainWindow.axaml(.cs)
│   ├── WelcomeView.axaml(.cs)
│   ├── ThreeColumnLayoutView.axaml(.cs)
│   └── Shell/
│       ├── LeftNavView.axaml(.cs)
│       ├── CenterContentView.axaml(.cs)
│       └── RightConversationView.axaml(.cs)   （M3 仅占位）
├── ViewModels/
│   ├── MainWindowViewModel.cs
│   ├── WelcomeViewModel.cs
│   ├── ThreeColumnLayoutViewModel.cs
│   └── Shell/
│       ├── LeftNavViewModel.cs
│       ├── CenterContentViewModel.cs
│       └── RightConversationViewModel.cs       （占位）
├── Navigation/
│   ├── INavigationService.cs
│   ├── NavigationService.cs
│   ├── PageRegistry.cs
│   └── PageKeys.cs
├── Theme/
│   ├── ThemeBridge.cs
│   ├── PalettesDark.axaml
│   ├── PalettesLight.axaml
│   └── CommonStyles.axaml
├── Infrastructure/
│   ├── AppPaths.cs                        （`~/Library/Application Support/Tianming` 等）
│   ├── WindowStateStore.cs
│   ├── AppLifecycle.cs
│   └── DispatcherScheduler.cs             （portable Task → UI 线程）
└── Assets/                                 （图标）
```

`Tianming.MacMigration.sln` 加入此项目，引用 `Tianming.ProjectData` / `Tianming.AI` / `Tianming.Framework`。

### 2.2 DI 装配

```csharp
public static class AppHost
{
    public static IServiceProvider Build()
    {
        var s = new ServiceCollection();
        s.AddLogging(b => b.AddConsole());
        s.AddProjectDataServices();
        s.AddAIServices();
        s.AddFrameworkServices();
        s.AddAvaloniaShell();
        return s.BuildServiceProvider();
    }
}
```

每个 portable 类库配一个 `ServiceCollectionExtensions.cs`（如 `AddProjectDataServices`），M3 建立约定并写第一版，M4 按需扩。

### 2.3 导航

```csharp
public interface INavigationService
{
    Task NavigateAsync(PageKey key, object? parameter = null);
    Task GoBackAsync();
    bool CanGoBack { get; }
    PageKey? CurrentKey { get; }
}

public readonly record struct PageKey(string Id);

public static class PageKeys
{
    public static readonly PageKey Dashboard = new("dashboard");
    public static readonly PageKey Welcome   = new("welcome");
    public static readonly PageKey Settings  = new("settings");
    // M4 扩展更多
}
```

`PageRegistry` 维护 `Dictionary<PageKey, (Type VMType, Type ViewType)>`；`NavigationService` 从 DI 取 VM、反射建 View、把 VM 设为 DataContext。

### 2.4 主题桥接

`ThemeBridge`：
- 订阅 `PortableThemeStateController.CurrentThemeChanged`
- palette 映射到 `Color` / `SolidColorBrush`，替换 `Application.Current.Resources["Color.Primary"]` 等 key
- `RequestedThemeVariant` 按 portable 状态设置
- 冷启动先 apply 再 `AvaloniaXamlLoader.Load(app)`，避免闪

### 2.5 三栏布局

```xml
<Grid ColumnDefinitions="{Binding LeftWidth}, Auto, *, Auto, {Binding RightWidth}">
  <ContentControl Grid.Column="0" Content="{Binding LeftNav}"/>
  <GridSplitter Grid.Column="1" Width="4"/>
  <ContentControl Grid.Column="2" Content="{Binding Center}"/>
  <GridSplitter Grid.Column="3" Width="4"/>
  <ContentControl Grid.Column="4" Content="{Binding RightPanel}"/>
</Grid>
```

`LeftWidth` / `RightWidth` 绑定 `WindowStateStore`；拖动保存。

### 2.6 启动流程

```
Program.Main
  └─ AppHost.Build() → IServiceProvider
  └─ BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)
      └─ App.OnFrameworkInitializationCompleted
          ├─ ThemeBridge.Initialize（apply 初始主题）
          ├─ AppLifecycle.OnStartup
          │   ├─ WindowStateStore.Load
          │   ├─ FileProjectManager.LoadLastOpenedAsync
          │   └─ 决定首页：Welcome or Dashboard
          └─ Show MainWindow
```

关闭：`MainWindow.Closing` → `WindowStateStore.Save` → `IAsyncDisposable` cleanup。

### 2.7 v2.8.7-ready 扩展点

M3 不实现 `v2.8.7-升级说明.txt` 里的业务内核升级，但 shell 必须给后续计划留稳定入口：

- `PageKeys` / `PageRegistry` 的命名要能直接扩展 M4 页面与 M6 内核页面，避免后面重命名导航协议。
- 右栏 `RightConversationView` 只做占位，但 `DataContext` 与布局边界要按 M4.5 对话面板预留，后续接 Ask / Plan / Agent 三模式时不改主窗口。
- `ChapterPipelinePage`、`AI 管理`、`校验报告`、`Agent 工具调用确认` 等页面不在 M3 创建，但 M3 的导航服务必须支持这些 PageKey 后续注册。
- `AppHost` 的 DI 约定要允许 M6 替换或追加写作内核服务，例如 HumanizeRules、CHANGES Canonicalizer、WAL、ContextService 子系统、AI middleware，而不要求 UI 层直接 new service。
- `DispatcherScheduler` 要作为 portable 异步任务进入 UI 的唯一通道；后续流式生成、校验日志、Agent 工具调用进度都走这里。

## 3. 工作拆分

串行 5 步：

1. **Step 1 — 项目骨架**（~1 小时）：新建 `Tianming.Desktop.Avalonia.csproj`，加 NuGet（Avalonia、CommunityToolkit.Mvvm、Microsoft.Extensions.DependencyInjection），sln 加项目，`Program.cs` 最小 `BuildAvaloniaApp().StartWithClassicDesktopLifetime(args)`。`dotnet run` 能弹空白窗。Commit。
2. **Step 2 — DI + AppHost**（~1 小时）：`AppHost.Build` + 各 portable 库的 `ServiceCollectionExtensions`。`AppHost.Build()` 能成功 resolve 几个 key service。1-2 单测。Commit。
3. **Step 3 — 导航 + 主窗口 + 三栏**（~3 小时）：`INavigationService` / `PageRegistry` / `ThreeColumnLayoutView` / `MainWindow`。`WelcomeView` 放个"新建项目"按钮。串通"启动 → 新建项目 → 跳 Dashboard（空页占位）"。Commit。
4. **Step 4 — 主题桥接**（~1.5 小时）：`ThemeBridge` + `PalettesLight/Dark.axaml` + 主题切换按钮（Welcome 页或 Dashboard 随便放一个）。Commit。
5. **Step 5 — 窗口状态持久化 + 生命周期收尾**（~1 小时）：`WindowStateStore` + `AppLifecycle`。重启后位置/尺寸/三栏比例恢复。Commit。

## 4. 测试

- VM 单测：`MainWindowViewModel`、`WelcomeViewModel`、`NavigationService`、`ThemeBridge`（主题切换 Resources 替换）
- 手工冒烟：按 Step 3/4/5 描述的流程跑一遍，能跑就过
- 不写：Avalonia.Headless 端到端自动化测试

## 5. 风险

| ID | 风险 | 缓解 |
|---|---|---|
| R1 | Avalonia 11 的 XAML 语法与 WPF 有差异（`x:DataType`、`StaticResource` vs `DynamicResource` 行为不同） | 边做边学；出问题查 Avalonia 文档；M3 只做 shell 量不大 |
| R2 | ThemeBridge 实时切换时 Resource key 未生效 | `DynamicResource` 全用上；切换后 `InvalidateVisual()` |
| R3 | DispatcherScheduler 跨线程误用导致偶发崩溃 | 强制所有 portable 异步 → UI 要走 `Dispatcher.UIThread.Post` |

## 6. 验收

1. `dotnet build Tianming.MacMigration.sln` → 0 Error
2. `dotnet test Tianming.MacMigration.sln` → 全过
3. `dotnet run --project src/Tianming.Desktop.Avalonia` 启动后看到三栏主窗口
4. 新建项目流程跑通（Welcome → 文件夹选择 → Dashboard 空页 → 标题栏显示项目名）
5. 切换主题按钮可视 Light/Dark 即时切换
6. 关闭重开后窗口位置/尺寸/三栏比例恢复

完成后进入 M4（核心模块页面迁移）。
