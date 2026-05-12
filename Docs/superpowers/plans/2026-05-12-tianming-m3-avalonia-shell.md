# 天命 macOS 迁移 — M3 Avalonia 基础 Shell Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建一个能启动的 Avalonia 主窗口，带 DI + MVVM + 三栏布局 + 导航 + 主题桥接 + 窗口状态持久化。无业务页面（M4 再填）。最终交付物：`dotnet run --project src/Tianming.Desktop.Avalonia` 启动 → 三栏主窗口 → 切主题可视变化 → 关闭重开状态恢复。

**Architecture:** 新建 `src/Tianming.Desktop.Avalonia/` 项目（net8.0，macOS arm64 + x64）。DI 走 `Microsoft.Extensions.DependencyInjection`（不引 `Hosting` 全家桶）；MVVM 走 `CommunityToolkit.Mvvm` 源生成器。`AppHost.Build()` 装配 ServiceCollection，各 portable 类库（Tianming.ProjectData / Tianming.AI / Tianming.Framework）暴露 `AddXxxServices` 扩展方法。Shell 三栏布局用 Avalonia `Grid` + `GridSplitter`，中央区 `ContentControl` 绑定 `CurrentPage`。导航走 `INavigationService` + `PageRegistry`，按 `PageKey` 从 DI 取 VM、反射建 View、设 DataContext。主题走 `ThemeBridge`：订阅 `PortableThemeStateController.ThemeChanged`，把 `PortableThemeBrushPalette` 里的 brush 值映射到 `SolidColorBrush` 替换 `Application.Current.Resources`。

**Tech Stack:** .NET 8.0 / Avalonia 11.x（`Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`, `Avalonia.Diagnostics`）/ `CommunityToolkit.Mvvm 8.x` / `Microsoft.Extensions.DependencyInjection 8.x` / `Microsoft.Extensions.Logging.Console 8.x` / xUnit。

**Spec:** `Docs/superpowers/specs/2026-05-12-tianming-m3-avalonia-shell-design.md`（位于分支 `m2-m6/specs-2026-05-12`）。

## Scope Alignment

对齐仓库真实状态后的补充：

- 仓里**没有任何现存的 `ServiceCollectionExtensions`**。M3 要从零建立 `AddProjectDataServices`、`AddAIServices`、`AddFrameworkServices`、`AddAvaloniaShell` 四个扩展方法，每个注册该类库里几个常用 service。
- `PortableThemeStateController` **已在 `src/Tianming.Framework/Appearance/`**，事件名为 `ThemeChanged`（不是 spec 里笔误的 `CurrentThemeChanged`）；`PortableThemeBrushPalette` 提供 brush 映射。
- 仓里**没有"项目管理器"portable 类**（`FileProjectManager` 不存在）。M3 暂时不引入 portable 项目管理，`WelcomeView` 的"新建/打开"两个按钮只做 UI 骨架，实际 "创建项目" 调用 `WritingNavigationCatalog` + 文件夹创建（M4 替换为真项目管理器 portable service 或用 `Tianming.ProjectData.ProjectSystem` 下现有 API — 需探索）。
- 简化：M3 仅要求"新建"按钮能落盘一个空目录作为项目根；项目元数据的严格定义留 M4。
- `WritingNavigationCatalog` 已在 `src/Tianming.ProjectData/Navigation/`，M3 的左栏导航用它拉全部一级分类，点击跳对应 `PageKey`（只有 `Dashboard` / `Welcome` / `Settings` 三个 stub 页在 M3 有 View，其他跳"空白 Placeholder 页"）。

## File Structure

**新建文件（项目骨架）：**
- `src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj` — 项目文件
- `src/Tianming.Desktop.Avalonia/Program.cs` — 入口
- `src/Tianming.Desktop.Avalonia/App.axaml`、`App.axaml.cs` — Avalonia Application
- `src/Tianming.Desktop.Avalonia/AppHost.cs` — ServiceCollection 装配

**新建文件（核心 infra）：**
- `src/Tianming.Desktop.Avalonia/Infrastructure/AppPaths.cs` — 标准 macOS 目录解析
- `src/Tianming.Desktop.Avalonia/Infrastructure/WindowStateStore.cs` — window_state.json
- `src/Tianming.Desktop.Avalonia/Infrastructure/DispatcherScheduler.cs` — `Task` → UI 线程
- `src/Tianming.Desktop.Avalonia/Infrastructure/AppLifecycle.cs` — startup/shutdown 钩子

**新建文件（导航）：**
- `src/Tianming.Desktop.Avalonia/Navigation/INavigationService.cs`
- `src/Tianming.Desktop.Avalonia/Navigation/NavigationService.cs`
- `src/Tianming.Desktop.Avalonia/Navigation/PageRegistry.cs`
- `src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs`
- `src/Tianming.Desktop.Avalonia/Navigation/IPage.cs` — 标识接口

**新建文件（主题）：**
- `src/Tianming.Desktop.Avalonia/Theme/ThemeBridge.cs`
- `src/Tianming.Desktop.Avalonia/Theme/PalettesLight.axaml`
- `src/Tianming.Desktop.Avalonia/Theme/PalettesDark.axaml`
- `src/Tianming.Desktop.Avalonia/Theme/CommonStyles.axaml`

**新建文件（View + ViewModel）：**
- `src/Tianming.Desktop.Avalonia/Views/MainWindow.axaml`、`.axaml.cs`
- `src/Tianming.Desktop.Avalonia/Views/WelcomeView.axaml`、`.axaml.cs`
- `src/Tianming.Desktop.Avalonia/Views/ThreeColumnLayoutView.axaml`、`.axaml.cs`
- `src/Tianming.Desktop.Avalonia/Views/PlaceholderView.axaml`、`.axaml.cs` — 未实装 PageKey 的 fallback
- `src/Tianming.Desktop.Avalonia/Views/Shell/LeftNavView.axaml`、`.axaml.cs`
- `src/Tianming.Desktop.Avalonia/Views/Shell/RightConversationView.axaml`、`.axaml.cs` — M3 仅占位文本
- `src/Tianming.Desktop.Avalonia/ViewModels/MainWindowViewModel.cs`
- `src/Tianming.Desktop.Avalonia/ViewModels/WelcomeViewModel.cs`
- `src/Tianming.Desktop.Avalonia/ViewModels/ThreeColumnLayoutViewModel.cs`
- `src/Tianming.Desktop.Avalonia/ViewModels/PlaceholderViewModel.cs`
- `src/Tianming.Desktop.Avalonia/ViewModels/Shell/LeftNavViewModel.cs`
- `src/Tianming.Desktop.Avalonia/ViewModels/Shell/RightConversationViewModel.cs`

**新建文件（DI 扩展方法）：**
- `src/Tianming.ProjectData/ServiceCollectionExtensions.cs`
- `src/Tianming.AI/ServiceCollectionExtensions.cs`
- `src/Tianming.Framework/ServiceCollectionExtensions.cs`
- `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`

**新建文件（测试）：**
- `tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj`
- `tests/Tianming.Desktop.Avalonia.Tests/Navigation/NavigationServiceTests.cs`
- `tests/Tianming.Desktop.Avalonia.Tests/Theme/ThemeBridgeTests.cs`
- `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/WindowStateStoreTests.cs`
- `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/AppPathsTests.cs`
- `tests/Tianming.Desktop.Avalonia.Tests/DI/AppHostTests.cs`

**修改文件：**
- `Tianming.MacMigration.sln` — 加 2 个新项目（App + Tests）

---

## Task 0：基线确认

- [ ] **Step 0.1：确认在正确 worktree**

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer/.worktrees/m2-parallel
git branch --show-current
```

Expected: `m2/onnx-localfetch-2026-05-12`（M3 plan 暂在此 worktree 落盘；未来 M3 实施时开新 worktree 再 cherry-pick plan 文件）。

- [ ] **Step 0.2：跑基线测试**

```bash
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -6
```

Expected: 1132 或 1143（若 M2 已合入）全过。

---

## Task 1：新建 Avalonia 项目骨架 + 挂 sln

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj`
- Create: `src/Tianming.Desktop.Avalonia/Program.cs`
- Create: `src/Tianming.Desktop.Avalonia/App.axaml`
- Create: `src/Tianming.Desktop.Avalonia/App.axaml.cs`
- Create: `src/Tianming.Desktop.Avalonia/Views/MainWindow.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Views/MainWindow.axaml.cs`
- Modify: `Tianming.MacMigration.sln`

- [ ] **Step 1.1：写 csproj**

创建 `src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Avalonia" Version="11.0.10" />
    <PackageReference Include="Avalonia.Desktop" Version="11.0.10" />
    <PackageReference Include="Avalonia.Themes.Fluent" Version="11.0.10" />
    <PackageReference Include="Avalonia.Fonts.Inter" Version="11.0.10" />
    <PackageReference Condition="'$(Configuration)' == 'Debug'" Include="Avalonia.Diagnostics" Version="11.0.10" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../Tianming.Framework/Tianming.Framework.csproj" />
    <ProjectReference Include="../Tianming.ProjectData/Tianming.ProjectData.csproj" />
    <ProjectReference Include="../Tianming.AI/Tianming.AI.csproj" />
  </ItemGroup>
</Project>
```

创建 `src/Tianming.Desktop.Avalonia/app.manifest`（Windows 元数据；macOS 上无害但 Avalonia 模板要求存在）：

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="Tianming.Desktop.Avalonia.app"/>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true</dpiAware>
    </windowsSettings>
  </application>
</assembly>
```

> 若 Avalonia 11.0.10 在执行当日 NuGet 不是 latest stable，改用最新 11.x（`dotnet search Avalonia --prerelease false` 可查）。

- [ ] **Step 1.2：写 Program.cs**

创建 `src/Tianming.Desktop.Avalonia/Program.cs`：

```csharp
using System;
using Avalonia;
using Tianming.Desktop.Avalonia;

namespace Tianming.Desktop.Avalonia;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
        => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
```

- [ ] **Step 1.3：写 App.axaml + App.axaml.cs**

创建 `src/Tianming.Desktop.Avalonia/App.axaml`：

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Tianming.Desktop.Avalonia.App"
             RequestedThemeVariant="Default">
  <Application.Styles>
    <FluentTheme />
  </Application.Styles>
</Application>
```

创建 `src/Tianming.Desktop.Avalonia/App.axaml.cs`：

```csharp
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Tianming.Desktop.Avalonia.Views;

namespace Tianming.Desktop.Avalonia;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
```

- [ ] **Step 1.4：写 MainWindow 最小空壳**

创建 `src/Tianming.Desktop.Avalonia/Views/MainWindow.axaml`：

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        x:Class="Tianming.Desktop.Avalonia.Views.MainWindow"
        Title="天命" Width="1200" Height="800">
  <TextBlock Text="天命 macOS — Hello, Avalonia!" HorizontalAlignment="Center" VerticalAlignment="Center" FontSize="24"/>
</Window>
```

创建 `src/Tianming.Desktop.Avalonia/Views/MainWindow.axaml.cs`：

```csharp
using Avalonia.Controls;

namespace Tianming.Desktop.Avalonia.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();
}
```

- [ ] **Step 1.5：sln 加入新项目**

```bash
dotnet sln Tianming.MacMigration.sln add src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj
```

Expected: `Project ... added to the solution.`

- [ ] **Step 1.6：build + run**

```bash
dotnet build src/Tianming.Desktop.Avalonia/ --nologo -v q 2>&1 | tail -4
```

Expected: 0 error 0 warning。

```bash
dotnet run --project src/Tianming.Desktop.Avalonia/ &
APP_PID=$!
sleep 3
kill $APP_PID 2>/dev/null
```

Expected: 3 秒内弹出窗口标题"天命"，内容"天命 macOS — Hello, Avalonia!"；`kill` 后进程退出。（手工验证；若你不在 macOS GUI 会话，跳过 run 只确认 build 过）。

- [ ] **Step 1.7：commit**

```bash
git add src/Tianming.Desktop.Avalonia/ Tianming.MacMigration.sln
git commit -m "feat(desktop): M3 Avalonia 项目骨架 + 空主窗口"
```

---

## Task 2：AppPaths（macOS 标准目录）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/AppPaths.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/AppPathsTests.cs`
- Modify: `Tianming.MacMigration.sln`

`AppPaths` 职责：解析 `~/Library/Application Support/Tianming/`、`~/Library/Caches/Tianming/`、`~/Library/Logs/Tianming/` 三个标准目录，目录不存在则创建。

- [ ] **Step 2.1：新建测试项目**

创建 `tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="../../src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj" />
  </ItemGroup>
</Project>
```

sln 加入：
```bash
dotnet sln Tianming.MacMigration.sln add tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj
```

- [ ] **Step 2.2：写失败的测试**

创建 `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/AppPathsTests.cs`：

```csharp
using System;
using System.IO;
using Tianming.Desktop.Avalonia.Infrastructure;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class AppPathsTests
{
    [Fact]
    public void AppSupport_UnderLibraryApplicationSupport()
    {
        var p = AppPaths.Default.AppSupportDirectory;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.StartsWith(Path.Combine(home, "Library", "Application Support"), p);
        Assert.EndsWith("Tianming", p);
    }

    [Fact]
    public void CustomRoot_OverridesAllPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tianming-test-{Guid.NewGuid():N}");
        try
        {
            var paths = new AppPaths(root);
            Assert.Equal(Path.Combine(root, "Application Support", "Tianming"), paths.AppSupportDirectory);
            Assert.Equal(Path.Combine(root, "Caches", "Tianming"), paths.CachesDirectory);
            Assert.Equal(Path.Combine(root, "Logs", "Tianming"), paths.LogsDirectory);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EnsureDirectories_CreatesAll()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tianming-test-{Guid.NewGuid():N}");
        try
        {
            var paths = new AppPaths(root);
            paths.EnsureDirectories();
            Assert.True(Directory.Exists(paths.AppSupportDirectory));
            Assert.True(Directory.Exists(paths.CachesDirectory));
            Assert.True(Directory.Exists(paths.LogsDirectory));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
```

- [ ] **Step 2.3：写实现**

创建 `src/Tianming.Desktop.Avalonia/Infrastructure/AppPaths.cs`：

```csharp
using System;
using System.IO;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class AppPaths
{
    public string AppSupportDirectory { get; }
    public string CachesDirectory { get; }
    public string LogsDirectory { get; }

    public AppPaths(string libraryRoot)
    {
        AppSupportDirectory = Path.Combine(libraryRoot, "Application Support", "Tianming");
        CachesDirectory     = Path.Combine(libraryRoot, "Caches", "Tianming");
        LogsDirectory       = Path.Combine(libraryRoot, "Logs", "Tianming");
    }

    public static AppPaths Default { get; } = Create();

    private static AppPaths Create()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new AppPaths(Path.Combine(home, "Library"));
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(AppSupportDirectory);
        Directory.CreateDirectory(CachesDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
```

- [ ] **Step 2.4：跑测试**

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ --nologo -v q 2>&1 | tail -4
```

Expected: 3 测试全过。

- [ ] **Step 2.5：commit**

```bash
git add src/Tianming.Desktop.Avalonia/Infrastructure/AppPaths.cs \
        tests/Tianming.Desktop.Avalonia.Tests/ \
        Tianming.MacMigration.sln
git commit -m "feat(desktop): AppPaths + 测试项目骨架"
```

---

## Task 3：WindowStateStore（window_state.json 持久化）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/WindowStateStore.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/WindowStateStoreTests.cs`

- [ ] **Step 3.1：写失败的测试**

创建 `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/WindowStateStoreTests.cs`：

```csharp
using System;
using System.IO;
using Tianming.Desktop.Avalonia.Infrastructure;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class WindowStateStoreTests
{
    private static string TempRoot() => Path.Combine(Path.GetTempPath(), $"tianming-{Guid.NewGuid():N}");

    [Fact]
    public void Load_WhenFileMissing_ReturnsDefault()
    {
        var dir = TempRoot();
        Directory.CreateDirectory(dir);
        try
        {
            var store = new WindowStateStore(Path.Combine(dir, "window_state.json"));
            var state = store.Load();
            Assert.Equal(1200.0, state.Width);
            Assert.Equal(800.0,  state.Height);
            Assert.Equal(240.0,  state.LeftColumnWidth);
            Assert.Equal(360.0,  state.RightColumnWidth);
            Assert.False(state.IsMaximized);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void SaveThenLoad_RoundTrip()
    {
        var dir = TempRoot();
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "window_state.json");
            var store = new WindowStateStore(path);
            var saved = new WindowState(X: 100, Y: 200, Width: 1400, Height: 900,
                LeftColumnWidth: 300, RightColumnWidth: 400, IsMaximized: true);
            store.Save(saved);
            var loaded = store.Load();
            Assert.Equal(saved, loaded);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Load_WhenFileCorrupted_ReturnsDefault()
    {
        var dir = TempRoot();
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "window_state.json");
            File.WriteAllText(path, "{ not valid json");
            var store = new WindowStateStore(path);
            var state = store.Load();
            Assert.Equal(1200.0, state.Width);
        }
        finally { Directory.Delete(dir, true); }
    }
}
```

- [ ] **Step 3.2：写实现**

创建 `src/Tianming.Desktop.Avalonia/Infrastructure/WindowStateStore.cs`：

```csharp
using System;
using System.IO;
using System.Text.Json;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed record WindowState(
    double X = double.NaN,
    double Y = double.NaN,
    double Width = 1200,
    double Height = 800,
    double LeftColumnWidth = 240,
    double RightColumnWidth = 360,
    bool IsMaximized = false);

public sealed class WindowStateStore
{
    private readonly string _filePath;

    public WindowStateStore(string filePath) { _filePath = filePath; }

    public WindowState Load()
    {
        if (!File.Exists(_filePath)) return new WindowState();
        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<WindowState>(json) ?? new WindowState();
        }
        catch (JsonException) { return new WindowState(); }
    }

    public void Save(WindowState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath) ?? ".");
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }
}
```

- [ ] **Step 3.3：跑测试**

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ --nologo --filter "WindowStateStoreTests" -v q 2>&1 | tail -4
```

Expected: 3 过。

- [ ] **Step 3.4：commit**

```bash
git add src/Tianming.Desktop.Avalonia/Infrastructure/WindowStateStore.cs \
        tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/WindowStateStoreTests.cs
git commit -m "feat(desktop): WindowStateStore 持久化"
```

---

## Task 4：导航系统（INavigationService + PageRegistry）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs`
- Create: `src/Tianming.Desktop.Avalonia/Navigation/IPage.cs`
- Create: `src/Tianming.Desktop.Avalonia/Navigation/INavigationService.cs`
- Create: `src/Tianming.Desktop.Avalonia/Navigation/PageRegistry.cs`
- Create: `src/Tianming.Desktop.Avalonia/Navigation/NavigationService.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Navigation/NavigationServiceTests.cs`

- [ ] **Step 4.1：写 PageKeys + IPage + INavigationService 契约**

创建 `src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs`：

```csharp
namespace Tianming.Desktop.Avalonia.Navigation;

public readonly record struct PageKey(string Id);

public static class PageKeys
{
    public static readonly PageKey Welcome   = new("welcome");
    public static readonly PageKey Dashboard = new("dashboard");
    public static readonly PageKey Settings  = new("settings");
    // M4 扩展更多
}
```

创建 `src/Tianming.Desktop.Avalonia/Navigation/IPage.cs`：

```csharp
namespace Tianming.Desktop.Avalonia.Navigation;

/// <summary>Marker interface for page ViewModels (optional; facilitates filtering).</summary>
public interface IPage { }
```

创建 `src/Tianming.Desktop.Avalonia/Navigation/INavigationService.cs`：

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Tianming.Desktop.Avalonia.Navigation;

public interface INavigationService
{
    PageKey? CurrentKey { get; }
    object?  CurrentViewModel { get; }
    bool     CanGoBack { get; }

    Task NavigateAsync(PageKey key, object? parameter = null, CancellationToken ct = default);
    Task GoBackAsync(CancellationToken ct = default);

    event EventHandler<PageKey>? CurrentKeyChanged;
}
```

- [ ] **Step 4.2：写 PageRegistry**

创建 `src/Tianming.Desktop.Avalonia/Navigation/PageRegistry.cs`：

```csharp
using System;
using System.Collections.Generic;

namespace Tianming.Desktop.Avalonia.Navigation;

public sealed class PageRegistry
{
    private readonly Dictionary<PageKey, (Type ViewModelType, Type ViewType)> _map = new();

    public void Register<TViewModel, TView>(PageKey key)
        where TViewModel : class
        where TView      : class
    {
        _map[key] = (typeof(TViewModel), typeof(TView));
    }

    public bool TryResolve(PageKey key, out Type viewModelType, out Type viewType)
    {
        if (_map.TryGetValue(key, out var pair))
        {
            viewModelType = pair.ViewModelType;
            viewType      = pair.ViewType;
            return true;
        }
        viewModelType = typeof(object);
        viewType      = typeof(object);
        return false;
    }

    public IReadOnlyCollection<PageKey> Keys => _map.Keys;
}
```

- [ ] **Step 4.3：写失败的 NavigationService 测试**

创建 `tests/Tianming.Desktop.Avalonia.Tests/Navigation/NavigationServiceTests.cs`：

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia.Navigation;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Navigation;

public class NavigationServiceTests
{
    private sealed class FakeVm1 { }
    private sealed class FakeVm2 { }
    private sealed class FakeView { }

    private static (NavigationService nav, PageRegistry reg) Build()
    {
        var reg = new PageRegistry();
        reg.Register<FakeVm1, FakeView>(PageKeys.Welcome);
        reg.Register<FakeVm2, FakeView>(PageKeys.Dashboard);
        var services = new ServiceCollection();
        services.AddTransient<FakeVm1>();
        services.AddTransient<FakeVm2>();
        var sp = services.BuildServiceProvider();
        return (new NavigationService(sp, reg), reg);
    }

    [Fact]
    public async Task Navigate_ResolvesVmFromDI()
    {
        var (nav, _) = Build();
        await nav.NavigateAsync(PageKeys.Welcome);
        Assert.Equal(PageKeys.Welcome, nav.CurrentKey);
        Assert.IsType<FakeVm1>(nav.CurrentViewModel);
    }

    [Fact]
    public async Task Navigate_FiresEvent()
    {
        var (nav, _) = Build();
        PageKey? fired = null;
        nav.CurrentKeyChanged += (_, k) => fired = k;
        await nav.NavigateAsync(PageKeys.Welcome);
        Assert.Equal(PageKeys.Welcome, fired);
    }

    [Fact]
    public async Task GoBack_RestoresPrevious()
    {
        var (nav, _) = Build();
        await nav.NavigateAsync(PageKeys.Welcome);
        await nav.NavigateAsync(PageKeys.Dashboard);
        Assert.True(nav.CanGoBack);
        await nav.GoBackAsync();
        Assert.Equal(PageKeys.Welcome, nav.CurrentKey);
        Assert.False(nav.CanGoBack);
    }

    [Fact]
    public async Task Navigate_UnknownKey_Throws()
    {
        var (nav, _) = Build();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => nav.NavigateAsync(new PageKey("unknown")).AsTaskOrTask());
    }
}

internal static class TaskExtensions
{
    public static Task AsTaskOrTask(this Task task) => task;
}
```

- [ ] **Step 4.4：写 NavigationService 实现**

创建 `src/Tianming.Desktop.Avalonia/Navigation/NavigationService.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Tianming.Desktop.Avalonia.Navigation;

public sealed class NavigationService : INavigationService
{
    private readonly IServiceProvider _sp;
    private readonly PageRegistry _registry;
    private readonly Stack<(PageKey Key, object Vm)> _stack = new();

    public NavigationService(IServiceProvider sp, PageRegistry registry)
    {
        _sp = sp;
        _registry = registry;
    }

    public PageKey? CurrentKey       => _stack.Count == 0 ? null : _stack.Peek().Key;
    public object?  CurrentViewModel => _stack.Count == 0 ? null : _stack.Peek().Vm;
    public bool     CanGoBack        => _stack.Count > 1;

    public event EventHandler<PageKey>? CurrentKeyChanged;

    public Task NavigateAsync(PageKey key, object? parameter = null, CancellationToken ct = default)
    {
        if (!_registry.TryResolve(key, out var vmType, out _))
            throw new InvalidOperationException($"PageKey 未注册：{key.Id}");

        var vm = _sp.GetRequiredService(vmType);
        _stack.Push((key, vm));
        CurrentKeyChanged?.Invoke(this, key);
        return Task.CompletedTask;
    }

    public Task GoBackAsync(CancellationToken ct = default)
    {
        if (!CanGoBack) return Task.CompletedTask;
        _stack.Pop();
        CurrentKeyChanged?.Invoke(this, CurrentKey!.Value);
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 4.5：跑测试**

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ --nologo --filter "NavigationServiceTests" -v q 2>&1 | tail -4
```

Expected: 4 过。

- [ ] **Step 4.6：commit**

```bash
git add src/Tianming.Desktop.Avalonia/Navigation/ \
        tests/Tianming.Desktop.Avalonia.Tests/Navigation/
git commit -m "feat(desktop): 导航系统（PageKey/Registry/NavigationService）"
```

---

## Task 5：DI 扩展方法（AddXxxServices）+ AppHost

**Files:**
- Create: `src/Tianming.ProjectData/ServiceCollectionExtensions.cs`
- Create: `src/Tianming.AI/ServiceCollectionExtensions.cs`
- Create: `src/Tianming.Framework/ServiceCollectionExtensions.cs`
- Create: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`
- Create: `src/Tianming.Desktop.Avalonia/AppHost.cs`
- Modify: `src/Tianming.ProjectData/Tianming.ProjectData.csproj`（加 MS.DI 包引用）
- Modify: `src/Tianming.AI/Tianming.AI.csproj`（加 MS.DI 包引用 + 新文件 Compile Include）
- Modify: `src/Tianming.Framework/Tianming.Framework.csproj`（加 MS.DI 包引用）
- Create: `tests/Tianming.Desktop.Avalonia.Tests/DI/AppHostTests.cs`

各 `AddXxxServices` 只注册"M3 用得到的"service；M4+ 扩展时每个页面再按需 `services.AddTransient<XxxViewModel>` 等。

- [ ] **Step 5.1：加 MS.DI 包到三个 portable csproj**

对 `src/Tianming.ProjectData/Tianming.ProjectData.csproj`、`src/Tianming.AI/Tianming.AI.csproj`、`src/Tianming.Framework/Tianming.Framework.csproj` 各自的 `<ItemGroup>` 加：

```xml
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="8.0.0" />
```

（仅抽象层即可，实现在 Avalonia 项目里有。）

- [ ] **Step 5.2：写三个 portable 的 ServiceCollectionExtensions**

创建 `src/Tianming.ProjectData/ServiceCollectionExtensions.cs`：

```csharp
using Microsoft.Extensions.DependencyInjection;
using TM.Services.Modules.ProjectData.Navigation;

namespace TM.Services.Modules.ProjectData;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProjectDataServices(this IServiceCollection s)
    {
        // WritingNavigationCatalog 是 static 类，不需要注册
        // 更多 portable service 在 M4 按需加
        return s;
    }
}
```

创建 `src/Tianming.AI/ServiceCollectionExtensions.cs`：

```csharp
using Microsoft.Extensions.DependencyInjection;
using TM.Services.Framework.AI.Core;
using TM.Services.Framework.AI.SemanticKernel;

namespace TM.Services.Framework.AI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAIServices(this IServiceCollection s)
    {
        s.AddSingleton<IApiKeySecretStore, MacOSKeychainApiKeySecretStore>();
        s.AddSingleton<ITextEmbedder>(_ => OnnxEmbedderFactory.Create(EmbeddingSettings.Default));
        // M4+ 继续加 FileAIConfigurationStore、FilePromptTemplateStore 等
        return s;
    }
}
```

> 注：若 M2 尚未 merge（无 `OnnxEmbedderFactory` / `EmbeddingSettings`），改为 `s.AddSingleton<ITextEmbedder>(_ => new HashingTextEmbedder(256));`

创建 `src/Tianming.Framework/ServiceCollectionExtensions.cs`：

```csharp
using Microsoft.Extensions.DependencyInjection;
using TM.Framework.Appearance;

namespace TM.Framework;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFrameworkServices(this IServiceCollection s)
    {
        // PortableThemeStateController 需要构造器参数（state、applyThemeAsync），
        // 在 AvaloniaShell 扩展里注册具名工厂，此处仅作预留。
        return s;
    }
}
```

- [ ] **Step 5.3：在 Tianming.AI.csproj 为新 ServiceCollectionExtensions.cs 加 Compile Include**

编辑 `src/Tianming.AI/Tianming.AI.csproj`，在最后一个 `<Compile Include="SemanticKernel/FileSessionStore.cs" />`（或 M2 末尾的 `<Compile Include="SemanticKernel/Embedding/OnnxEmbedderFactory.cs" />`）之后插入：

```xml
    <Compile Include="ServiceCollectionExtensions.cs" />
```

> 另两个 portable csproj 是否 `EnableDefaultCompileItems=false`？M3 实施时 `grep -l "EnableDefaultCompileItems" src/*/Tianming.*.csproj` 确认；只有 `Tianming.AI.csproj` 是 explicit，其他两个走默认 glob。若都是 explicit，那两个也得加 `<Compile Include="ServiceCollectionExtensions.cs" />`。

- [ ] **Step 5.4：写 AvaloniaShellServiceCollectionExtensions**

创建 `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`：

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TM.Framework.Appearance;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Theme;
using Tianming.Desktop.Avalonia.ViewModels;
using Tianming.Desktop.Avalonia.ViewModels.Shell;
using Tianming.Desktop.Avalonia.Views;
using Tianming.Desktop.Avalonia.Views.Shell;

namespace Tianming.Desktop.Avalonia;

public static class AvaloniaShellServiceCollectionExtensions
{
    public static IServiceCollection AddAvaloniaShell(this IServiceCollection s)
    {
        // Infra
        s.AddSingleton(AppPaths.Default);
        s.AddSingleton(sp => new WindowStateStore(
            System.IO.Path.Combine(sp.GetRequiredService<AppPaths>().AppSupportDirectory, "window_state.json")));
        s.AddSingleton<AppLifecycle>();
        s.AddSingleton<DispatcherScheduler>();

        // Theme
        s.AddSingleton<PortableThemeState>(_ => new PortableThemeState());
        s.AddSingleton<PortableThemeStateController>(sp =>
        {
            var state = sp.GetRequiredService<PortableThemeState>();
            var bridge = sp.GetRequiredService<ThemeBridge>();
            return new PortableThemeStateController(state, bridge.ApplyAsync);
        });
        s.AddSingleton<ThemeBridge>();

        // Navigation
        s.AddSingleton<PageRegistry>(_ => RegisterPages(new PageRegistry()));
        s.AddSingleton<INavigationService, NavigationService>();

        // ViewModels
        s.AddSingleton<MainWindowViewModel>();
        s.AddSingleton<ThreeColumnLayoutViewModel>();
        s.AddSingleton<LeftNavViewModel>();
        s.AddSingleton<RightConversationViewModel>();
        s.AddTransient<WelcomeViewModel>();
        s.AddTransient<PlaceholderViewModel>();

        return s;
    }

    private static PageRegistry RegisterPages(PageRegistry reg)
    {
        reg.Register<WelcomeViewModel,     WelcomeView>(PageKeys.Welcome);
        reg.Register<PlaceholderViewModel, PlaceholderView>(PageKeys.Dashboard);
        reg.Register<PlaceholderViewModel, PlaceholderView>(PageKeys.Settings);
        return reg;
    }
}
```

- [ ] **Step 5.5：写 AppHost**

创建 `src/Tianming.Desktop.Avalonia/AppHost.cs`：

```csharp
using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TM.Framework;
using TM.Services.Framework.AI;
using TM.Services.Modules.ProjectData;

namespace Tianming.Desktop.Avalonia;

public static class AppHost
{
    public static IServiceProvider Build()
    {
        var s = new ServiceCollection();
        s.AddLogging(b =>
        {
            b.AddConsole();
            b.SetMinimumLevel(LogLevel.Information);
        });
        s.AddProjectDataServices();
        s.AddAIServices();
        s.AddFrameworkServices();
        s.AddAvaloniaShell();
        return s.BuildServiceProvider();
    }
}
```

- [ ] **Step 5.6：写 AppHost 冒烟测试**

创建 `tests/Tianming.Desktop.Avalonia.Tests/DI/AppHostTests.cs`：

```csharp
using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.ViewModels;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.DI;

public class AppHostTests
{
    [Fact]
    public void Build_ResolvesNavigationService()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var nav = sp.GetRequiredService<INavigationService>();
        Assert.NotNull(nav);
    }

    [Fact]
    public void Build_ResolvesMainWindowViewModel()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var vm = sp.GetRequiredService<MainWindowViewModel>();
        Assert.NotNull(vm);
    }

    [Fact]
    public void Build_RegistersAllM3Pages()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var reg = sp.GetRequiredService<PageRegistry>();
        Assert.Contains(PageKeys.Welcome,   reg.Keys);
        Assert.Contains(PageKeys.Dashboard, reg.Keys);
        Assert.Contains(PageKeys.Settings,  reg.Keys);
    }
}
```

> 此测试需要 Task 6/7/8 的 VM 都写完后才会全过。先写出来，让它失败，等 Task 6-8 完成后一起验证。

- [ ] **Step 5.7：commit**（实现 VM 前先提交 DI 骨架）

```bash
git add src/Tianming.ProjectData/ServiceCollectionExtensions.cs \
        src/Tianming.ProjectData/Tianming.ProjectData.csproj \
        src/Tianming.AI/ServiceCollectionExtensions.cs \
        src/Tianming.AI/Tianming.AI.csproj \
        src/Tianming.Framework/ServiceCollectionExtensions.cs \
        src/Tianming.Framework/Tianming.Framework.csproj \
        src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs \
        src/Tianming.Desktop.Avalonia/AppHost.cs \
        tests/Tianming.Desktop.Avalonia.Tests/DI/AppHostTests.cs
git commit -m "feat(desktop): DI 扩展方法 + AppHost 装配"
```

---

## Task 6：DispatcherScheduler + AppLifecycle（Infrastructure 基础）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/DispatcherScheduler.cs`
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/AppLifecycle.cs`

- [ ] **Step 6.1：DispatcherScheduler**

创建 `src/Tianming.Desktop.Avalonia/Infrastructure/DispatcherScheduler.cs`：

```csharp
using System;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class DispatcherScheduler
{
    public bool IsUIThread => Dispatcher.UIThread.CheckAccess();

    public void Post(Action action) => Dispatcher.UIThread.Post(action);

    public Task InvokeAsync(Action action) => Dispatcher.UIThread.InvokeAsync(action).GetTask();

    public Task<T> InvokeAsync<T>(Func<T> func) => Dispatcher.UIThread.InvokeAsync(func).GetTask();
}
```

- [ ] **Step 6.2：AppLifecycle**

创建 `src/Tianming.Desktop.Avalonia/Infrastructure/AppLifecycle.cs`：

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Theme;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class AppLifecycle
{
    private readonly AppPaths _paths;
    private readonly WindowStateStore _windowStore;
    private readonly ThemeBridge _theme;
    private readonly INavigationService _nav;
    private readonly ILogger<AppLifecycle> _log;

    public AppLifecycle(
        AppPaths paths,
        WindowStateStore windowStore,
        ThemeBridge theme,
        INavigationService nav,
        ILogger<AppLifecycle> log)
    {
        _paths = paths;
        _windowStore = windowStore;
        _theme = theme;
        _nav = nav;
        _log = log;
    }

    public async Task OnStartupAsync()
    {
        _paths.EnsureDirectories();
        _log.LogInformation("AppSupport={Path}", _paths.AppSupportDirectory);
        await _theme.InitializeAsync();
        await _nav.NavigateAsync(PageKeys.Welcome);
    }

    public WindowState LoadInitialWindowState() => _windowStore.Load();

    public void SaveWindowState(WindowState state) => _windowStore.Save(state);

    public Task OnShutdownAsync()
    {
        _log.LogInformation("Shutting down");
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 6.3：build**

```bash
dotnet build src/Tianming.Desktop.Avalonia/ --nologo -v q 2>&1 | tail -4
```

Expected: 会报 `ThemeBridge` 不存在（Task 7 会建）。暂允许此错误存在——下一步就建。

- [ ] **Step 6.4：commit**（一个 Task 内的多文件一起提交）

不 commit，接 Task 7 再合并 commit。

---

## Task 7：ThemeBridge + Palette 资源

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Theme/ThemeBridge.cs`
- Create: `src/Tianming.Desktop.Avalonia/Theme/PalettesLight.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Theme/PalettesDark.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Theme/CommonStyles.axaml`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Theme/ThemeBridgeTests.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

`ThemeBridge` 职责：
- 持有 `Application.Current.Resources` 的引用
- 对外暴露 `ApplyAsync(PortableThemeApplicationRequest)` —— 供 `PortableThemeStateController` 回调
- 把 `Brushes` 字典里每个 `(key, hex)` 映射成 `SolidColorBrush` 替换到资源字典
- 暴露 `InitializeAsync()`——冷启动第一次 apply 默认 palette

- [ ] **Step 7.1：PalettesLight.axaml / PalettesDark.axaml**

创建 `src/Tianming.Desktop.Avalonia/Theme/PalettesLight.axaml`：

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <SolidColorBrush x:Key="UnifiedBackground"  Color="#FFFFFF"/>
  <SolidColorBrush x:Key="ContentBackground"  Color="#F6F7F9"/>
  <SolidColorBrush x:Key="Surface"            Color="#FFFFFF"/>
  <SolidColorBrush x:Key="ContentHighlight"   Color="#EEF2FF"/>
  <SolidColorBrush x:Key="WindowBorder"       Color="#D0D7DE"/>
  <SolidColorBrush x:Key="BorderBrush"        Color="#E1E4E8"/>
  <SolidColorBrush x:Key="TextPrimary"        Color="#0F172A"/>
  <SolidColorBrush x:Key="TextSecondary"      Color="#475569"/>
  <SolidColorBrush x:Key="TextTertiary"       Color="#94A3B8"/>
  <SolidColorBrush x:Key="TextDisabled"       Color="#CBD5E1"/>
  <SolidColorBrush x:Key="HoverBackground"    Color="#F1F5F9"/>
  <SolidColorBrush x:Key="ActiveBackground"   Color="#E2E8F0"/>
  <SolidColorBrush x:Key="SelectedBackground" Color="#DBEAFE"/>
  <SolidColorBrush x:Key="PrimaryColor"       Color="#2563EB"/>
  <SolidColorBrush x:Key="PrimaryHover"       Color="#1D4ED8"/>
  <SolidColorBrush x:Key="PrimaryActive"      Color="#1E40AF"/>
  <SolidColorBrush x:Key="SuccessColor"       Color="#16A34A"/>
  <SolidColorBrush x:Key="WarningColor"       Color="#D97706"/>
  <SolidColorBrush x:Key="DangerColor"        Color="#DC2626"/>
  <SolidColorBrush x:Key="DangerHover"        Color="#B91C1C"/>
  <SolidColorBrush x:Key="InfoColor"          Color="#0891B2"/>
</ResourceDictionary>
```

创建 `src/Tianming.Desktop.Avalonia/Theme/PalettesDark.axaml`（同样 21 个 key，颜色互补）：

```xml
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <SolidColorBrush x:Key="UnifiedBackground"  Color="#0F172A"/>
  <SolidColorBrush x:Key="ContentBackground"  Color="#1E293B"/>
  <SolidColorBrush x:Key="Surface"            Color="#0F172A"/>
  <SolidColorBrush x:Key="ContentHighlight"   Color="#1E40AF"/>
  <SolidColorBrush x:Key="WindowBorder"       Color="#334155"/>
  <SolidColorBrush x:Key="BorderBrush"        Color="#475569"/>
  <SolidColorBrush x:Key="TextPrimary"        Color="#F8FAFC"/>
  <SolidColorBrush x:Key="TextSecondary"      Color="#CBD5E1"/>
  <SolidColorBrush x:Key="TextTertiary"       Color="#94A3B8"/>
  <SolidColorBrush x:Key="TextDisabled"       Color="#475569"/>
  <SolidColorBrush x:Key="HoverBackground"    Color="#1E293B"/>
  <SolidColorBrush x:Key="ActiveBackground"   Color="#334155"/>
  <SolidColorBrush x:Key="SelectedBackground" Color="#1E40AF"/>
  <SolidColorBrush x:Key="PrimaryColor"       Color="#3B82F6"/>
  <SolidColorBrush x:Key="PrimaryHover"       Color="#60A5FA"/>
  <SolidColorBrush x:Key="PrimaryActive"      Color="#93C5FD"/>
  <SolidColorBrush x:Key="SuccessColor"       Color="#22C55E"/>
  <SolidColorBrush x:Key="WarningColor"       Color="#F59E0B"/>
  <SolidColorBrush x:Key="DangerColor"        Color="#EF4444"/>
  <SolidColorBrush x:Key="DangerHover"        Color="#F87171"/>
  <SolidColorBrush x:Key="InfoColor"          Color="#06B6D4"/>
</ResourceDictionary>
```

- [ ] **Step 7.2：CommonStyles.axaml**

创建 `src/Tianming.Desktop.Avalonia/Theme/CommonStyles.axaml`（M3 用的最少样式）：

```xml
<Styles xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Style Selector="Window">
    <Setter Property="Background" Value="{DynamicResource UnifiedBackground}"/>
    <Setter Property="Foreground" Value="{DynamicResource TextPrimary}"/>
  </Style>
  <Style Selector="TextBlock">
    <Setter Property="Foreground" Value="{DynamicResource TextPrimary}"/>
  </Style>
  <Style Selector="Button">
    <Setter Property="Background" Value="{DynamicResource PrimaryColor}"/>
    <Setter Property="Foreground" Value="White"/>
    <Setter Property="Padding" Value="12,6"/>
  </Style>
  <Style Selector="Button:pointerover">
    <Setter Property="Background" Value="{DynamicResource PrimaryHover}"/>
  </Style>
</Styles>
```

- [ ] **Step 7.3：修改 App.axaml 加载 palette + styles**

编辑 `src/Tianming.Desktop.Avalonia/App.axaml`，替换为：

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             x:Class="Tianming.Desktop.Avalonia.App"
             RequestedThemeVariant="Default">
  <Application.Resources>
    <ResourceDictionary>
      <ResourceDictionary.MergedDictionaries>
        <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Theme/PalettesLight.axaml"/>
      </ResourceDictionary.MergedDictionaries>
    </ResourceDictionary>
  </Application.Resources>
  <Application.Styles>
    <FluentTheme />
    <StyleInclude Source="avares://Tianming.Desktop.Avalonia/Theme/CommonStyles.axaml"/>
  </Application.Styles>
</Application>
```

- [ ] **Step 7.4：ThemeBridge**

创建 `src/Tianming.Desktop.Avalonia/Theme/ThemeBridge.cs`：

```csharp
using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using TM.Framework.Appearance;

namespace Tianming.Desktop.Avalonia.Theme;

public sealed class ThemeBridge
{
    private readonly ILogger<ThemeBridge> _log;
    private PortableThemeType _currentVariant = PortableThemeType.Light;

    public ThemeBridge(ILogger<ThemeBridge> log) { _log = log; }

    public Task InitializeAsync()
    {
        ApplyLightDarkVariant(PortableThemeType.Light);
        return Task.CompletedTask;
    }

    /// <summary>Callback wired to PortableThemeStateController.</summary>
    public Task ApplyAsync(PortableThemeApplicationRequest request)
    {
        if (Dispatcher.UIThread.CheckAccess())
            ApplyCore(request);
        else
            Dispatcher.UIThread.Post(() => ApplyCore(request));
        return Task.CompletedTask;
    }

    private void ApplyCore(PortableThemeApplicationRequest request)
    {
        var app = Application.Current;
        if (app is null) return;

        foreach (var kv in request.Brushes)
        {
            if (!TryParseHex(kv.Value, out var color)) continue;
            app.Resources[kv.Key] = new SolidColorBrush(color);
        }

        // 按 palette 包含的 brush 明暗自动切换 Avalonia ThemeVariant
        var variant = request.Plan.ColorMode == PortableThemeColorMode.Dark
            ? ThemeVariant.Dark
            : ThemeVariant.Light;
        app.RequestedThemeVariant = variant;
        _currentVariant = request.Plan.ThemeType;
        _log.LogInformation("Applied theme {Theme}", request.Plan.ThemeType);
    }

    private void ApplyLightDarkVariant(PortableThemeType type)
    {
        var app = Application.Current;
        if (app is null) return;
        app.RequestedThemeVariant = type == PortableThemeType.Dark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    private static bool TryParseHex(string s, out Color color)
    {
        if (Color.TryParse(s, out color)) return true;
        color = default;
        return false;
    }
}
```

- [ ] **Step 7.5：ThemeBridge 测试（不依赖 Avalonia UI 线程的纯逻辑部分）**

M3 的 ThemeBridge 对 `Application.Current` 访问需要 Avalonia 运行，单测跳过真实 apply，只测 `TryParseHex` 等纯方法。简化：Task 7 暂不写 ThemeBridge 单测；把验证放 AppHost 冒烟与手工 run。

- [ ] **Step 7.6：build**

```bash
dotnet build src/Tianming.Desktop.Avalonia/ --nologo -v q 2>&1 | tail -4
```

Expected: 0 error。`PortableThemeApplicationPlan` 的实际字段已在 `src/Tianming.Framework/Appearance/PortableThemeApplicationPlanner.cs` 定义为 `ThemeType` / `ColorMode`（加其他），上面代码直接用这两个字段。若 Avalonia 对 `ThemeVariant` 的 API 不是 `ThemeVariant.Light` / `ThemeVariant.Dark`（Avalonia 11.x 是这样，但 11.2+ 可能变），按编译错误指示改。

- [ ] **Step 7.7：commit**

```bash
git add src/Tianming.Desktop.Avalonia/Theme/ \
        src/Tianming.Desktop.Avalonia/Infrastructure/ \
        src/Tianming.Desktop.Avalonia/App.axaml
git commit -m "feat(desktop): ThemeBridge + 主题 palette 资源 + Common 样式"
```

---

## Task 8：ViewModels（MainWindow / ThreeColumn / Welcome / Placeholder / Shell）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/MainWindowViewModel.cs`
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/ThreeColumnLayoutViewModel.cs`
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/WelcomeViewModel.cs`
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/PlaceholderViewModel.cs`
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Shell/LeftNavViewModel.cs`
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Shell/RightConversationViewModel.cs`

- [ ] **Step 8.1：MainWindowViewModel**

创建 `src/Tianming.Desktop.Avalonia/ViewModels/MainWindowViewModel.cs`：

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tianming.Desktop.Avalonia.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "天命";

    [ObservableProperty]
    private ThreeColumnLayoutViewModel _layout;

    public MainWindowViewModel(ThreeColumnLayoutViewModel layout) { _layout = layout; }
}
```

- [ ] **Step 8.2：ThreeColumnLayoutViewModel**

创建 `src/Tianming.Desktop.Avalonia/ViewModels/ThreeColumnLayoutViewModel.cs`：

```csharp
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.ViewModels.Shell;

namespace Tianming.Desktop.Avalonia.ViewModels;

public partial class ThreeColumnLayoutViewModel : ObservableObject
{
    private readonly WindowStateStore _windowStore;
    private readonly INavigationService _nav;
    private readonly IServiceProvider _sp;

    [ObservableProperty] private LeftNavViewModel _leftNav;
    [ObservableProperty] private RightConversationViewModel _rightPanel;
    [ObservableProperty] private object? _center;
    [ObservableProperty] private double _leftColumnWidth;
    [ObservableProperty] private double _rightColumnWidth;

    public ThreeColumnLayoutViewModel(
        WindowStateStore windowStore,
        INavigationService nav,
        IServiceProvider sp,
        LeftNavViewModel left,
        RightConversationViewModel right)
    {
        _windowStore = windowStore;
        _nav = nav;
        _sp = sp;
        _leftNav = left;
        _rightPanel = right;

        var state = _windowStore.Load();
        _leftColumnWidth = state.LeftColumnWidth;
        _rightColumnWidth = state.RightColumnWidth;

        _nav.CurrentKeyChanged += OnNavigated;
    }

    private void OnNavigated(object? sender, PageKey key)
    {
        Center = _nav.CurrentViewModel;
    }
}
```

- [ ] **Step 8.3：LeftNavViewModel**

创建 `src/Tianming.Desktop.Avalonia/ViewModels/Shell/LeftNavViewModel.cs`：

```csharp
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Navigation;
using TM.Services.Modules.ProjectData.Navigation;

namespace Tianming.Desktop.Avalonia.ViewModels.Shell;

public partial class LeftNavViewModel : ObservableObject
{
    private readonly INavigationService _nav;
    public ObservableCollection<NavEntry> Entries { get; } = new();

    public LeftNavViewModel(INavigationService nav)
    {
        _nav = nav;
        Entries.Add(new NavEntry("欢迎",     PageKeys.Welcome,   "\uE80F"));
        Entries.Add(new NavEntry("主页",     PageKeys.Dashboard, "\uE80F"));
        Entries.Add(new NavEntry("设置",     PageKeys.Settings,  "\uE713"));
        // M4 扩展 WritingNavigationCatalog 里的一级分类
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task NavigateAsync(NavEntry entry)
        => await _nav.NavigateAsync(entry.Key);
}

public sealed record NavEntry(string Label, PageKey Key, string Icon);
```

- [ ] **Step 8.4：RightConversationViewModel + WelcomeViewModel + PlaceholderViewModel**

创建 `src/Tianming.Desktop.Avalonia/ViewModels/Shell/RightConversationViewModel.cs`：

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tianming.Desktop.Avalonia.ViewModels.Shell;

public partial class RightConversationViewModel : ObservableObject
{
    [ObservableProperty]
    private string _placeholderText = "对话面板（M4.5 实装）";
}
```

创建 `src/Tianming.Desktop.Avalonia/ViewModels/WelcomeViewModel.cs`：

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;

namespace Tianming.Desktop.Avalonia.ViewModels;

public partial class WelcomeViewModel : ObservableObject
{
    private readonly AppPaths _paths;
    private readonly INavigationService _nav;

    [ObservableProperty] private string _newProjectName = "未命名项目";

    public WelcomeViewModel(AppPaths paths, INavigationService nav)
    {
        _paths = paths;
        _nav = nav;
    }

    [RelayCommand]
    private async Task CreateProjectAsync()
    {
        var safe = string.IsNullOrWhiteSpace(NewProjectName) ? "未命名项目" : NewProjectName.Trim();
        var dir = Path.Combine(_paths.AppSupportDirectory, "Projects", $"{safe}-{DateTime.Now:yyyyMMdd-HHmmss}");
        Directory.CreateDirectory(dir);
        // 最简占位 meta；M4 接入真正的项目管理器
        File.WriteAllText(Path.Combine(dir, "project.json"),
            $"{{\"name\":\"{safe}\",\"createdAt\":\"{DateTime.UtcNow:o}\"}}");
        await _nav.NavigateAsync(PageKeys.Dashboard);
    }
}
```

创建 `src/Tianming.Desktop.Avalonia/ViewModels/PlaceholderViewModel.cs`：

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tianming.Desktop.Avalonia.ViewModels;

public partial class PlaceholderViewModel : ObservableObject
{
    [ObservableProperty]
    private string _message = "此页面将在 M4 实装。";
}
```

- [ ] **Step 8.5：build**

```bash
dotnet build src/Tianming.Desktop.Avalonia/ --nologo -v q 2>&1 | tail -4
```

Expected: 0 error。

- [ ] **Step 8.6：commit**

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/
git commit -m "feat(desktop): ViewModels — MainWindow/ThreeColumn/Welcome/Placeholder/Shell"
```

---

## Task 9：Views（MainWindow 重写 / ThreeColumn / Welcome / Placeholder / Shell）

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/Views/MainWindow.axaml`
- Modify: `src/Tianming.Desktop.Avalonia/Views/MainWindow.axaml.cs`
- Create: `src/Tianming.Desktop.Avalonia/Views/ThreeColumnLayoutView.axaml`、`.axaml.cs`
- Create: `src/Tianming.Desktop.Avalonia/Views/WelcomeView.axaml`、`.axaml.cs`
- Create: `src/Tianming.Desktop.Avalonia/Views/PlaceholderView.axaml`、`.axaml.cs`
- Create: `src/Tianming.Desktop.Avalonia/Views/Shell/LeftNavView.axaml`、`.axaml.cs`
- Create: `src/Tianming.Desktop.Avalonia/Views/Shell/RightConversationView.axaml`、`.axaml.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml.cs`（走 DI 取 MainWindow + 注入 VM）

- [ ] **Step 9.1：重写 MainWindow.axaml 使用 ThreeColumnLayoutView**

编辑 `src/Tianming.Desktop.Avalonia/Views/MainWindow.axaml`：

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Tianming.Desktop.Avalonia.ViewModels"
        xmlns:v="using:Tianming.Desktop.Avalonia.Views"
        x:Class="Tianming.Desktop.Avalonia.Views.MainWindow"
        x:DataType="vm:MainWindowViewModel"
        Title="{Binding Title}" Width="1200" Height="800">
  <v:ThreeColumnLayoutView DataContext="{Binding Layout}"/>
</Window>
```

- [ ] **Step 9.2：ThreeColumnLayoutView**

创建 `src/Tianming.Desktop.Avalonia/Views/ThreeColumnLayoutView.axaml`：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Tianming.Desktop.Avalonia.ViewModels"
             x:Class="Tianming.Desktop.Avalonia.Views.ThreeColumnLayoutView"
             x:DataType="vm:ThreeColumnLayoutViewModel">
  <Grid>
    <Grid.ColumnDefinitions>
      <ColumnDefinition Width="{Binding LeftColumnWidth, Mode=TwoWay}" MinWidth="180"/>
      <ColumnDefinition Width="4"/>
      <ColumnDefinition Width="*"/>
      <ColumnDefinition Width="4"/>
      <ColumnDefinition Width="{Binding RightColumnWidth, Mode=TwoWay}" MinWidth="240"/>
    </Grid.ColumnDefinitions>
    <ContentControl Grid.Column="0" Content="{Binding LeftNav}"/>
    <GridSplitter Grid.Column="1" Background="{DynamicResource BorderBrush}" ResizeDirection="Columns"/>
    <ContentControl Grid.Column="2" Content="{Binding Center}"/>
    <GridSplitter Grid.Column="3" Background="{DynamicResource BorderBrush}" ResizeDirection="Columns"/>
    <ContentControl Grid.Column="4" Content="{Binding RightPanel}"/>
  </Grid>
</UserControl>
```

创建 `src/Tianming.Desktop.Avalonia/Views/ThreeColumnLayoutView.axaml.cs`：

```csharp
using Avalonia.Controls;

namespace Tianming.Desktop.Avalonia.Views;

public partial class ThreeColumnLayoutView : UserControl
{
    public ThreeColumnLayoutView() => InitializeComponent();
}
```

- [ ] **Step 9.3：LeftNavView**

创建 `src/Tianming.Desktop.Avalonia/Views/Shell/LeftNavView.axaml`：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Tianming.Desktop.Avalonia.ViewModels.Shell"
             x:Class="Tianming.Desktop.Avalonia.Views.Shell.LeftNavView"
             x:DataType="vm:LeftNavViewModel">
  <Border Background="{DynamicResource ContentBackground}" Padding="8">
    <StackPanel>
      <TextBlock Text="导航" FontWeight="Bold" Margin="0,0,0,12" Foreground="{DynamicResource TextSecondary}"/>
      <ItemsControl ItemsSource="{Binding Entries}">
        <ItemsControl.ItemTemplate>
          <DataTemplate>
            <Button Command="{Binding $parent[ItemsControl].((vm:LeftNavViewModel)DataContext).NavigateCommand}"
                    CommandParameter="{Binding}"
                    HorizontalAlignment="Stretch" HorizontalContentAlignment="Left"
                    Margin="0,2" Background="Transparent" Foreground="{DynamicResource TextPrimary}">
              <TextBlock Text="{Binding Label}"/>
            </Button>
          </DataTemplate>
        </ItemsControl.ItemTemplate>
      </ItemsControl>
    </StackPanel>
  </Border>
</UserControl>
```

创建 `src/Tianming.Desktop.Avalonia/Views/Shell/LeftNavView.axaml.cs`：

```csharp
using Avalonia.Controls;

namespace Tianming.Desktop.Avalonia.Views.Shell;

public partial class LeftNavView : UserControl
{
    public LeftNavView() => InitializeComponent();
}
```

- [ ] **Step 9.4：RightConversationView（占位）**

创建 `src/Tianming.Desktop.Avalonia/Views/Shell/RightConversationView.axaml`：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Tianming.Desktop.Avalonia.ViewModels.Shell"
             x:Class="Tianming.Desktop.Avalonia.Views.Shell.RightConversationView"
             x:DataType="vm:RightConversationViewModel">
  <Border Background="{DynamicResource ContentBackground}" Padding="16">
    <TextBlock Text="{Binding PlaceholderText}" Foreground="{DynamicResource TextTertiary}"
               HorizontalAlignment="Center" VerticalAlignment="Center"/>
  </Border>
</UserControl>
```

创建 `src/Tianming.Desktop.Avalonia/Views/Shell/RightConversationView.axaml.cs`：

```csharp
using Avalonia.Controls;

namespace Tianming.Desktop.Avalonia.Views.Shell;

public partial class RightConversationView : UserControl
{
    public RightConversationView() => InitializeComponent();
}
```

- [ ] **Step 9.5：WelcomeView + PlaceholderView**

创建 `src/Tianming.Desktop.Avalonia/Views/WelcomeView.axaml`：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Tianming.Desktop.Avalonia.ViewModels"
             x:Class="Tianming.Desktop.Avalonia.Views.WelcomeView"
             x:DataType="vm:WelcomeViewModel">
  <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center" Spacing="12">
    <TextBlock Text="欢迎使用天命" FontSize="28" FontWeight="Bold" HorizontalAlignment="Center"/>
    <TextBlock Text="新建一个项目开始写小说" Foreground="{DynamicResource TextSecondary}" HorizontalAlignment="Center"/>
    <TextBox Text="{Binding NewProjectName, Mode=TwoWay}" Width="300" Watermark="项目名"/>
    <Button Content="新建项目" Command="{Binding CreateProjectCommand}" HorizontalAlignment="Center"/>
  </StackPanel>
</UserControl>
```

创建 `src/Tianming.Desktop.Avalonia/Views/WelcomeView.axaml.cs`：

```csharp
using Avalonia.Controls;

namespace Tianming.Desktop.Avalonia.Views;

public partial class WelcomeView : UserControl
{
    public WelcomeView() => InitializeComponent();
}
```

创建 `src/Tianming.Desktop.Avalonia/Views/PlaceholderView.axaml`：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Tianming.Desktop.Avalonia.ViewModels"
             x:Class="Tianming.Desktop.Avalonia.Views.PlaceholderView"
             x:DataType="vm:PlaceholderViewModel">
  <TextBlock Text="{Binding Message}" Foreground="{DynamicResource TextTertiary}"
             HorizontalAlignment="Center" VerticalAlignment="Center" FontSize="18"/>
</UserControl>
```

创建 `src/Tianming.Desktop.Avalonia/Views/PlaceholderView.axaml.cs`：

```csharp
using Avalonia.Controls;

namespace Tianming.Desktop.Avalonia.Views;

public partial class PlaceholderView : UserControl
{
    public PlaceholderView() => InitializeComponent();
}
```

- [ ] **Step 9.6：修改 App.axaml.cs 从 DI 取 VM**

编辑 `src/Tianming.Desktop.Avalonia/App.axaml.cs`：

```csharp
using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.ViewModels;
using Tianming.Desktop.Avalonia.Views;

namespace Tianming.Desktop.Avalonia;

public partial class App : Application
{
    internal static IServiceProvider? Services { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Services = AppHost.Build();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var lifecycle = Services.GetRequiredService<AppLifecycle>();
            _ = lifecycle.OnStartupAsync();

            var vm = Services.GetRequiredService<MainWindowViewModel>();
            var window = new MainWindow { DataContext = vm };

            var savedState = lifecycle.LoadInitialWindowState();
            window.Width  = savedState.Width;
            window.Height = savedState.Height;
            if (!double.IsNaN(savedState.X) && !double.IsNaN(savedState.Y))
                window.Position = new PixelPoint((int)savedState.X, (int)savedState.Y);
            if (savedState.IsMaximized)
                window.WindowState = global::Avalonia.Controls.WindowState.Maximized;

            window.Closing += (_, _) =>
            {
                var layout = Services!.GetRequiredService<ThreeColumnLayoutViewModel>();
                var state = new WindowState(
                    X: window.Position.X,
                    Y: window.Position.Y,
                    Width: window.Width,
                    Height: window.Height,
                    LeftColumnWidth: layout.LeftColumnWidth,
                    RightColumnWidth: layout.RightColumnWidth,
                    IsMaximized: window.WindowState == global::Avalonia.Controls.WindowState.Maximized);
                lifecycle.SaveWindowState(state);
            };

            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
```

- [ ] **Step 9.7：build + run**

```bash
dotnet build src/Tianming.Desktop.Avalonia/ --nologo -v q 2>&1 | tail -4
```

Expected: 0 error 0 warning。

```bash
dotnet run --project src/Tianming.Desktop.Avalonia/ &
sleep 5
kill %1 2>/dev/null
```

Expected: 弹出三栏窗口；左栏看到 "欢迎/主页/设置" 三个条目；中央显示 WelcomeView（标题"欢迎使用天命" + 输入框 + "新建项目"按钮）；右栏显示"对话面板（M4.5 实装）"占位文本；标题栏"天命"。

- [ ] **Step 9.8：跑 AppHostTests 全过**

```bash
dotnet test tests/Tianming.Desktop.Avalonia.Tests/ --nologo -v q 2>&1 | tail -4
```

Expected: 所有新增测试（AppPaths 3 + WindowStateStore 3 + NavigationService 4 + AppHost 3 = 13）全过。

- [ ] **Step 9.9：commit**

```bash
git add src/Tianming.Desktop.Avalonia/Views/ \
        src/Tianming.Desktop.Avalonia/App.axaml.cs
git commit -m "feat(desktop): Views 三栏布局 + Welcome/Placeholder + DI 接线"
```

---

## Task 10：收尾

- [ ] **Step 10.1：全量测试**

```bash
dotnet test Tianming.MacMigration.sln --nologo -v q 2>&1 | tail -6
```

Expected: `1132（或 1143 若 M2 合入）+ 13 = 1145 或 1156` 全过。

- [ ] **Step 10.2：Windows 绑定扫描**

```bash
Scripts/check-windows-bindings.sh src/ tests/ 2>&1 | tail -5
```

Expected: 0 命中。

- [ ] **Step 10.3：手工验收（冷启动/重启状态恢复）**

1. `dotnet run --project src/Tianming.Desktop.Avalonia/`
2. 调整窗口位置与尺寸，拖动 GridSplitter 改左右栏宽度，点"新建项目"，关闭
3. 再次 `dotnet run`
4. 验证：窗口位置/尺寸/三栏比例与上次关闭一致；左栏能跳 Welcome/Dashboard/Settings；主题资源显示正常

- [ ] **Step 10.4：更新功能对齐矩阵**

编辑 `Docs/macOS迁移/功能对齐矩阵.md`，把"主窗口"、"三栏布局"、"主题管理"三行状态改为"框架已就位（M3）"。

```bash
git add Docs/macOS迁移/功能对齐矩阵.md
git commit -m "docs(alignment): M3 框架落地 — 主窗口/三栏/主题"
```

---

## 验收标准

1. `dotnet build Tianming.MacMigration.sln` → 0 Warning / 0 Error
2. `dotnet test Tianming.MacMigration.sln` → 全过（较 M2 基线 +13）
3. `Scripts/check-windows-bindings.sh src/ tests/` → 0 命中
4. `dotnet run --project src/Tianming.Desktop.Avalonia` 启动后三栏主窗口可视，左栏导航可点，Welcome 页能"新建项目"落盘到 `~/Library/Application Support/Tianming/Projects/`
5. 关闭重开后：窗口位置/尺寸/左右栏宽度恢复
6. 主题资源（`PrimaryColor` 等 21 key）在 Light 下显示正确颜色
7. AppHost 冒烟测试过（DI 能 resolve 所有 M3 VM 与 INavigationService）
8. 分支 `m3/avalonia-shell-2026-05-12`（实施时开）所有 M3 commit 干净

完成后进入 M4（核心模块页面迁移）。
