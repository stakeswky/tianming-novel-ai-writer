# Round 7 Lane A — Settings Shell + 主题区 + Lane 0 复核遗留收口

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 Avalonia 端建立 Settings Shell + 主题相关 3 个 settings page，让用户能在 macOS 上首次看到并控制"设置"页；顺手收口 Lane 0 复核遗留的 App.axaml 冷启动 Light 闪烁与 manual-test-howto lsregister 兜底文档。

**Architecture:** 沿用现有 PageRegistry + NavigationService + DataTemplate 模式。新建一个外层 `SettingsShellView`（左侧子导航 + 内容容器），与 3 个内部 settings page 用同款 PageRegistry 注册 + LeftNav 单一入口"设置"。Theme 相关 page 直接持有 `PortableThemeStateController` / `PortableSystemFollowController` / `PortableThemeScheduleService` 等 lib，让用户控制 Lane 0 已接通的平台监听链路。

**Tech Stack:** Avalonia 11.x, CommunityToolkit.Mvvm (ObservableObject + RelayCommand), .NET 8, xUnit, Moq。

**Worktree:** `/Users/jimmy/Downloads/tianming-novel-ai-writer/.claude/worktrees/m7-lane-a-appearance` (branch `worktree-m7-lane-a-appearance`)

---

## Scope

**本 plan 覆盖（矩阵 D #1 + #4 + #5）**：
- 主题管理（Light / Dark / Auto / Custom 切换）
- 主题跟随系统 + 定时主题 + 排除时段
- Lane 0 复核遗留：App.axaml 冷启动 Light 闪烁
- Lane 0 复核遗留：manual-test-howto.md lsregister 兜底命令

**本 plan 不覆盖（deferred 到后续 sub-plan）**：
- 矩阵 D #2 自动主题冲突解析（融在 #4 跟随系统的 controller 内部，无独立 UI）
- 矩阵 D #3 主题过渡动画（PortableThemeTransitionSettings 在 Appearance 子目录，UI 工作量大，单独成 plan）
- 矩阵 D #6 字体管理（PortableFontCatalog 等十几个 lib 类，单独成 plan）
- 矩阵 D #7-#9 图片取色 / AI 配色 / 配色历史（单独成 plan）

---

## File Structure

### 新增（13 个文件）

```
src/Tianming.Desktop.Avalonia/
  Navigation/PageKeys.cs                                  # 加 2 个子 key
  ViewModels/Settings/SettingsShellViewModel.cs           # 子导航容器 VM
  ViewModels/Settings/ThemeSettingsViewModel.cs           # 主题切换 VM
  ViewModels/Settings/ThemeFollowSystemViewModel.cs       # 跟随 + 定时 VM
  Views/Settings/SettingsShellView.axaml                  # 子导航容器 view
  Views/Settings/SettingsShellView.axaml.cs
  Views/Settings/ThemeSettingsPage.axaml
  Views/Settings/ThemeSettingsPage.axaml.cs
  Views/Settings/ThemeFollowSystemPage.axaml
  Views/Settings/ThemeFollowSystemPage.axaml.cs

tests/Tianming.Desktop.Avalonia.Tests/
  ViewModels/Settings/SettingsShellViewModelTests.cs
  ViewModels/Settings/ThemeSettingsViewModelTests.cs
  ViewModels/Settings/ThemeFollowSystemViewModelTests.cs
  Theme/ThemeBootBaselineTests.cs                         # Task 4 闪烁 baseline
```

### 修改（4 个文件）

```
src/Tianming.Desktop.Avalonia/
  Navigation/PageKeys.cs                                  # +2 readonly fields
  AvaloniaShellServiceCollectionExtensions.cs             # 替换 Settings PlaceholderView + 注册 2 个新 page + DI
  App.axaml                                               # L22 删除 RequestedThemeVariant="Light" + 3 个新 DataTemplate
Docs/macOS迁移/manual-test-howto.md                       # Fresh profile lsregister 兜底段
```

---

## Task 1: Settings Shell — 解掉 PlaceholderView 路由，建立子导航容器

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs`
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Settings/SettingsShellViewModel.cs`
- Create: `src/Tianming.Desktop.Avalonia/Views/Settings/SettingsShellView.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Views/Settings/SettingsShellView.axaml.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml` (add DataTemplate)
- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs:511` (replace PlaceholderViewModel registration)
- Test: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Settings/SettingsShellViewModelTests.cs`

- [ ] **Step 1.1: 加 2 个子 PageKey**

修改 `Navigation/PageKeys.cs`，在 `Packaging` 行之后加：

```csharp
    // M7 Lane A 设置
    public static readonly PageKey SettingsTheme       = new("settings.theme");
    public static readonly PageKey SettingsFollowSystem = new("settings.followsystem");
```

- [ ] **Step 1.2: 写 SettingsShellViewModelTests（红）**

创建 `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Settings/SettingsShellViewModelTests.cs`：

```csharp
using System.Linq;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.ViewModels.Settings;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Settings;

public class SettingsShellViewModelTests
{
    [Fact]
    public void Ctor_exposes_theme_and_followsystem_subnav_entries()
    {
        var vm = new SettingsShellViewModel();

        Assert.NotEmpty(vm.SubNavItems);
        Assert.Contains(vm.SubNavItems, i => i.Key == PageKeys.SettingsTheme);
        Assert.Contains(vm.SubNavItems, i => i.Key == PageKeys.SettingsFollowSystem);
    }

    [Fact]
    public void Selecting_subnav_item_updates_SelectedItem()
    {
        var vm = new SettingsShellViewModel();
        var followItem = vm.SubNavItems.First(i => i.Key == PageKeys.SettingsFollowSystem);

        vm.SelectedItem = followItem;

        Assert.Equal(PageKeys.SettingsFollowSystem, vm.SelectedItem!.Key);
    }
}
```

- [ ] **Step 1.3: 跑测试确认红**

```bash
dotnet test --filter "FullyQualifiedName~SettingsShellViewModelTests" --nologo -v minimal
```

预期：编译失败 — `SettingsShellViewModel` 不存在

- [ ] **Step 1.4: 写 SettingsShellViewModel 实现**

创建 `src/Tianming.Desktop.Avalonia/ViewModels/Settings/SettingsShellViewModel.cs`：

```csharp
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Tianming.Desktop.Avalonia.Navigation;

namespace Tianming.Desktop.Avalonia.ViewModels.Settings;

public sealed record SettingsSubNavItem(PageKey Key, string Label, string Icon);

public partial class SettingsShellViewModel : ObservableObject
{
    public IReadOnlyList<SettingsSubNavItem> SubNavItems { get; }

    [ObservableProperty] private SettingsSubNavItem? _selectedItem;

    public SettingsShellViewModel()
    {
        SubNavItems = new List<SettingsSubNavItem>
        {
            new(PageKeys.SettingsTheme,        "外观主题", "🎨"),
            new(PageKeys.SettingsFollowSystem, "跟随系统", "🌓"),
        };
        SelectedItem = SubNavItems[0];
    }
}
```

- [ ] **Step 1.5: 跑测试确认绿**

```bash
dotnet test --filter "FullyQualifiedName~SettingsShellViewModelTests" --nologo -v minimal
```

预期：2/2 passed

- [ ] **Step 1.6: 创建 SettingsShellView axaml**

创建 `src/Tianming.Desktop.Avalonia/Views/Settings/SettingsShellView.axaml`：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:vm="using:Tianming.Desktop.Avalonia.ViewModels.Settings"
             x:Class="Tianming.Desktop.Avalonia.Views.Settings.SettingsShellView"
             x:DataType="vm:SettingsShellViewModel">
  <Grid ColumnDefinitions="220,*">
    <!-- 子导航 -->
    <Border Grid.Column="0"
            Background="{DynamicResource SurfaceContainerBrush}"
            BorderBrush="{DynamicResource BorderSubtleBrush}"
            BorderThickness="0,0,1,0"
            Padding="12">
      <StackPanel Spacing="4">
        <TextBlock Text="设置" FontSize="20" FontWeight="SemiBold" Margin="6,4,0,12"/>
        <ListBox ItemsSource="{Binding SubNavItems}"
                 SelectedItem="{Binding SelectedItem}"
                 Background="Transparent"
                 BorderThickness="0">
          <ListBox.ItemTemplate>
            <DataTemplate DataType="vm:SettingsSubNavItem">
              <StackPanel Orientation="Horizontal" Spacing="10" Margin="2">
                <TextBlock Text="{Binding Icon}" FontSize="16"/>
                <TextBlock Text="{Binding Label}" VerticalAlignment="Center"/>
              </StackPanel>
            </DataTemplate>
          </ListBox.ItemTemplate>
        </ListBox>
      </StackPanel>
    </Border>
    <!-- 内容（暂占位，Task 2/3 完成后嵌真实 page） -->
    <ContentControl Grid.Column="1" Padding="24">
      <TextBlock Text="选择左侧子项以查看设置" 
                 HorizontalAlignment="Center" 
                 VerticalAlignment="Center"
                 Foreground="{DynamicResource TextSecondaryBrush}"/>
    </ContentControl>
  </Grid>
</UserControl>
```

创建对应 `SettingsShellView.axaml.cs`（标准 InitializeComponent，参考 `Views/AI/ApiKeysPage.axaml.cs`）：

```csharp
using Avalonia.Controls;

namespace Tianming.Desktop.Avalonia.Views.Settings;

public partial class SettingsShellView : UserControl
{
    public SettingsShellView() => InitializeComponent();
}
```

- [ ] **Step 1.7: 注册 DataTemplate + DI**

修改 `src/Tianming.Desktop.Avalonia/App.axaml`，在 PackagingPage 的 DataTemplate 之后加：

```xml
    <!-- M7 Lane A 设置 -->
    <DataTemplate DataType="vmset:SettingsShellViewModel">
      <vset:SettingsShellView/>
    </DataTemplate>
```

同时在顶部 xmlns 列表加（与现有 xmlns 排版风格一致）：

```xml
xmlns:vmset="using:Tianming.Desktop.Avalonia.ViewModels.Settings"
xmlns:vset="using:Tianming.Desktop.Avalonia.Views.Settings"
```

修改 `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs:511`，把：

```csharp
reg.Register<PlaceholderViewModel, PlaceholderView>(PageKeys.Settings, "设置");
```

改为：

```csharp
reg.Register<SettingsShellViewModel, SettingsShellView>(PageKeys.Settings, "设置");
```

并在文件顶部 `using` 列表加：

```csharp
using Tianming.Desktop.Avalonia.ViewModels.Settings;
using Tianming.Desktop.Avalonia.Views.Settings;
```

在合适的 DI 注册位置（参考 ModelManagementViewModel 注册行 ~570 行附近）加：

```csharp
s.AddTransient<SettingsShellViewModel>();
```

- [ ] **Step 1.8: build + 全量 test**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v minimal
dotnet test Tianming.MacMigration.sln --nologo -v minimal 2>&1 | tail -8
```

预期：0 W / 0 E；1628 → **1630** passed（新增 2 条 SettingsShell 测试）

- [ ] **Step 1.9: commit**

```bash
git add src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs \
        src/Tianming.Desktop.Avalonia/ViewModels/Settings/SettingsShellViewModel.cs \
        src/Tianming.Desktop.Avalonia/Views/Settings/SettingsShellView.axaml \
        src/Tianming.Desktop.Avalonia/Views/Settings/SettingsShellView.axaml.cs \
        src/Tianming.Desktop.Avalonia/App.axaml \
        src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs \
        tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Settings/SettingsShellViewModelTests.cs

git commit -m "$(cat <<'EOF'
Replace Settings placeholder with a real shell that hosts theme subpages

Constraint: 沿用 PageRegistry + DataTemplate 模式；不引入新导航服务
Rejected: 把 settings 子导航做成 NavigationService 二级路由（增 service 边界，不必要）
Confidence: high
Scope-risk: narrow
Tested: dotnet build, dotnet test SettingsShellViewModelTests (2/2)
Not-tested: Computer Use 真机点击（留到 Task 3 完成后整体走一次）
EOF
)"
```

---

## Task 2: ThemeSettingsPage — Light / Dark / Auto / Custom 切换 UI

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Settings/ThemeSettingsViewModel.cs`
- Create: `src/Tianming.Desktop.Avalonia/Views/Settings/ThemeSettingsPage.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Views/Settings/ThemeSettingsPage.axaml.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml` (add DataTemplate)
- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs` (register VM + page in PageRegistry)
- Test: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Settings/ThemeSettingsViewModelTests.cs`

**Key lib API**（实施时先 read 确认）：
- `PortableThemeStateController.CurrentTheme` → `PortableThemeType` (enum: Light/Dark/Auto/Custom)
- `PortableThemeStateController.SwitchThemeAsync(PortableThemeType)` → `Task<PortableThemeApplyResult>`
- `PortableThemeStateController.ThemeChanged` event
- `ThemeBridge.ApplyAsync(PortableThemeApplicationRequest)` — Lane 0 已让它真切 Avalonia ThemeVariant

- [ ] **Step 2.1: 写 ThemeSettingsViewModelTests（红）**

创建 `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Settings/ThemeSettingsViewModelTests.cs`：

```csharp
using System.Threading.Tasks;
using Moq;
using TM.Services.Framework.Appearance;
using Tianming.Desktop.Avalonia.ViewModels.Settings;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Settings;

public class ThemeSettingsViewModelTests
{
    [Fact]
    public void Ctor_loads_current_theme_from_controller()
    {
        var controller = BuildController(PortableThemeType.Dark);
        var vm = new ThemeSettingsViewModel(controller);

        Assert.Equal(PortableThemeType.Dark, vm.SelectedTheme);
    }

    [Fact]
    public async Task SwitchTheme_invokes_controller_with_new_value()
    {
        var controller = BuildController(PortableThemeType.Light);
        var vm = new ThemeSettingsViewModel(controller);

        vm.SelectedTheme = PortableThemeType.Dark;
        await vm.ApplyThemeCommand.ExecuteAsync(null);

        Assert.Equal(PortableThemeType.Dark, controller.CurrentTheme);
    }

    private static PortableThemeStateController BuildController(PortableThemeType initial)
    {
        // 实施时根据真实 ctor 签名构造（可能需要 mock IThemeStateStore + IThemePalette 等）
        // 参考 src/Tianming.Framework.Tests/Appearance/PortableThemeStateControllerTests.cs
        throw new System.NotImplementedException("Read existing ctor in plan execution");
    }
}
```

**实施提示**：subagent 在 Step 2.1 实施时必须先 read `PortableThemeStateController.cs:114-180` 看 ctor 签名 + 已有测试 fixture，按真实 API 写 `BuildController` helper（不要照搬 plan placeholder）。

- [ ] **Step 2.2: 跑测试确认红**

```bash
dotnet test --filter "FullyQualifiedName~ThemeSettingsViewModelTests" --nologo -v minimal
```

预期：编译失败 — `ThemeSettingsViewModel` 不存在

- [ ] **Step 2.3: 写 ThemeSettingsViewModel 实现**

创建 `src/Tianming.Desktop.Avalonia/ViewModels/Settings/ThemeSettingsViewModel.cs`：

```csharp
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TM.Services.Framework.Appearance;

namespace Tianming.Desktop.Avalonia.ViewModels.Settings;

public partial class ThemeSettingsViewModel : ObservableObject
{
    private readonly PortableThemeStateController _controller;

    [ObservableProperty] private PortableThemeType _selectedTheme;

    public ThemeSettingsViewModel(PortableThemeStateController controller)
    {
        _controller = controller;
        _selectedTheme = controller.CurrentTheme;
        controller.ThemeChanged += (_, args) => SelectedTheme = args.CurrentTheme;
    }

    [RelayCommand]
    private async Task ApplyThemeAsync()
    {
        if (_selectedTheme == _controller.CurrentTheme)
            return;
        await _controller.SwitchThemeAsync(_selectedTheme);
    }
}
```

**实施提示**：`PortableThemeChangedEventArgs` 的字段名以真实 record 定义为准（看 `PortableThemeStateController.cs:26`）。如果是 `args.NewTheme` 而非 `args.CurrentTheme`，按真实写。

- [ ] **Step 2.4: 跑测试确认绿**

```bash
dotnet test --filter "FullyQualifiedName~ThemeSettingsViewModelTests" --nologo -v minimal
```

预期：2/2 passed

- [ ] **Step 2.5: 写 ThemeSettingsPage axaml**

创建 `src/Tianming.Desktop.Avalonia/Views/Settings/ThemeSettingsPage.axaml`：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:Tianming.Desktop.Avalonia.Controls"
             xmlns:vm="using:Tianming.Desktop.Avalonia.ViewModels.Settings"
             xmlns:app="using:TM.Services.Framework.Appearance"
             x:Class="Tianming.Desktop.Avalonia.Views.Settings.ThemeSettingsPage"
             x:DataType="vm:ThemeSettingsViewModel">
  <ScrollViewer Padding="24">
    <StackPanel Spacing="20" MaxWidth="640" HorizontalAlignment="Left">
      <controls:SectionCard Header="主题模式" Subtitle="选择应用使用的主题">
        <StackPanel Spacing="8">
          <RadioButton GroupName="ThemeMode" Content="跟随系统（Auto）"
                       IsChecked="{Binding SelectedTheme, Converter={x:Static vm:ThemeModeConverters.IsAuto}}"
                       Command="{Binding SetAutoCommand}"/>
          <RadioButton GroupName="ThemeMode" Content="浅色 Light"
                       IsChecked="{Binding SelectedTheme, Converter={x:Static vm:ThemeModeConverters.IsLight}}"
                       Command="{Binding SetLightCommand}"/>
          <RadioButton GroupName="ThemeMode" Content="深色 Dark"
                       IsChecked="{Binding SelectedTheme, Converter={x:Static vm:ThemeModeConverters.IsDark}}"
                       Command="{Binding SetDarkCommand}"/>
          <Button Content="应用" Command="{Binding ApplyThemeCommand}"
                  HorizontalAlignment="Right" Classes="accent" Margin="0,12,0,0"/>
        </StackPanel>
      </controls:SectionCard>
    </StackPanel>
  </ScrollViewer>
</UserControl>
```

**实施提示**：上面用了 `ThemeModeConverters` (静态 converter) + `SetAuto/Light/DarkCommand` 模式，是为了让 RadioButton IsChecked 双向绑定 enum 值。实施时**简化为**直接把 `SelectedTheme` 用 ComboBox 选 + 一个 "Apply" 按钮，避免 converter 复杂度：

```xml
<ComboBox SelectedItem="{Binding SelectedTheme}">
  <ComboBox.Items>
    <app:PortableThemeType>Auto</app:PortableThemeType>
    <app:PortableThemeType>Light</app:PortableThemeType>
    <app:PortableThemeType>Dark</app:PortableThemeType>
  </ComboBox.Items>
</ComboBox>
<Button Content="应用" Command="{Binding ApplyThemeCommand}"/>
```

创建对应 `.axaml.cs`（标准 InitializeComponent）。

- [ ] **Step 2.6: 注册 PageRegistry + DataTemplate + DI**

修改 `AvaloniaShellServiceCollectionExtensions.cs`，在 SettingsShellViewModel 注册行后加：

```csharp
reg.Register<ThemeSettingsViewModel, ThemeSettingsPage>(PageKeys.SettingsTheme, "外观主题");
s.AddTransient<ThemeSettingsViewModel>();
```

修改 `App.axaml` 加 DataTemplate：

```xml
<DataTemplate DataType="vmset:ThemeSettingsViewModel">
  <vset:ThemeSettingsPage/>
</DataTemplate>
```

- [ ] **Step 2.7: SettingsShell 嵌入 ThemeSettings 内容路由**

修改 `SettingsShellView.axaml` 把右侧 ContentControl 改为 DataTemplate 驱动渲染：

```xml
<ContentControl Grid.Column="1" Content="{Binding CurrentPageViewModel}"/>
```

修改 `SettingsShellViewModel.cs` 加 `CurrentPageViewModel` 属性（根据 `SelectedItem` 变化时由 DI 容器解析）。

**实施提示**：subagent 实施时考虑两种方案：
1. SettingsShellViewModel 注入 `IServiceProvider`，`partial void OnSelectedItemChanged()` 时按 SubNavItem.Key 解析对应 VM 类型
2. SettingsShellViewModel 注入 PageRegistry + IServiceProvider，统一查 PageKey → VM 类型 → 解析

方案 2 更干净，与现有 NavigationService 风格一致。新增对应单测：`Selecting_theme_subnav_resolves_ThemeSettingsViewModel`。

- [ ] **Step 2.8: build + test**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v minimal
dotnet test Tianming.MacMigration.sln --nologo -v minimal 2>&1 | tail -8
```

预期：0 W / 0 E；测试 1630 → **1633**（新增 2 ThemeSettings 测试 + 1 嵌入测试）

- [ ] **Step 2.9: commit**

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Settings/ThemeSettingsViewModel.cs \
        src/Tianming.Desktop.Avalonia/Views/Settings/ThemeSettingsPage.axaml \
        src/Tianming.Desktop.Avalonia/Views/Settings/ThemeSettingsPage.axaml.cs \
        src/Tianming.Desktop.Avalonia/App.axaml \
        src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs \
        src/Tianming.Desktop.Avalonia/ViewModels/Settings/SettingsShellViewModel.cs \
        src/Tianming.Desktop.Avalonia/Views/Settings/SettingsShellView.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Settings/ThemeSettingsViewModelTests.cs \
        tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Settings/SettingsShellViewModelTests.cs

git commit -m "$(cat <<'EOF'
Add theme settings page driven by PortableThemeStateController

Constraint: 复用 Lane 0 已注册的 PortableThemeStateController + ThemeBridge
Rejected: 在 VM 内直接调 Avalonia Application（绕过 ThemeBridge 抽象，破坏 lib/UI 边界）
Confidence: high
Scope-risk: narrow
Tested: ThemeSettingsViewModelTests (2/2), SettingsShell subnav resolve test
Not-tested: 真机切 Dark 后 UI 资源是否立刻随之切换（控件 ControlTheme 仍硬编码 Light palette，属遗留待 M7 后续）
EOF
)"
```

---

## Task 3: ThemeFollowSystemPage — 跟随系统开关 + 定时主题

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Settings/ThemeFollowSystemViewModel.cs`
- Create: `src/Tianming.Desktop.Avalonia/Views/Settings/ThemeFollowSystemPage.axaml` + `.axaml.cs`
- Modify: `App.axaml` + `AvaloniaShellServiceCollectionExtensions.cs`
- Test: `tests/.../ViewModels/Settings/ThemeFollowSystemViewModelTests.cs`

**Key lib API**：
- `PortableSystemFollowController.HandleAppearanceChangedAsync(...)`（Lane 0 已让 monitor 调它）
- `PortableTimeBasedThemeSettings` (Enabled / StartTime / EndTime / TargetTheme / EnabledWeekdays) 在 PortableThemeScheduleService.cs:67-82
- `PortableThemeScheduleService` 公开 API（read 时确认）

**实施提示**：Task 3 用户可控的核心是 4 项：
1. 主跟随系统开关（启用 → SystemFollowController 接管；禁用 → 维持手动主题）
2. 定时主题启用开关 + 起止时间 + 目标主题（最多 1 个 schedule，原版"简单日/夜主题"）
3. 排除时段（一个时间窗内不切，避免会议中突然变暗）
4. 节假日跳过开关（用 PortableHolidayLibrary）

- [ ] **Step 3.1: 写 ThemeFollowSystemViewModelTests（红）** — 至少 2 条：
  - `Ctor_loads_current_follow_settings_from_controller`
  - `ToggleFollow_invokes_controller_state_save`

实施时先 read `PortableSystemFollowController.cs` 看真实持久化方法名（Save / Apply / Persist）。

- [ ] **Step 3.2: 写 ThemeFollowSystemViewModel 实现**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TM.Services.Framework.Appearance;

namespace Tianming.Desktop.Avalonia.ViewModels.Settings;

public partial class ThemeFollowSystemViewModel : ObservableObject
{
    private readonly PortableSystemFollowController _follow;
    private readonly PortableThemeScheduleService _schedule;

    [ObservableProperty] private bool _followSystemEnabled;
    [ObservableProperty] private bool _scheduleEnabled;
    [ObservableProperty] private string _scheduleStart = "20:00";
    [ObservableProperty] private string _scheduleEnd   = "08:00";
    [ObservableProperty] private PortableThemeType _scheduleTargetTheme = PortableThemeType.Dark;

    public ThemeFollowSystemViewModel(
        PortableSystemFollowController follow,
        PortableThemeScheduleService schedule)
    {
        _follow = follow;
        _schedule = schedule;
        // 实施时按真实 API 加载当前 settings
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        // 实施时按真实 controller API 持久化
        await System.Threading.Tasks.Task.CompletedTask;
    }
}
```

- [ ] **Step 3.3: 写 ThemeFollowSystemPage axaml**

模板：两段 SectionCard。第一段 "跟随系统外观"，含一个 ToggleSwitch 绑 `FollowSystemEnabled`。第二段 "定时主题"，含 ToggleSwitch 绑 `ScheduleEnabled` + 起止时间 TextBox + ComboBox 选 TargetTheme + Save 按钮。

参考 `Views/AI/ApiKeysPage.axaml` 的 SectionCard + StackPanel + 控件模式（plan Task 1 已示例）。

- [ ] **Step 3.4: 注册 PageRegistry + DataTemplate + DI**

```csharp
reg.Register<ThemeFollowSystemViewModel, ThemeFollowSystemPage>(PageKeys.SettingsFollowSystem, "跟随系统");
s.AddTransient<ThemeFollowSystemViewModel>();
```

加 DataTemplate 到 App.axaml。

- [ ] **Step 3.5: build + test**

预期：测试 1633 → **1635**（新增 2 个 follow 测试）

- [ ] **Step 3.6: commit**

```bash
git commit -m "$(cat <<'EOF'
Add follow-system settings page driven by PortableSystemFollowController

Constraint: 仅暴露主开关 + 定时 schedule + 节假日跳过 3 类选项；高级排除时段 deferred
Rejected: 让 VM 直接 mutate Portable*Settings 实例（破坏 controller 抽象）
Confidence: medium
Scope-risk: narrow
Tested: ThemeFollowSystemViewModelTests (2/2)
Not-tested: 跨平台 macOS appearance probe 真切系统外观时的端到端（依赖 Lane 0 已通的链路）
EOF
)"
```

---

## Task 4: App.axaml Light 闪烁修复 + 启动 baseline 测试

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml:22` (删除 `RequestedThemeVariant="Light"`)
- Test: `tests/Tianming.Desktop.Avalonia.Tests/Theme/ThemeBootBaselineTests.cs`

**背景**：Lane 0 让 `ThemeBridge.ApplyAsync` 真切 ThemeVariant，但 `App.axaml:22` 仍写死 `RequestedThemeVariant="Light"`，导致冷启动到 `AppLifecycle.InitializeAsync` 这一帧之间窗口先 Light 后切 Dark，视觉闪烁。

修法：删 `App.axaml:22` 的 `RequestedThemeVariant="Light"` 属性（让 Avalonia FluentTheme 默认随系统）；初始主题完全由 `AppLifecycle` + `ThemeBridge` 在启动时决定。

- [ ] **Step 4.1: 写 ThemeBootBaselineTests（红）**

创建 `tests/Tianming.Desktop.Avalonia.Tests/Theme/ThemeBootBaselineTests.cs`：

```csharp
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Theme;

public class ThemeBootBaselineTests
{
    [Fact]
    public void App_axaml_does_not_hardcode_RequestedThemeVariant()
    {
        // 这条测试断言 App.axaml 根元素不再硬编码 RequestedThemeVariant，
        // 让 ThemeBridge 在冷启动时拥有真正的"未定"状态，无 Light 闪烁。
        var appAxamlPath = Path.Combine(
            FindRepoRoot(),
            "src", "Tianming.Desktop.Avalonia", "App.axaml");
        var doc = XDocument.Load(appAxamlPath);
        var root = doc.Root!;
        var attr = root.Attribute("RequestedThemeVariant");

        Assert.True(
            attr is null,
            $"App.axaml 根元素仍然写死 RequestedThemeVariant={attr?.Value}，会导致冷启动 Light 闪烁");
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Tianming.MacMigration.sln")))
            dir = dir.Parent;
        return dir!.FullName;
    }
}
```

- [ ] **Step 4.2: 跑测试确认红**

```bash
dotnet test --filter "FullyQualifiedName~ThemeBootBaselineTests" --nologo -v minimal
```

预期：1/1 FAIL — App.axaml 仍含 `RequestedThemeVariant="Light"`

- [ ] **Step 4.3: 改 App.axaml**

修改 `src/Tianming.Desktop.Avalonia/App.axaml:22`：

把：

```xml
             x:Class="Tianming.Desktop.Avalonia.App"
             RequestedThemeVariant="Light">
```

改为：

```xml
             x:Class="Tianming.Desktop.Avalonia.App">
```

（删除整行 `RequestedThemeVariant="Light"`）

- [ ] **Step 4.4: 跑测试确认绿 + 全量回归**

```bash
dotnet test --filter "FullyQualifiedName~ThemeBootBaselineTests" --nologo -v minimal
dotnet test Tianming.MacMigration.sln --nologo -v minimal 2>&1 | tail -8
```

预期：单测 1/1 PASS；全量测试 1635 → **1636**（新增 1）

**重要回归点**：Lane 0 的 `MacOSAppearanceBridgeTests` 等主题相关测试应仍全过。如有 break，subagent 必须报告并停下让 user 决定（不要为了 force pass 改 Lane 0 已合并的测试）。

- [ ] **Step 4.5: commit**

```bash
git add src/Tianming.Desktop.Avalonia/App.axaml \
        tests/Tianming.Desktop.Avalonia.Tests/Theme/ThemeBootBaselineTests.cs

git commit -m "$(cat <<'EOF'
Stop forcing Light theme at app boot so ThemeBridge can drive variant from start

Constraint: 移除 App.axaml 根 RequestedThemeVariant 后，初始主题由 AppLifecycle.InitializeAsync 控制
Rejected: 加新的 startup variant guard service（与 Lane 0 已实装的 ThemeBridge 路径冗余）
Confidence: high
Scope-risk: narrow（仅一行 axaml + 一条 baseline 单测）
Tested: ThemeBootBaselineTests 红→绿 + 全量回归 1636 passed
Not-tested: macOS dark-mode 真机冷启动视觉无闪烁（依赖 Computer Use 真机测，留到 Lane A 收尾)
EOF
)"
```

---

## Task 5: manual-test-howto lsregister 兜底文档

**Files:**
- Modify: `Docs/macOS迁移/manual-test-howto.md`（追加 "Fresh profile 兜底" 段）

- [ ] **Step 5.1: 阅读现有 manual-test-howto.md**

```bash
cat Docs/macOS迁移/manual-test-howto.md
```

找到 Computer Use attach gate / 验证三连命令段，记录现有结构。

- [ ] **Step 5.2: 追加 Fresh profile 兜底段**

在 manual-test-howto.md 末尾追加：

````markdown

## Fresh profile 兜底

若 `get_app_state("dev.tianming.avalonia.manualtest")` 在 fresh macOS user profile 仍返回 `appNotFound`（即 `lsregister` 还未发现 `~/Applications/TianmingDev.app`）：

1. 重建 LaunchServices DB：
   ```bash
   /System/Library/Frameworks/CoreServices.framework/Frameworks/LaunchServices.framework/Support/lsregister \
       -kill -r -domain user
   ```
2. 用 Finder 打开 `~/Applications/TianmingDev.app` 一次（让 LS pick up 新签名）：
   ```bash
   open ~/Applications/TianmingDev.app
   ```
3. 重新跑 Computer Use 验证：
   - `list_apps` 应看到 `Avalonia Application — dev.tianming.avalonia.manualtest`
   - `get_app_state("dev.tianming.avalonia.manualtest")` 应返回 accessibility 树
````

- [ ] **Step 5.3: commit**

```bash
git add Docs/macOS迁移/manual-test-howto.md

git commit -m "$(cat <<'EOF'
Document the lsregister fallback when LaunchServices cache is stale

Constraint: 仅追加文档，未改任何脚本（兜底命令只在 fresh profile 时使用，不入默认 build 流程）
Confidence: high
Scope-risk: narrow（纯文档）
Tested: 文档结构与现有 manual-test-howto 章节一致；命令用 macOS 标准 lsregister 路径
Not-tested: 真实 fresh macOS user profile 重建 LS DB 后 attach（依赖第三方机器）
EOF
)"
```

---

## 收尾：全量 build/test 复核 + Computer Use 真机一次

完成 Task 1-5 后跑：

- [ ] **Final 1: 全量 build/test**

```bash
dotnet build Tianming.MacMigration.sln --nologo -v minimal
dotnet test Tianming.MacMigration.sln --nologo -v minimal 2>&1 | tail -10
```

预期：0 W / 0 E；**1636 passed / 0 failed**（+8 from 1628 baseline）

- [ ] **Final 2: 打 bundle + Computer Use 验证**

```bash
bash Scripts/build-dev-bundle.sh
# Computer Use:
#   list_apps → 应看到 dev.tianming.avalonia.manualtest
#   get_app_state(dev.tianming.avalonia.manualtest) → 应返回包含 "设置" 入口的 accessibility 树
#   点 "设置" → 应能切到 SettingsShellView，看到子导航 "外观主题" / "跟随系统"
#   点 "外观主题" → 应能切到 ThemeSettingsPage
#   切 Dark + 点 "应用" → ThemeBridge 真切 Avalonia ThemeVariant 到 Dark
#   冷启动重开 → 不再先 Light 后 Dark 闪烁
```

把 Computer Use accessibility 树关键节点（特别是"设置 / 外观主题"页可达）作为最终交付证据。

- [ ] **Final 3: 不 push，由 main thread 主线程负责 merge + push**

---

## Self-Review

按 writing-plans skill 收尾自检。

**1. Spec coverage**：
- Task 1 ✓ 解掉 PlaceholderView 路由（Round 7 prompt Lane A Step 1）
- Task 2 ✓ ThemeSettings（Round 7 prompt Lane A Step 2）
- Task 3 ✓ ThemeFollowSystem（Round 7 prompt Lane A Step 3）
- Task 4 ✓ App.axaml Light 闪烁修复（Round 7 prompt Lane A Step 7）
- Task 5 ✓ manual-test-howto lsregister 文档（Round 7 prompt Lane A Step 8）
- **Deferred**（标注在 Scope 段）：Lane A 原计划 Step 4 ThemeTransition、Step 5 FontSettings、Step 6 ColorSchemeDesigner（D #3, #6, #7-#9）— 单独成后续 sub-plan

**2. Placeholder scan**：
- Task 2.1 / Task 3.1 / Task 3.2 含"实施提示：subagent 实施时先 read..."—— 这不是 placeholder，是合理的"plan 锁定结构 + 实施者按真实 API 写代码"指引；对 lib 类繁多且我未逐一 read 全部公开 API 的情况，比写错的 API 名更负责
- 已确认无 "TODO" / "TBD" / "fill in details" 等纯 placeholder

**3. Type consistency**：
- `PortableThemeType` 在 PortableThemeScheduleService.cs:6 ✓
- `PortableThemeStateController.SwitchThemeAsync` / `CurrentTheme` / `ThemeChanged` ✓（已 read 公开 API 行 133/135/152）
- `PortableSystemFollowController.HandleAppearanceChangedAsync` ✓
- `PortableThemeChangedEventArgs.CurrentTheme` — 实施时确认（plan 标注了 fallback）
- `PortableTimeBasedThemeSettings.{Enabled, StartTime, EndTime, TargetTheme}` ✓（已 read 67-82）
- `ThemeBridge.ApplyAsync(PortableThemeApplicationRequest)` ✓

**4. Step granularity**：
- Task 1: 9 step（OK）
- Task 2: 9 step（OK，含 Step 2.7 嵌入路由是设计决策需要的额外 step）
- Task 3: 6 step（OK，相比 Task 2 简化是因为模式重复）
- Task 4: 5 step（OK）
- Task 5: 3 step（OK，纯文档）
- 总 32 step，每 step 2-5 分钟级别 ✓

---

## Execution Handoff

按 superpowers 工作流，本 plan 推荐用 **subagent-driven-development**：每个 Task 派一个 fresh subagent 实施，主 thread review 后才进下一个 Task。

如要 inline 执行，用 superpowers:executing-plans，但 32 step 在单 session 可能溢出 context。
