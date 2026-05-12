# 天命 macOS 迁移 — M3 Avalonia 基础 Shell 设计

日期：2026-05-12
分支：`m3/avalonia-shell-2026-05-12`（计划，本 spec 在 `m2-m6/specs-2026-05-12`）
依据：`Docs/macOS迁移/功能对齐审计与迁移设计草案.md`、`Docs/macOS迁移/功能对齐矩阵.md`、`Docs/superpowers/specs/2026-05-12-tianming-m2-ai-vector-design.md`

## 1. 范围与边界

### 1.1 纳入 M3

- **A. Avalonia 桌面项目创建**：`src/Tianming.Desktop.Avalonia` + `Program.cs` + `App.axaml` + `App.axaml.cs`，net8.0 targeting macOS（arm64 + x64），首启可打开一个空 `MainWindow`。
- **B. MVVM + DI 基础设施**：引入 `CommunityToolkit.Mvvm`（Source Generator 风格）+ `Microsoft.Extensions.DependencyInjection`；`AppHost` 负责装配所有 portable service 与 ViewModel。
- **C. 主窗口骨架**：`MainWindow` + `ThreeColumnLayoutView`（左导航 / 中央主区 / 右对话面板），布局可响应式缩放，分栏可拖拽调宽度并持久化。
- **D. 导航框架**：基于 portable `WritingNavigationCatalog` 的 `INavigationService`，中央区 `ContentPresenter` 按 `PageKey` 加载 ViewModel 与 View，支持返回栈、参数传递、页面缓存策略。
- **E. 项目生命周期**：启动时读取上次打开的项目、无项目时显示 `WelcomeView`（新建/打开）、切换项目时广播 `ProjectContextChangedEvent`，所有 ViewModel 按需响应刷新。
- **F. 主题资源桥接**：`PortableThemeStateController`（M1 已端口）输出的主题 brush palette 注入 Avalonia `Application.Resources` 与 `Styles`，完成 Light/Dark/Custom 切换可视化。
- **G. 基础控件库占位**：`Controls/` 目录保留常用自定义控件壳（`TitleBar`、`SectionHeader`、`BusyOverlay`、`ToastHost`、`InlineDialog`）——仅骨架 + 样式，业务使用方在 M4 填充。
- **H. 冒烟验收**：macOS 启动即现三栏空壳，可点"新建项目"走到 `FileProjectManager`，落盘一个新项目，标题栏显示项目名；冷启动 < 3 秒，关闭正常退出。

### 1.2 不在 M3 范围

- 任何业务页面的真实 UI（设计/生成/校验/编辑器等，M4）
- Markdown 渲染、Diff 展示（M4 替换）
- macOS 平台能力（Keychain 真值接入、菜单栏图标、通知、URL Protocol，M5）
- 服务端登录/订阅页面（M6）
- ONNX 真向量实际加载（M2 已完成，M3 仅注入 `ITextEmbedder`）
- 打包、签名、公证（M7）

### 1.3 决策记录

| 编号 | 决策 |
|---|---|
| Q1 | MVVM：`CommunityToolkit.Mvvm`（源生成器 + `ObservableObject`/`RelayCommand`/`ObservableProperty`），非 ReactiveUI |
| Q2 | DI：`Microsoft.Extensions.Hosting` + `IServiceCollection`；`AppHost` 组装，非 Autofac |
| Q3 | 日志：`Microsoft.Extensions.Logging` + `Serilog.Sinks.File`；沿用 M1 端口的 `PortableLogFormatter` |
| Q4 | Avalonia 版本：锁定到 M3 起跑时最新稳定版（11.x），`Directory.Packages.props` 集中版本 |
| Q5 | 样式方案：FluentTheme 基底 + 天命自定义 accent / brush overrides；不做 Material-style 重装饰 |
| Q6 | 导航：页面实例按 `PageKey` 缓存（常驻型），不走 Page-Reload；对话面板独立持久 VM |
| Q7 | 窗口状态持久化：`window_state.json` 存位置/尺寸/三栏比例/最大化状态 |
| Q8 | 启动项目策略：优先上次打开；上次打开失效则显示 Welcome；Welcome 可跳新建向导 |
| Q9 | 字体：macOS 默认 `PingFang SC` + `.AppleSystemUIFont`；`PortableFontConfigurationStore` 已端口，M3 仅接入默认主题字体 |

## 2. 架构改造

### 2.1 解决方案布局

新增项目：

```
src/Tianming.Desktop.Avalonia/           net8.0（macOS x64 + arm64）
├── Tianming.Desktop.Avalonia.csproj
├── Program.cs                            （AppHost 构建 + Avalonia 启动）
├── App.axaml / App.axaml.cs              （Avalonia Application）
├── AppHost.cs                            （DI 组装入口）
├── Views/
│   ├── MainWindow.axaml                  （根窗口）
│   ├── WelcomeView.axaml                 （无项目时欢迎页）
│   ├── ThreeColumnLayoutView.axaml       （三栏布局）
│   └── Shell/
│       ├── LeftNavView.axaml
│       ├── CenterContentView.axaml
│       └── RightConversationView.axaml   （M3 仅占位）
├── ViewModels/
│   ├── MainWindowViewModel.cs
│   ├── WelcomeViewModel.cs
│   ├── ThreeColumnLayoutViewModel.cs
│   └── Shell/
│       ├── LeftNavViewModel.cs
│       ├── CenterContentViewModel.cs
│       └── RightConversationViewModel.cs （M3 仅占位）
├── Navigation/
│   ├── INavigationService.cs
│   ├── NavigationService.cs
│   ├── PageRegistry.cs                   （PageKey ↔ VM/View 映射）
│   └── IPage.cs                          （标识接口）
├── Theme/
│   ├── ThemeBridge.cs                    （PortableThemeStateController → Application.Resources）
│   ├── PalettesDark.axaml
│   ├── PalettesLight.axaml
│   └── CommonStyles.axaml
├── Controls/
│   ├── TitleBar.axaml
│   ├── SectionHeader.axaml
│   ├── BusyOverlay.axaml
│   ├── ToastHost.axaml
│   └── InlineDialog.axaml
├── Platform/
│   ├── IPlatformAdapter.cs               （接口占位；M5 填实现）
│   └── PlatformAdapterStub.cs
├── Infrastructure/
│   ├── DispatcherScheduler.cs            （把 portable Task 结果切到 UI 线程）
│   ├── AppLifecycle.cs                   （启动/恢复/退出钩子）
│   ├── WindowStateStore.cs               （window_state.json）
│   └── AppPaths.cs                       （应用数据/配置/日志目录解析）
└── Assets/                                （图标、字体资源）
```

`Tianming.MacMigration.sln` 加入 `Tianming.Desktop.Avalonia`。`Tianming.Desktop.Avalonia.csproj` 引用 `Tianming.ProjectData` / `Tianming.AI` / `Tianming.Framework`。

### 2.2 AppHost / DI 装配

```csharp
public static class AppHost
{
    public static IHost BuildHost(string[] args) =>
        Host.CreateApplicationBuilder(args)
            .ConfigureServices((ctx, s) =>
            {
                s.AddProjectDataServices();    // ProjectData 扩展方法（M3 新增）
                s.AddAIServices();             // AI 扩展方法
                s.AddFrameworkServices();      // Framework 扩展方法
                s.AddAvaloniaShellServices();  // Shell/VM/Nav/Theme
            })
            .Build();
}

internal static class AvaloniaShellServiceCollectionExtensions
{
    public static IServiceCollection AddAvaloniaShellServices(this IServiceCollection s)
    {
        s.AddSingleton<INavigationService, NavigationService>();
        s.AddSingleton<PageRegistry>();
        s.AddSingleton<WindowStateStore>();
        s.AddSingleton<ThemeBridge>();
        s.AddSingleton<AppLifecycle>();
        s.AddSingleton<MainWindowViewModel>();
        s.AddSingleton<ThreeColumnLayoutViewModel>();
        s.AddSingleton<LeftNavViewModel>();
        s.AddTransient<WelcomeViewModel>();
        // ... M3 涉及的所有 VM
        return s;
    }
}
```

每个 portable 类库新增一个 `ServiceCollectionExtensions.cs`（如 `AddProjectDataServices`），M3 负责建立约定与第一版，M4 扩展各模块 service。

### 2.3 导航设计

```csharp
public interface INavigationService
{
    Task NavigateAsync(PageKey key, object? parameter = null, CancellationToken ct = default);
    Task GoBackAsync(CancellationToken ct = default);
    bool CanGoBack { get; }
    PageKey? CurrentKey { get; }
    event EventHandler<PageKey> CurrentKeyChanged;
}

public readonly record struct PageKey(string Id);

public static class PageKeys
{
    public static readonly PageKey Dashboard = new("dashboard");
    public static readonly PageKey Welcome   = new("welcome");
    // M3 只注册 Welcome / Dashboard / Settings 三个壳
    // M4 扩展：Design/Generate/Validate/Editor/AI/Prompts/...
}
```

`PageRegistry` 持 `Dictionary<PageKey, (Type ViewModelType, Type ViewType)>`，由启动时静态注册。`NavigationService` 从 DI 取 ViewModel（常驻型用 Singleton、瞬时型用 Transient）、反射构造 View、把 VM 作为 DataContext 附加。

### 2.4 主题资源桥接

`ThemeBridge`：
- 订阅 `PortableThemeStateController.CurrentThemeChanged`（M1 已端口）
- 主题切换时：
  - 构造 Avalonia `Color`/`SolidColorBrush` 实例映射到 `PortableThemeResourcePalette` 的 brush key
  - 替换 `Application.Current.Resources["Color.Primary"]` 等 key
  - `RequestedThemeVariant = Light/Dark` 按 portable 状态值设置
- 冷启动先 apply 初始主题再 `AvaloniaXamlLoader.Load(app)`，避免 FOUC

`PalettesDark.axaml` / `PalettesLight.axaml` 只放静态默认值；运行时被 `ThemeBridge` 覆盖。

### 2.5 三栏布局

`ThreeColumnLayoutView`：
- `Grid ColumnDefinitions="{Binding LeftWidth}, Auto, *, Auto, {Binding RightWidth}"`
- 左/右列宽度绑定到 `WindowStateStore`，拖动 `GridSplitter` 持久化
- 中间列承载 `CenterContentView`（内 `ContentControl`，`Content=CurrentPage`）
- 响应式：窗口宽度 < 1000 时右栏自动折叠到抽屉（预留接口，M4 接入）

### 2.6 启动流程

```
Program.Main
  └─ AppHost.BuildHost(args)
      └─ 初始化 DI
  └─ BuildAvaloniaApp().StartWithClassicDesktopLifetime
      └─ App.OnFrameworkInitializationCompleted
          ├─ ThemeBridge.Initialize（apply 初始主题）
          ├─ AppLifecycle.OnStartup
          │   ├─ 读 window_state.json
          │   ├─ FileProjectManager.LoadLastOpenedAsync
          │   └─ 决定首页：Welcome or Dashboard
          └─ Show MainWindow
```

关闭流程：
- `MainWindow.Closing` → `AppLifecycle.OnShutdown`
- 写 `window_state.json`
- 广播 shutdown 给所有 service（`IAsyncDisposable`）
- `await Host.StopAsync()`

## 3. 工作拆分

### 3.1 Wave 0（主代理串行，~30 分钟）

1. 创建 `src/Tianming.Desktop.Avalonia/` 骨架（csproj、Program、App）
2. 把 sln 加入新项目
3. 引入 NuGet：`Avalonia` / `Avalonia.Desktop` / `Avalonia.Themes.Fluent` / `Avalonia.Diagnostics`（Debug 配置）/ `CommunityToolkit.Mvvm` / `Microsoft.Extensions.Hosting` / `Microsoft.Extensions.Logging` / `Serilog.Extensions.Logging` / `Serilog.Sinks.File`
4. 建立 `Directory.Packages.props` 集中版本（新建仓级文件）
5. 跑 `dotnet build sln` → 0 Warning / 0 Error
6. 建分支 `m3/avalonia-shell-2026-05-12`
7. 提交 Wave 0

### 3.2 Wave 1（5 个 agent 并行）

| ID | 范围 | 主要新增 |
|---|---|---|
| **S1** | AppHost + DI 扩展方法 | `AppHost.cs`、`AddProjectDataServices`、`AddAIServices`、`AddFrameworkServices`、`AddAvaloniaShellServices` + 单测（容器可解析所有 key singleton） |
| **S2** | Navigation 系统 | `INavigationService`、`NavigationService`、`PageRegistry`、`PageKeys`、`IPage`、返回栈、参数传递 + 单测 |
| **S3** | 主窗口 + 三栏布局 View/VM | `MainWindow.axaml(.cs)`、`ThreeColumnLayoutView.axaml(.cs)`、`MainWindowViewModel`、`ThreeColumnLayoutViewModel`、`WindowStateStore` + UI 冒烟测试（`Avalonia.Headless`） |
| **S4** | ThemeBridge + 样式基线 | `ThemeBridge.cs`、`PalettesLight.axaml`、`PalettesDark.axaml`、`CommonStyles.axaml`、初始主题应用 + 单测（主题切换后 Resources 键更新） |
| **S5** | Welcome + 项目生命周期 | `WelcomeView.axaml(.cs)`、`WelcomeViewModel`、新建项目命令、打开项目命令、最近项目列表、`AppLifecycle`、`AppPaths` + 单测 |

### 3.3 Wave 2（2 个 agent，依赖 Wave 1）

| ID | 依赖 | 范围 | 主要新增 |
|---|---|---|---|
| **S6** | S2/S3/S5 | 基础控件库占位 | `TitleBar`、`SectionHeader`、`BusyOverlay`、`ToastHost`、`InlineDialog` 五个控件的 axaml + cs + 样式 + 使用 demo 页 + UI 冒烟 |
| **S7** | S1-S6 | 冒烟集成 | 启动 → 新建项目 → 切主题 → 关闭 的端到端 `Avalonia.Headless` 场景测试 + Program.cs 收尾 |

## 4. 执行编排

### 4.1 Wave 0（主代理）

- 新建 `src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj`
- 新建 `Directory.Packages.props`
- 把 `Tianming.MacMigration.sln` 加入新项目
- 提交 Wave 0 commit

### 4.2 Wave 1（主代理单消息并行派发 5 agent）

5 个 agent 同时起跑，互不依赖（目录隔离：S1→Infrastructure、S2→Navigation、S3→Views/Shell、S4→Theme、S5→Welcome + AppPaths）。

### 4.3 Wave 1 合流

1. review 所有新增文件清单、扫违例（同 M1 硬门禁，无 WPF/Windows 绑定）
2. `dotnet build sln` → 0 错
3. `dotnet test sln -v minimal` → 全过
4. 5 个 commit，各 agent 一个：`feat(avalonia): DI host`、`feat(avalonia): navigation`、`feat(avalonia): main window + three column`、`feat(avalonia): theme bridge`、`feat(avalonia): welcome + lifecycle`

### 4.4 Wave 2（主代理并行派发 2 agent）

S6/S7 并行，S7 需等待 S6 的控件能 import 才能写集成测。实际把 S7 排在 S6 合流之后：

- Wave 2.0：派 S6
- Wave 2.0 合流：commit `feat(avalonia): control shells`
- Wave 2.1：派 S7
- Wave 2.1 合流：commit `test(avalonia): end-to-end smoke`

### 4.5 收尾

1. 手动冒烟：macOS 启动 `dotnet run --project src/Tianming.Desktop.Avalonia` → 观察到三栏空壳
2. 新建项目、切主题、关闭、重开 → 状态恢复
3. 更新 `Docs/macOS迁移/功能对齐矩阵.md`：`主窗口` / `三栏布局` / `左侧导航` / `主题管理` 等相关行状态更新（部分标注"框架已就位，页面留 M4"）
4. commit `docs(macos): M3 shell 验证点`
5. push

## 5. 验收门禁

### 5.1 Agent 硬门禁（每个 agent 必满足）

1. 不出现 `using System.Windows`、`using TM.App`、`using NAudio`、`System.Speech`、`Microsoft.Web.WebView2`、`System.Management`、`ProtectedData`、注册表 P/Invoke、`System.Drawing`
2. 不引 WPF NuGet；不引 Avalonia alpha/preview 包
3. `dotnet test tests/...` 对该 agent 产出的测试全过
4. 只新增文件，不动既有 M1/M2 目录
5. 不 commit

### 5.2 主代理合流门禁

1. `dotnet build Tianming.MacMigration.sln` → 0 Warning / 0 Error
2. `dotnet test Tianming.MacMigration.sln -v minimal` → 全过（M2 基线 + M3 增量）
3. `Scripts/check-windows-bindings.sh src/ tests/` → 0 命中
4. macOS 本机 `dotnet run` 启动无 crash、三栏空壳可视

## 6. 测试策略

| 测试类别 | 技术 | 覆盖 |
|---|---|---|
| VM 单元测试 | xUnit + Moq | ViewModel 命令、状态变更、Navigation 调用 |
| DI 装配测试 | xUnit | 解析所有 singleton 不抛异常 |
| Navigation 行为 | xUnit | 导航栈、参数传递、页面缓存策略 |
| ThemeBridge | xUnit | 主题切换后 Resources 键值变化 |
| UI 冒烟 | Avalonia.Headless + xUnit | MainWindow 加载、三栏布局渲染、新建项目按钮响应 |
| 端到端场景 | Avalonia.Headless | Welcome → 新建项目 → 主窗口 → 切主题 → 关闭 |

M3 预计新增测试 ≥ 60 用例。

## 7. 风险与回滚

### 7.1 主要风险

| ID | 风险 | 影响 | 缓解 |
|---|---|---|---|
| R1 | Avalonia 11.x 主题 API 与 portable brush palette 映射不齐 | ThemeBridge 覆盖不全 → 视觉不一致 | Wave 1 S4 先跑 `PalettesLight.axaml` 最小集；缺失 key 加入 M4 遗留清单 |
| R2 | `CommunityToolkit.Mvvm` Source Generator 与 AOT/R2R 冲突 | 构建失败 | M3 不启用 AOT；R2R 留 M7；若生成器行为异常降级到手写 INotifyPropertyChanged |
| R3 | `Microsoft.Extensions.Hosting` 与 Avalonia classic desktop lifetime 互斥 | 启动失败 | 先 Build Host、再 Start Avalonia；Shutdown 时反向关闭；参考 Avalonia docs sample |
| R4 | Avalonia.Headless 测试在 macOS CI 不稳定 | UI 冒烟 flaky | S7 只测关键路径；flaky 用例加 `[Fact(Skip="headless-flaky")]` 并记录 |
| R5 | Window 状态持久化读坏 | 启动崩 | `WindowStateStore` 坏 JSON 恢复默认值；首次启动不读取；原子保存 |
| R6 | `FluentTheme` 与自定义 accent 交互处发生样式级联污染 | 控件样式异常 | CommonStyles.axaml 用 `Selector` 精确命中；避免 `Window /` 全局选择器 |

### 7.2 回滚策略

- 独立分支 `m3/avalonia-shell-2026-05-12`，弃分支即可完全回滚
- 每 Wave 独立 commit 组
- NuGet 升级单独 commit，便于 revert

### 7.3 退路

- 若 R1/R6 连环发生：M3 仅保证"空主窗口 + 左导航占位能点击 + 右占位"，主题与控件细节留 M4 同步打磨
- 若 R3 发生：去掉 `Microsoft.Extensions.Hosting` 依赖，退到裸 `ServiceCollection.BuildServiceProvider`

## 8. 验收标准

M3 完成的判据：

1. `dotnet build Tianming.MacMigration.sln` → 0 Warning / 0 Error
2. `dotnet test Tianming.MacMigration.sln -v minimal` → 全过（较 M2 基线增加 ≥ 60 用例）
3. `Scripts/check-windows-bindings.sh src/ tests/` → 0 命中
4. macOS 本机 `dotnet run --project src/Tianming.Desktop.Avalonia` 成功启动，主窗口三栏布局可视，冷启动 < 3 秒
5. 冒烟场景：无项目 → 显示 Welcome；点"新建项目" → 落盘项目并进入主窗口；切换主题 → 视觉即时变化；关闭重开 → 窗口位置/尺寸/主题恢复
6. `Docs/macOS迁移/功能对齐矩阵.md` 中 `主窗口`、`三栏布局`、`主题管理` 三行新增"框架已就位"注记
7. 分支 `m3/avalonia-shell-2026-05-12` 已 push

完成后进入 M4（核心模块页面迁移）。
