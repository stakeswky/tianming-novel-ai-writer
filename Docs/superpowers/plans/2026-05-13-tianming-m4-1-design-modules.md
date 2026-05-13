# M4.1 设计模块 6 页（step-level plan）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 M4.0 schema-driven adapter 之上落地 6 个设计模块页面（世界观/角色/势力/地点/剧情/创意素材），统一通过 `CategoryDataPageView` 三栏 UI + `DataManagementViewModel<,,>` 基类驱动。

**Architecture:** 单个共享 `CategoryDataPageView` user control（左分类列表 / 中条目列表 / 右动态表单 by schema.Fields）+ 6 个一行派生 VM（继承 `DataManagementViewModel<TCategory, TData, TSchema>`）+ 6 个一行 axaml view + 6 个 PageKey + DI 注册 6 个 schema 单例 + 6 个 adapter Transient + DataTemplate 6 条 + LeftNav 启用 6 项。引入 `ICurrentProjectService` 提供项目根目录给 adapter；M4.1 默认根 = `AppPaths.AppSupportDirectory/Projects/Default`（M4 后续接 FileProjectManager）。

**Tech Stack:** Avalonia 11.0.10 / CommunityToolkit.Mvvm 8.2.2 / Microsoft.Extensions.DI 8.0.1 / xUnit 2.9.2。不引新 NuGet。

---

## Scope Alignment（Explore 输出）

### M4.0 实测签名（已读源码确认）

- **IModuleSchema<TCategory, TData>**：成员 = `PageTitle` / `PageIcon` / `ModuleRelativePath` / `Fields` (IReadOnlyList<FieldDescriptor>) / `CreateNewItem()` / `CreateNewCategory(string name)` / `BuildAIPromptContext(IReadOnlyList<TData>)`
- **FieldDescriptor**：record，`(PropertyName, Label, Type, Required, Placeholder, MaxLength = null, EnumOptions = null)`；`FieldType` 枚举：SingleLineText / MultiLineText / Number / Tags / Enum / Boolean
- **ModuleDataAdapter<TCategory, TData>** 公开 API：`ctor(IModuleSchema, projectRoot)` / `Schema` / `LoadAsync()` / `GetCategories()` / `GetData()` / `AddCategoryAsync(c)` / `AddAsync(d)` / `DeleteAsync(id)` / `UpdateAsync(d)` / `CascadeDeleteCategoryAsync(name)` / `GetDataForCategory(cat)`
- **DataManagementViewModel<TCategory, TData, TSchema>** 基类已有公开成员：`Schema (TSchema)` / `PageTitle` / `PageIcon` / `Fields` / `Categories (ObservableCollection)` / `Items (ObservableCollection)` / `SelectedCategory` / `SelectedItem` / `IsLoading` / `LoadAsync()` / `AddCategoryAsync(name)` / `AddNewItemAsync(categoryName, name)` / `UpdateSelectedItemAsync()` / `DeleteSelectedItemAsync()` / `CascadeDeleteCategoryAsync(name)` / `ItemsInSelectedCategory()`
- 注意：基类没有 `[RelayCommand]` 字段，VM 命令都是 public async 方法；form/view 需要直接 `Command="{Binding AddNewItemAsyncCommand}"` 等 — **不行，没有 Command 包装**。所以 view 里用 `Click` 事件转发或直接 wire 到 `Button` 时通过 `RelayCommand` 在 view 侧手动建 wrapper，**或者** 在基类追加 RelayCommand 包装（本 plan 选第 2 条：见 Step 1.1 扩基类）

  > Lane B 与 Lane A 约定：不动 M4.0 接口；但**新增 RelayCommand 包装**在基类 `partial` 文件中（同命名空间另文件），属于"扩展"非"破坏"。Step 1.1 单独提交。

### 已有 schema 字段实测

| Schema | Fields 数 | 关键类型分布 |
|---|---|---|
| WorldRulesSchema | 11 | 1 SingleLine + 10 MultiLine（Name 必填）|
| CharacterRulesSchema | 20 | 1 Enum (CharacterType, 5 options) + 6 SingleLine + 13 MultiLine |
| FactionRulesSchema | 10 | 2 SingleLine + 1 SingleLine(Leader) + 7 MultiLine |
| LocationRulesSchema | 11 | 4 SingleLine + 3 Tags (Landmarks/Resources/Dangers) + 4 MultiLine |
| PlotRulesSchema | 20 | 11 SingleLine + 9 MultiLine |
| CreativeMaterialsSchema | 20 | 4 SingleLine (含 Icon, Genre) + 16 MultiLine |

POCO 字段名与 schema.PropertyName 1:1 一致。Tags 的 POCO 类型是 `List<string>`（LocationRulesData 实测）。

### Shell 关键 API

- **PageKeys.cs**：用 `public static readonly PageKey Welcome = new("welcome");` 模式声明；末注释 `// M4 扩展更多`
- **PageRegistry**：`Register<TViewModel, TView>(PageKey)`；`TryResolve` 用于 NavigationService
- **NavigationService**：`_sp.GetRequiredService(vmType)` 解析 VM；需要 VM 在 DI 注册（`AddTransient` 推荐，每页独立状态）
- **App.axaml `<Application.DataTemplates>`**：VM 类 → View 实例的映射；每个新 VM 都要在这里加 `<DataTemplate DataType="vm:XxxViewModel"><v:XxxView/></DataTemplate>` 一行
- **LeftNavViewModel**：`Groups.Add(new NavRailGroup("写作", new List<NavRailItem> { ... }))`；M3 后已有占位的 "草稿/大纲/角色/世界观" 等 IsEnabled=false 项，本 plan 改为启用并指向真 PageKey
- **NavRailItem record**：`(PageKey Key, string Label, string IconGlyph, bool IsEnabled = true)`
- **IconGlyphIsShortConverter**：长度 ≤ 2 显示，否则隐藏；emoji 长度 = 1 或 2（如 "⚔️" length 2），完美对齐
- **ThreeColumnLayoutViewModel.Center**：导航后接收 `_nav.CurrentViewModel`，再通过 `Application.DataTemplates` 渲染为 View

### 项目根目录注入

无现成 `ICurrentProjectService`。本 plan 新增最小契约：

```csharp
namespace Tianming.Desktop.Avalonia.Infrastructure;
public interface ICurrentProjectService { string ProjectRoot { get; } }
public sealed class CurrentProjectService : ICurrentProjectService {
    public CurrentProjectService(AppPaths paths) =>
        ProjectRoot = System.IO.Path.Combine(paths.AppSupportDirectory, "Projects", "Default");
    public string ProjectRoot { get; }
}
```

DI 注册为 Singleton。M4 后续接 FileProjectManager 时只需替换实现。

### Baseline

- `dotnet build` 0/0
- `dotnet test` 1312 通过（ProjectData 273 + AI 144 + Avalonia 104 + Framework 791）

---

## File Structure

```
src/Tianming.Desktop.Avalonia/
├── Infrastructure/
│   └── CurrentProjectService.cs              # ICurrentProjectService + 默认实现（NEW）
├── ViewModels/
│   ├── DataManagementViewModel.Commands.cs   # partial 扩展：RelayCommand 包装（NEW）
│   └── Design/
│       ├── WorldRulesViewModel.cs            # NEW
│       ├── CharacterRulesViewModel.cs        # NEW
│       ├── FactionRulesViewModel.cs          # NEW
│       ├── LocationRulesViewModel.cs         # NEW
│       ├── PlotRulesViewModel.cs             # NEW
│       └── CreativeMaterialsViewModel.cs     # NEW
├── Views/
│   └── Design/
│       ├── DesignModulePage.axaml(.cs)       # 占位 view，所有 6 页共用（NEW）
│       └── (无需 6 个 axaml — 用 DataTemplate 全部指向 DesignModulePage)
├── Controls/
│   ├── CategoryDataPageView.axaml(.cs)       # 三栏 UI：左 category / 中 items / 右 form（NEW）
│   └── DynamicFieldConverters.cs             # FieldType → 控件可见性 / Tags 双向转换（NEW）
├── Navigation/
│   └── PageKeys.cs                           # 追加 6 个 design.* PageKey（MODIFY）
├── ViewModels/Shell/
│   └── LeftNavViewModel.cs                   # 启用 6 项设计页 navItem（MODIFY）
├── App.axaml                                  # 追加 6 条 DataTemplate（MODIFY）
└── AvaloniaShellServiceCollectionExtensions.cs  # 注册 6 schema / 6 adapter / 6 VM / PageRegistry / ICurrentProjectService（MODIFY）

tests/Tianming.Desktop.Avalonia.Tests/
└── ViewModels/Design/
    ├── WorldRulesViewModelTests.cs           # NEW（≥3）
    ├── CharacterRulesViewModelTests.cs       # NEW（≥3）
    ├── FactionRulesViewModelTests.cs         # NEW（≥3）
    ├── LocationRulesViewModelTests.cs        # NEW（≥3）
    ├── PlotRulesViewModelTests.cs            # NEW（≥3）
    └── CreativeMaterialsViewModelTests.cs    # NEW（≥3）
```

**职责单一性：**
- `CategoryDataPageView` 只负责三栏布局 + schema 驱动渲染；零业务逻辑
- `DataManagementViewModel.Commands.cs` 仅追加 RelayCommand 包装，不动既有 API
- 6 个 ViewModel 完全 thin shell（构造器 + base 调用），便于将来扩展不污染基类
- `CurrentProjectService` 只暴露 `ProjectRoot` 一个字符串，留给 M4 后续替换

---

## 任务步骤

### Task 1：DataManagementViewModel 命令包装（partial 扩展）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/DataManagementViewModel.Commands.cs`
- Modify: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/DataManagementViewModelTests.cs` 追加命令测试

#### Step 1.1：写命令测试

`tests/Tianming.Desktop.Avalonia.Tests/ViewModels/DataManagementViewModelTests.cs` 文件末尾追加（保留现有 7 条测试，新增 3 条）：

```csharp
    [Fact]
    public async Task DeleteSelectedItemCommand_invokes_DeleteSelectedItemAsync()
    {
        using var workspace = new TempDirectory();
        var (adapter, _) = await CreateAdapter(workspace.Path, withItems: true);
        var vm = new TestVm(adapter);
        await vm.LoadAsync();
        vm.SelectedItem = vm.Items[0];
        var idBefore = vm.SelectedItem.Id;

        await vm.DeleteSelectedItemCommand.ExecuteAsync(null);

        Assert.DoesNotContain(vm.Items, i => i.Id == idBefore);
        Assert.Null(vm.SelectedItem);
    }

    [Fact]
    public async Task UpdateSelectedItemCommand_persists_change()
    {
        using var workspace = new TempDirectory();
        var (adapter, _) = await CreateAdapter(workspace.Path, withItems: true);
        var vm = new TestVm(adapter);
        await vm.LoadAsync();
        vm.SelectedItem = vm.Items[0];
        vm.SelectedItem.Name = "更名";

        await vm.UpdateSelectedItemCommand.ExecuteAsync(null);

        await vm.LoadAsync();
        Assert.Contains(vm.Items, i => i.Name == "更名");
    }

    [Fact]
    public async Task AddNewItemInCurrentCategoryCommand_uses_selected_category_name()
    {
        using var workspace = new TempDirectory();
        var (adapter, _) = await CreateAdapter(workspace.Path, withItems: true);
        var vm = new TestVm(adapter);
        await vm.LoadAsync();
        vm.SelectedCategory = vm.Categories[0];

        await vm.AddNewItemInCurrentCategoryCommand.ExecuteAsync(null);

        Assert.Contains(vm.Items, i => i.Category == "C1" && i.Name == "新条目");
    }
```

#### Step 1.2：跑测试验证失败

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter DataManagementViewModelTests -v q`

Expected: 3 个新测试 FAIL with "DeleteSelectedItemCommand / UpdateSelectedItemCommand / AddNewItemInCurrentCategoryCommand does not exist"。

#### Step 1.3：实装 partial 扩展

`src/Tianming.Desktop.Avalonia/ViewModels/DataManagementViewModel.Commands.cs`：

```csharp
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using TM.Framework.Common.Models;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels;

// 给基类追加 RelayCommand 包装：M4.1 view 通过 {Binding XxxCommand} 绑定。
// 不动 M4.0 接口，仅扩展。
public abstract partial class DataManagementViewModel<TCategory, TData, TSchema>
{
    [RelayCommand]
    private Task DeleteSelectedItemAsyncCommandImpl() => DeleteSelectedItemAsync();

    [RelayCommand]
    private Task UpdateSelectedItemAsyncCommandImpl() => UpdateSelectedItemAsync();

    [RelayCommand]
    private async Task AddNewItemInCurrentCategoryAsync()
    {
        if (SelectedCategory == null) return;
        var created = await AddNewItemAsync(categoryName: SelectedCategory.Name, name: "新条目").ConfigureAwait(false);
        if (created != null) SelectedItem = created;
    }

    [RelayCommand]
    private async Task AddCategoryWithDefaultNameAsync()
    {
        // UI 暂时不弹输入框 — 先用 "新分类 N" 占位（M4.1 个人自用可接受，M4 后续接对话框）
        var n = Categories.Count + 1;
        await AddCategoryAsync($"新分类 {n}").ConfigureAwait(false);
    }
}
```

> 注：CommunityToolkit.Mvvm 的 `[RelayCommand]` 源生成器根据方法名 `XxxAsyncCommandImpl` 会生成 `XxxAsyncCommandImplCommand` 而非 `DeleteSelectedItemCommand`。所以**改方法名**为 `DeleteSelectedItem` / `UpdateSelectedItem`：

正确写法：

```csharp
public abstract partial class DataManagementViewModel<TCategory, TData, TSchema>
{
    [RelayCommand]
    private Task DeleteSelectedItem() => DeleteSelectedItemAsync();

    [RelayCommand]
    private Task UpdateSelectedItem() => UpdateSelectedItemAsync();

    [RelayCommand]
    private async Task AddNewItemInCurrentCategory()
    {
        if (SelectedCategory == null) return;
        var created = await AddNewItemAsync(categoryName: SelectedCategory.Name, name: "新条目").ConfigureAwait(false);
        if (created != null) SelectedItem = created;
    }

    [RelayCommand]
    private async Task AddCategoryWithDefaultName()
    {
        var n = Categories.Count + 1;
        await AddCategoryAsync($"新分类 {n}").ConfigureAwait(false);
    }
}
```

生成的命令名是 `DeleteSelectedItemCommand` / `UpdateSelectedItemCommand` / `AddNewItemInCurrentCategoryCommand` / `AddCategoryWithDefaultNameCommand`（Mvvm 源生成器规则：去 Async 后缀，加 Command 后缀；同步方法则 `XxxCommand`）。

> **关键 type-check**：基类已是 `public abstract partial class DataManagementViewModel<,,>` —— partial 修饰符已有，分文件追加 OK。

#### Step 1.4：跑测试验证全通

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter DataManagementViewModelTests -v q`

Expected: 10 个测试 PASS（原 7 + 新 3）

#### Step 1.5：commit

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/DataManagementViewModel.Commands.cs \
        tests/Tianming.Desktop.Avalonia.Tests/ViewModels/DataManagementViewModelTests.cs
git commit -m "feat(avalonia): DataManagementViewModel 命令包装 (RelayCommand partial)"
```

---

### Task 2：ICurrentProjectService + DI 注册

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Infrastructure/CurrentProjectService.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/CurrentProjectServiceTests.cs`

#### Step 2.1：写测试

`tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/CurrentProjectServiceTests.cs`：

```csharp
using System.IO;
using Tianming.Desktop.Avalonia.Infrastructure;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class CurrentProjectServiceTests
{
    [Fact]
    public void ProjectRoot_combines_app_support_with_Projects_Default()
    {
        var paths = new AppPaths(libraryRoot: "/tmp/test-lib");
        var svc = new CurrentProjectService(paths);

        var expected = Path.Combine("/tmp/test-lib", "Application Support", "Tianming", "Projects", "Default");
        Assert.Equal(expected, svc.ProjectRoot);
    }

    [Fact]
    public void ProjectRoot_is_non_empty()
    {
        var paths = new AppPaths(libraryRoot: "/var/folders/xxx");
        var svc = new CurrentProjectService(paths);

        Assert.False(string.IsNullOrWhiteSpace(svc.ProjectRoot));
    }
}
```

#### Step 2.2：跑测试验证失败

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter CurrentProjectServiceTests -v q`

Expected: FAIL with "CurrentProjectService not found"。

#### Step 2.3：实装

`src/Tianming.Desktop.Avalonia/Infrastructure/CurrentProjectService.cs`：

```csharp
using System.IO;

namespace Tianming.Desktop.Avalonia.Infrastructure;

/// <summary>
/// M4.1 项目根目录契约。当前用 AppPaths/Projects/Default；M4 后续接 FileProjectManager 时换实现即可。
/// </summary>
public interface ICurrentProjectService
{
    string ProjectRoot { get; }
}

public sealed class CurrentProjectService : ICurrentProjectService
{
    public CurrentProjectService(AppPaths paths)
    {
        ProjectRoot = Path.Combine(paths.AppSupportDirectory, "Projects", "Default");
    }

    public string ProjectRoot { get; }
}
```

#### Step 2.4：跑测试 + commit

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter CurrentProjectServiceTests -v q`

Expected: 2 测试 PASS。

```bash
git add src/Tianming.Desktop.Avalonia/Infrastructure/CurrentProjectService.cs \
        tests/Tianming.Desktop.Avalonia.Tests/Infrastructure/CurrentProjectServiceTests.cs
git commit -m "feat(avalonia): ICurrentProjectService 占位实现 (M4 后续接 FileProjectManager)"
```

---

### Task 3：CategoryDataPageView 共享控件 + 字段渲染

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Controls/CategoryDataPageView.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Controls/CategoryDataPageView.axaml.cs`
- Create: `src/Tianming.Desktop.Avalonia/Controls/DynamicFieldConverters.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml` 追加 `<ResourceInclude Source="avares://.../Controls/CategoryDataPageView.axaml"/>`

> **设计决策**：CategoryDataPageView 是 `UserControl`（非 TemplatedControl），DataContext = `DataManagementViewModel<,,>` 派生 VM；通过 `x:CompileBindings="False"` 避免泛型基类 binding 问题（基类有 3 个类型参数，编译绑定难写）。表单字段渲染：用 `ItemsControl ItemsSource="{Binding Fields}"` + 每个 FieldDescriptor 一个 DataTemplate（用 `FieldType` 判断）。字段值读写：因为运行时通过反射读 `SelectedItem.GetType().GetProperty(field.PropertyName)`，所以表单 TextBox 不能用 `{Binding}` 直接对接 SelectedItem 的属性 —— 在 view 的 code-behind 里写一个 `FieldRowBuilder` 把每个 Field row 手工 wire 到 reflection。

> **更简单的实现**：用 `Binding Path` 字符串拼接 `SelectedItem.<PropertyName>`，运行时绑定（CompileBindings=False 即可）。验证：Avalonia 11 支持 `Binding "SelectedItem.PowerSystem"` 这种路径，并且 SelectedItem 是 IDataItem 接口 —— 但反射 binding 在编译期不知道 PowerSystem 存不存在，运行时会 fail silently 或抛错。

> **可靠选择**：DataFormView 在 code-behind 里 dynamically 构建子控件树。每个 FieldDescriptor 一个 `Grid(2 cols)` row，左 Label，右 控件（TextBox / NumericUpDown / ComboBox），TwoWay 绑定到 `SelectedItem` + 字符串 Path。

#### Step 3.1：写 DynamicFieldConverters

`src/Tianming.Desktop.Avalonia/Controls/DynamicFieldConverters.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia.Data.Converters;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.Controls;

/// <summary>List<string> ↔ "a, b, c" 双向转换（Tags 字段）。</summary>
public sealed class TagsListStringConverter : IValueConverter
{
    public static readonly TagsListStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is IEnumerable<string> list ? string.Join(", ", list) : string.Empty;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s))
            return new List<string>();
        return s.Split(new[] { ',', '，' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }
}

/// <summary>FieldType 等值匹配 — 用于 DataTrigger 隐显单元格。</summary>
public sealed class FieldTypeEqualsConverter : IValueConverter
{
    public static readonly FieldTypeEqualsConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is FieldType t && parameter is string s && Enum.TryParse<FieldType>(s, out var p) && t == p;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
```

#### Step 3.2：写 CategoryDataPageView axaml

`src/Tianming.Desktop.Avalonia/Controls/CategoryDataPageView.axaml`：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:Tianming.Desktop.Avalonia.Controls"
             x:Class="Tianming.Desktop.Avalonia.Controls.CategoryDataPageView"
             x:CompileBindings="False"
             FontFamily="{DynamicResource FontUI}">
  <Grid ColumnDefinitions="220, 4, 280, 4, *">
    <!-- 左：分类列表 -->
    <Border Grid.Column="0" Background="{DynamicResource SurfaceCanvasBrush}" Padding="12">
      <DockPanel>
        <StackPanel DockPanel.Dock="Top" Margin="0,0,0,8" Spacing="6">
          <StackPanel Orientation="Horizontal" Spacing="6">
            <TextBlock Text="{Binding PageIcon}"
                       FontSize="{DynamicResource FontSizeH2}"
                       VerticalAlignment="Center"/>
            <TextBlock Text="{Binding PageTitle}"
                       FontSize="{DynamicResource FontSizeH2}"
                       FontWeight="{DynamicResource FontWeightSemibold}"
                       Foreground="{DynamicResource TextPrimaryBrush}"
                       VerticalAlignment="Center"/>
          </StackPanel>
          <Button Classes="ghost"
                  Content="+ 新建分类"
                  HorizontalAlignment="Stretch"
                  Command="{Binding AddCategoryWithDefaultNameCommand}"/>
        </StackPanel>
        <ListBox ItemsSource="{Binding Categories}"
                 SelectedItem="{Binding SelectedCategory, Mode=TwoWay}"
                 Background="Transparent"
                 BorderThickness="0">
          <ListBox.ItemTemplate>
            <DataTemplate>
              <StackPanel Orientation="Horizontal" Spacing="6" Margin="0,2">
                <TextBlock Text="{Binding Icon}" Width="20"
                           VerticalAlignment="Center"
                           FontSize="{DynamicResource FontSizeBody}"/>
                <TextBlock Text="{Binding Name}"
                           VerticalAlignment="Center"
                           Foreground="{DynamicResource TextPrimaryBrush}"/>
              </StackPanel>
            </DataTemplate>
          </ListBox.ItemTemplate>
        </ListBox>
      </DockPanel>
    </Border>

    <GridSplitter Grid.Column="1" Background="{DynamicResource BorderBrush}" ResizeDirection="Columns"/>

    <!-- 中：条目列表 -->
    <Border Grid.Column="2" Background="{DynamicResource SurfaceCanvasBrush}" Padding="12">
      <DockPanel>
        <StackPanel DockPanel.Dock="Top" Margin="0,0,0,8" Spacing="6">
          <TextBlock Text="条目"
                     FontSize="{DynamicResource FontSizeBody}"
                     FontWeight="{DynamicResource FontWeightSemibold}"
                     Foreground="{DynamicResource TextSecondaryBrush}"/>
          <Button Classes="ghost"
                  Content="+ 在当前分类新建"
                  HorizontalAlignment="Stretch"
                  Command="{Binding AddNewItemInCurrentCategoryCommand}"/>
        </StackPanel>
        <ListBox Name="ItemsList"
                 ItemsSource="{Binding Items}"
                 SelectedItem="{Binding SelectedItem, Mode=TwoWay}"
                 Background="Transparent"
                 BorderThickness="0">
          <ListBox.ItemTemplate>
            <DataTemplate>
              <StackPanel Margin="0,2" Spacing="2">
                <TextBlock Text="{Binding Name}"
                           FontSize="{DynamicResource FontSizeBody}"
                           Foreground="{DynamicResource TextPrimaryBrush}"/>
                <TextBlock Text="{Binding Category}"
                           FontSize="{DynamicResource FontSizeCaption}"
                           Foreground="{DynamicResource TextTertiaryBrush}"/>
              </StackPanel>
            </DataTemplate>
          </ListBox.ItemTemplate>
        </ListBox>
      </DockPanel>
    </Border>

    <GridSplitter Grid.Column="3" Background="{DynamicResource BorderBrush}" ResizeDirection="Columns"/>

    <!-- 右：动态表单（code-behind 构建） -->
    <Border Grid.Column="4" Background="{DynamicResource SurfacePanelBrush}" Padding="16">
      <DockPanel>
        <StackPanel DockPanel.Dock="Top" Orientation="Horizontal" Spacing="8" Margin="0,0,0,12">
          <Button Classes="primary"
                  Content="保存"
                  Command="{Binding UpdateSelectedItemCommand}"/>
          <Button Classes="ghost"
                  Content="删除"
                  Command="{Binding DeleteSelectedItemCommand}"/>
        </StackPanel>
        <ScrollViewer HorizontalScrollBarVisibility="Disabled">
          <StackPanel Name="FormHost" Spacing="10"/>
        </ScrollViewer>
      </DockPanel>
    </Border>
  </Grid>
</UserControl>
```

#### Step 3.3：写 CategoryDataPageView code-behind（动态表单）

`src/Tianming.Desktop.Avalonia/Controls/CategoryDataPageView.axaml.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.Controls;

public partial class CategoryDataPageView : UserControl
{
    private StackPanel? _formHost;

    public CategoryDataPageView()
    {
        InitializeComponent();
        _formHost = this.FindControl<StackPanel>("FormHost");
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // 当 DataContext 是 DataManagementViewModel 派生 VM 时，监听 SelectedItem 变化重建表单。
        if (DataContext is INotifyPropertyChanged inpc)
        {
            inpc.PropertyChanged -= OnVmPropertyChanged;
            inpc.PropertyChanged += OnVmPropertyChanged;
        }
        RebuildForm();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "SelectedItem")
            RebuildForm();
    }

    private void RebuildForm()
    {
        if (_formHost == null) return;
        _formHost.Children.Clear();

        var vm = DataContext;
        if (vm == null) return;

        var fieldsProp = vm.GetType().GetProperty("Fields");
        var selectedItemProp = vm.GetType().GetProperty("SelectedItem");
        if (fieldsProp == null || selectedItemProp == null) return;

        var fields = fieldsProp.GetValue(vm) as IReadOnlyList<FieldDescriptor>;
        var selectedItem = selectedItemProp.GetValue(vm);
        if (fields == null) return;

        foreach (var field in fields)
        {
            var row = BuildFieldRow(field, selectedItem);
            _formHost.Children.Add(row);
        }
    }

    private static Control BuildFieldRow(FieldDescriptor field, object? selectedItem)
    {
        var label = new TextBlock
        {
            Text = field.Label + (field.Required ? " *" : string.Empty),
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
        };

        Control editor = field.Type switch
        {
            FieldType.MultiLineText => CreateMultiLineTextBox(field, selectedItem),
            FieldType.Number        => CreateNumericTextBox(field, selectedItem),
            FieldType.Tags          => CreateTagsTextBox(field, selectedItem),
            FieldType.Enum          => CreateEnumComboBox(field, selectedItem),
            FieldType.Boolean       => CreateBooleanCheckBox(field, selectedItem),
            _                       => CreateSingleLineTextBox(field, selectedItem),
        };

        return new StackPanel
        {
            Spacing = 2,
            Children = { label, editor }
        };
    }

    private static TextBox CreateSingleLineTextBox(FieldDescriptor field, object? item)
    {
        var tb = new TextBox { Watermark = field.Placeholder ?? string.Empty };
        BindStringProperty(tb, TextBox.TextProperty, field.PropertyName, item, converter: null);
        return tb;
    }

    private static TextBox CreateMultiLineTextBox(FieldDescriptor field, object? item)
    {
        var tb = new TextBox
        {
            Watermark = field.Placeholder ?? string.Empty,
            AcceptsReturn = true,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MinHeight = 80,
            MaxHeight = 200,
        };
        BindStringProperty(tb, TextBox.TextProperty, field.PropertyName, item, converter: null);
        return tb;
    }

    private static TextBox CreateNumericTextBox(FieldDescriptor field, object? item)
    {
        // 简化：用 TextBox + 数字格式。NumericUpDown 在 Avalonia 11 行为略不一致，先 TextBox 凑合。
        var tb = new TextBox { Watermark = field.Placeholder ?? "0" };
        BindStringProperty(tb, TextBox.TextProperty, field.PropertyName, item, converter: null);
        return tb;
    }

    private static TextBox CreateTagsTextBox(FieldDescriptor field, object? item)
    {
        var tb = new TextBox { Watermark = field.Placeholder ?? "逗号分隔" };
        BindStringProperty(tb, TextBox.TextProperty, field.PropertyName, item, converter: TagsListStringConverter.Instance);
        return tb;
    }

    private static ComboBox CreateEnumComboBox(FieldDescriptor field, object? item)
    {
        var cb = new ComboBox
        {
            ItemsSource = field.EnumOptions?.ToList() ?? new List<string>(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        BindStringProperty(cb, ComboBox.SelectedItemProperty, field.PropertyName, item, converter: null);
        return cb;
    }

    private static CheckBox CreateBooleanCheckBox(FieldDescriptor field, object? item)
    {
        var cbox = new CheckBox { Content = field.Label };
        BindStringProperty(cbox, ToggleButton.IsCheckedProperty, field.PropertyName, item, converter: null);
        return cbox;
    }

    private static void BindStringProperty(
        Control control,
        AvaloniaProperty targetProperty,
        string sourcePropertyName,
        object? source,
        IValueConverter? converter)
    {
        if (source == null) return;
        var prop = source.GetType().GetProperty(sourcePropertyName);
        if (prop == null) return;

        // 用 ReflectionBinding：DataContext = SelectedItem，路径 = PropertyName
        control.DataContext = source;
        var binding = new Binding(sourcePropertyName)
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.LostFocus,
            Converter = converter,
        };
        control.Bind(targetProperty, binding);
    }
}
```

> 注：使用 `Avalonia.Controls.Primitives.ToggleButton.IsCheckedProperty`（CheckBox 继承）。

#### Step 3.4：注册 CategoryDataPageView 资源

修改 `src/Tianming.Desktop.Avalonia/App.axaml`，在 `<ResourceDictionary.MergedDictionaries>` 列表中追加（位置：所有 controls 资源末尾）：

```xml
        <ResourceInclude Source="avares://Tianming.Desktop.Avalonia/Controls/CategoryDataPageView.axaml"/>
```

> 注：CategoryDataPageView 是 UserControl，axaml 含 `<UserControl ...>`，ResourceInclude 不是必须 — 但为统一加载且 axaml 文件随类型走，本控件**不需要**ResourceInclude；UserControl 通过 axaml 内联编译。所以**跳过此步骤**。

#### Step 3.5：跑 build

Run: `dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj -v q`

Expected: 0 Warning / 0 Error。

#### Step 3.6：commit

```bash
git add src/Tianming.Desktop.Avalonia/Controls/CategoryDataPageView.axaml \
        src/Tianming.Desktop.Avalonia/Controls/CategoryDataPageView.axaml.cs \
        src/Tianming.Desktop.Avalonia/Controls/DynamicFieldConverters.cs
git commit -m "feat(controls): CategoryDataPageView 三栏共享控件 (M4.1 + M4.2 复用)"
```

---

### Task 4：6 个 PageKey 新增

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs`

#### Step 4.1：实装

替换 `src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs`：

```csharp
namespace Tianming.Desktop.Avalonia.Navigation;

public readonly record struct PageKey(string Id);

public static class PageKeys
{
    public static readonly PageKey Welcome   = new("welcome");
    public static readonly PageKey Dashboard = new("dashboard");
    public static readonly PageKey Settings  = new("settings");

    // M4.1 设计模块（6 页）
    public static readonly PageKey DesignWorld     = new("design.world");
    public static readonly PageKey DesignCharacter = new("design.character");
    public static readonly PageKey DesignFaction   = new("design.faction");
    public static readonly PageKey DesignLocation  = new("design.location");
    public static readonly PageKey DesignPlot      = new("design.plot");
    public static readonly PageKey DesignMaterials = new("design.materials");
}
```

#### Step 4.2：跑 build

Run: `dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj -v q`

Expected: 0 Warning / 0 Error。

#### Step 4.3：commit

```bash
git add src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs
git commit -m "feat(nav): M4.1 design.* 6 PageKey 入仓"
```

---

### Task 5：WorldRulesViewModel + 测试

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Design/WorldRulesViewModel.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Design/WorldRulesViewModelTests.cs`

#### Step 5.1：写测试

`tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Design/WorldRulesViewModelTests.cs`：

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Modules.Design.WorldRules;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels.Design;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Design;

public class WorldRulesViewModelTests
{
    [Fact]
    public void PageTitle_and_icon_proxy_to_schema()
    {
        var (vm, _) = NewVm();
        Assert.Equal("世界观规则", vm.PageTitle);
        Assert.Equal("🌍", vm.PageIcon);
    }

    [Fact]
    public async Task LoadAsync_with_seeded_data_populates_both_collections()
    {
        var (vm, root) = NewVm();
        var schema = vm.Schema;
        var adapter = new ModuleDataAdapter<WorldRulesCategory, WorldRulesData>(schema, root);
        await adapter.LoadAsync();
        await adapter.AddCategoryAsync(schema.CreateNewCategory("主线"));
        var item = schema.CreateNewItem();
        item.Name = "九州大陆";
        item.Category = "主线";
        item.PowerSystem = "灵气";
        await adapter.AddAsync(item);

        await vm.LoadAsync();

        Assert.Single(vm.Categories);
        Assert.Single(vm.Items, i => i.Name == "九州大陆");
    }

    [Fact]
    public async Task AddCategoryCommand_persists_and_appears_in_collection()
    {
        var (vm, _) = NewVm();
        await vm.LoadAsync();

        var ok = await vm.AddCategoryAsync("主线");

        Assert.True(ok);
        Assert.Contains(vm.Categories, c => c.Name == "主线");
    }

    [Fact]
    public async Task AddNewItemInCurrentCategoryCommand_creates_with_selected_category_name()
    {
        var (vm, _) = NewVm();
        await vm.LoadAsync();
        await vm.AddCategoryAsync("主线");
        vm.SelectedCategory = vm.Categories[0];

        await vm.AddNewItemInCurrentCategoryCommand.ExecuteAsync(null);

        Assert.Single(vm.Items, i => i.Category == "主线");
    }

    private static (WorldRulesViewModel vm, string root) NewVm()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-wr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var schema = new WorldRulesSchema();
        var adapter = new ModuleDataAdapter<WorldRulesCategory, WorldRulesData>(schema, root);
        return (new WorldRulesViewModel(adapter), root);
    }
}
```

#### Step 5.2：跑测试验证失败

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter WorldRulesViewModelTests -v q`

Expected: FAIL with "WorldRulesViewModel not found"。

#### Step 5.3：实装 VM

`src/Tianming.Desktop.Avalonia/ViewModels/Design/WorldRulesViewModel.cs`：

```csharp
using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Modules.Design.WorldRules;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels.Design;

public sealed partial class WorldRulesViewModel
    : DataManagementViewModel<WorldRulesCategory, WorldRulesData, WorldRulesSchema>
{
    public WorldRulesViewModel(ModuleDataAdapter<WorldRulesCategory, WorldRulesData> adapter)
        : base(adapter) { }
}
```

#### Step 5.4：跑测试 + commit

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter WorldRulesViewModelTests -v q`

Expected: 4 测试 PASS。

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Design/WorldRulesViewModel.cs \
        tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Design/WorldRulesViewModelTests.cs
git commit -m "feat(design): WorldRulesViewModel + 4 测试"
```

---

### Task 6：CharacterRulesViewModel + 测试

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Design/CharacterRulesViewModel.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Design/CharacterRulesViewModelTests.cs`

#### Step 6.1：写测试

`tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Design/CharacterRulesViewModelTests.cs`：

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Modules.Design.CharacterRules;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels.Design;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Design;

public class CharacterRulesViewModelTests
{
    [Fact]
    public void Fields_include_CharacterType_enum()
    {
        var (vm, _) = NewVm();
        var ct = vm.Fields.Single(f => f.PropertyName == "CharacterType");
        Assert.Equal(FieldType.Enum, ct.Type);
        Assert.NotNull(ct.EnumOptions);
        Assert.Contains("主角", ct.EnumOptions!);
    }

    [Fact]
    public async Task LoadAsync_with_seeded_character_populates_items()
    {
        var (vm, root) = NewVm();
        var schema = vm.Schema;
        var adapter = new ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData>(schema, root);
        await adapter.LoadAsync();
        await adapter.AddCategoryAsync(schema.CreateNewCategory("主角"));
        var c = schema.CreateNewItem();
        c.Name = "张三";
        c.Category = "主角";
        c.CharacterType = "主角";
        await adapter.AddAsync(c);

        await vm.LoadAsync();

        Assert.Single(vm.Items, i => i.Name == "张三");
    }

    [Fact]
    public async Task DeleteSelectedItemCommand_removes_character()
    {
        var (vm, _) = NewVm();
        await vm.LoadAsync();
        await vm.AddCategoryAsync("主角");
        vm.SelectedCategory = vm.Categories[0];
        await vm.AddNewItemInCurrentCategoryCommand.ExecuteAsync(null);
        vm.SelectedItem!.Name = "李四";
        await vm.UpdateSelectedItemCommand.ExecuteAsync(null);

        await vm.DeleteSelectedItemCommand.ExecuteAsync(null);

        Assert.Empty(vm.Items);
    }

    private static (CharacterRulesViewModel vm, string root) NewVm()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-cr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var schema = new CharacterRulesSchema();
        var adapter = new ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData>(schema, root);
        return (new CharacterRulesViewModel(adapter), root);
    }
}
```

#### Step 6.2：跑测试验证失败

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter CharacterRulesViewModelTests -v q`

Expected: FAIL with "CharacterRulesViewModel not found"。

#### Step 6.3：实装 VM

`src/Tianming.Desktop.Avalonia/ViewModels/Design/CharacterRulesViewModel.cs`：

```csharp
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Modules.Design.CharacterRules;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels.Design;

public sealed partial class CharacterRulesViewModel
    : DataManagementViewModel<CharacterRulesCategory, CharacterRulesData, CharacterRulesSchema>
{
    public CharacterRulesViewModel(ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData> adapter)
        : base(adapter) { }
}
```

#### Step 6.4：跑测试 + commit

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter CharacterRulesViewModelTests -v q`

Expected: 3 测试 PASS。

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Design/CharacterRulesViewModel.cs \
        tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Design/CharacterRulesViewModelTests.cs
git commit -m "feat(design): CharacterRulesViewModel + 3 测试"
```

---

### Task 7：FactionRulesViewModel + 测试

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Design/FactionRulesViewModel.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Design/FactionRulesViewModelTests.cs`

#### Step 7.1：写测试

`tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Design/FactionRulesViewModelTests.cs`：

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Design.Factions;
using TM.Services.Modules.ProjectData.Modules.Design.FactionRules;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels.Design;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Design;

public class FactionRulesViewModelTests
{
    [Fact]
    public void PageTitle_is_势力规则()
    {
        var (vm, _) = NewVm();
        Assert.Equal("势力规则", vm.PageTitle);
    }

    [Fact]
    public async Task AddCategoryAsync_persists_to_disk()
    {
        var (vm, root) = NewVm();
        await vm.LoadAsync();

        await vm.AddCategoryAsync("正派");
        await vm.LoadAsync(); // reload from disk

        Assert.Contains(vm.Categories, c => c.Name == "正派");
    }

    [Fact]
    public async Task AddNewItemInCurrentCategoryCommand_then_Update_changes_persist()
    {
        var (vm, _) = NewVm();
        await vm.LoadAsync();
        await vm.AddCategoryAsync("正派");
        vm.SelectedCategory = vm.Categories[0];
        await vm.AddNewItemInCurrentCategoryCommand.ExecuteAsync(null);
        vm.SelectedItem!.Name = "青云宗";
        vm.SelectedItem.Goal = "守护苍生";

        await vm.UpdateSelectedItemCommand.ExecuteAsync(null);
        await vm.LoadAsync();

        Assert.Contains(vm.Items, i => i.Name == "青云宗" && i.Goal == "守护苍生");
    }

    private static (FactionRulesViewModel vm, string root) NewVm()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-fr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var schema = new FactionRulesSchema();
        var adapter = new ModuleDataAdapter<FactionRulesCategory, FactionRulesData>(schema, root);
        return (new FactionRulesViewModel(adapter), root);
    }
}
```

#### Step 7.2：跑测试验证失败

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter FactionRulesViewModelTests -v q`

Expected: FAIL with "FactionRulesViewModel not found"。

#### Step 7.3：实装 VM

`src/Tianming.Desktop.Avalonia/ViewModels/Design/FactionRulesViewModel.cs`：

```csharp
using TM.Services.Modules.ProjectData.Models.Design.Factions;
using TM.Services.Modules.ProjectData.Modules.Design.FactionRules;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels.Design;

public sealed partial class FactionRulesViewModel
    : DataManagementViewModel<FactionRulesCategory, FactionRulesData, FactionRulesSchema>
{
    public FactionRulesViewModel(ModuleDataAdapter<FactionRulesCategory, FactionRulesData> adapter)
        : base(adapter) { }
}
```

#### Step 7.4：跑测试 + commit

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter FactionRulesViewModelTests -v q`

Expected: 3 测试 PASS。

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Design/FactionRulesViewModel.cs \
        tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Design/FactionRulesViewModelTests.cs
git commit -m "feat(design): FactionRulesViewModel + 3 测试"
```

---

### Task 8：LocationRulesViewModel + 测试（含 Tags 字段断言）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Design/LocationRulesViewModel.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Design/LocationRulesViewModelTests.cs`

#### Step 8.1：写测试

`tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Design/LocationRulesViewModelTests.cs`：

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Design.Location;
using TM.Services.Modules.ProjectData.Modules.Design.LocationRules;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels.Design;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Design;

public class LocationRulesViewModelTests
{
    [Fact]
    public void Fields_include_Tags_for_Landmarks_Resources_Dangers()
    {
        var (vm, _) = NewVm();
        Assert.Equal(FieldType.Tags, vm.Fields.Single(f => f.PropertyName == "Landmarks").Type);
        Assert.Equal(FieldType.Tags, vm.Fields.Single(f => f.PropertyName == "Resources").Type);
        Assert.Equal(FieldType.Tags, vm.Fields.Single(f => f.PropertyName == "Dangers").Type);
    }

    [Fact]
    public async Task AddNewItem_with_landmarks_round_trips()
    {
        var (vm, _) = NewVm();
        await vm.LoadAsync();
        await vm.AddCategoryAsync("主线");
        vm.SelectedCategory = vm.Categories[0];
        await vm.AddNewItemInCurrentCategoryCommand.ExecuteAsync(null);
        vm.SelectedItem!.Name = "青云山";
        vm.SelectedItem.Landmarks = new() { "藏经阁", "练剑峰" };

        await vm.UpdateSelectedItemCommand.ExecuteAsync(null);
        await vm.LoadAsync();

        var loaded = vm.Items.Single(i => i.Name == "青云山");
        Assert.Equal(2, loaded.Landmarks.Count);
        Assert.Contains("藏经阁", loaded.Landmarks);
    }

    [Fact]
    public async Task DeleteSelectedItem_clears_selection()
    {
        var (vm, _) = NewVm();
        await vm.LoadAsync();
        await vm.AddCategoryAsync("主线");
        vm.SelectedCategory = vm.Categories[0];
        await vm.AddNewItemInCurrentCategoryCommand.ExecuteAsync(null);

        await vm.DeleteSelectedItemCommand.ExecuteAsync(null);

        Assert.Null(vm.SelectedItem);
        Assert.Empty(vm.Items);
    }

    private static (LocationRulesViewModel vm, string root) NewVm()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-lr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var schema = new LocationRulesSchema();
        var adapter = new ModuleDataAdapter<LocationRulesCategory, LocationRulesData>(schema, root);
        return (new LocationRulesViewModel(adapter), root);
    }
}
```

#### Step 8.2：跑测试验证失败

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter LocationRulesViewModelTests -v q`

Expected: FAIL with "LocationRulesViewModel not found"。

#### Step 8.3：实装 VM

`src/Tianming.Desktop.Avalonia/ViewModels/Design/LocationRulesViewModel.cs`：

```csharp
using TM.Services.Modules.ProjectData.Models.Design.Location;
using TM.Services.Modules.ProjectData.Modules.Design.LocationRules;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels.Design;

public sealed partial class LocationRulesViewModel
    : DataManagementViewModel<LocationRulesCategory, LocationRulesData, LocationRulesSchema>
{
    public LocationRulesViewModel(ModuleDataAdapter<LocationRulesCategory, LocationRulesData> adapter)
        : base(adapter) { }
}
```

#### Step 8.4：跑测试 + commit

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter LocationRulesViewModelTests -v q`

Expected: 3 测试 PASS。

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Design/LocationRulesViewModel.cs \
        tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Design/LocationRulesViewModelTests.cs
git commit -m "feat(design): LocationRulesViewModel + 3 测试 (含 Tags 字段验证)"
```

---

### Task 9：PlotRulesViewModel + 测试

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Design/PlotRulesViewModel.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Design/PlotRulesViewModelTests.cs`

#### Step 9.1：写测试

`tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Design/PlotRulesViewModelTests.cs`：

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Design.Plot;
using TM.Services.Modules.ProjectData.Modules.Design.PlotRules;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels.Design;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Design;

public class PlotRulesViewModelTests
{
    [Fact]
    public void Fields_count_is_20()
    {
        var (vm, _) = NewVm();
        Assert.Equal(20, vm.Fields.Count);
    }

    [Fact]
    public async Task AddCategoryAsync_persists()
    {
        var (vm, _) = NewVm();
        await vm.LoadAsync();

        await vm.AddCategoryAsync("开端");
        await vm.LoadAsync();

        Assert.Contains(vm.Categories, c => c.Name == "开端");
    }

    [Fact]
    public async Task NewItem_in_category_updates_and_round_trips()
    {
        var (vm, _) = NewVm();
        await vm.LoadAsync();
        await vm.AddCategoryAsync("开端");
        vm.SelectedCategory = vm.Categories[0];
        await vm.AddNewItemInCurrentCategoryCommand.ExecuteAsync(null);
        vm.SelectedItem!.Name = "青云山初会";
        vm.SelectedItem.Goal = "主角拜师";

        await vm.UpdateSelectedItemCommand.ExecuteAsync(null);
        await vm.LoadAsync();

        Assert.Contains(vm.Items, i => i.Name == "青云山初会" && i.Goal == "主角拜师");
    }

    private static (PlotRulesViewModel vm, string root) NewVm()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-pr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var schema = new PlotRulesSchema();
        var adapter = new ModuleDataAdapter<PlotRulesCategory, PlotRulesData>(schema, root);
        return (new PlotRulesViewModel(adapter), root);
    }
}
```

#### Step 9.2：跑测试验证失败

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter PlotRulesViewModelTests -v q`

Expected: FAIL with "PlotRulesViewModel not found"。

#### Step 9.3：实装 VM

`src/Tianming.Desktop.Avalonia/ViewModels/Design/PlotRulesViewModel.cs`：

```csharp
using TM.Services.Modules.ProjectData.Models.Design.Plot;
using TM.Services.Modules.ProjectData.Modules.Design.PlotRules;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels.Design;

public sealed partial class PlotRulesViewModel
    : DataManagementViewModel<PlotRulesCategory, PlotRulesData, PlotRulesSchema>
{
    public PlotRulesViewModel(ModuleDataAdapter<PlotRulesCategory, PlotRulesData> adapter)
        : base(adapter) { }
}
```

#### Step 9.4：跑测试 + commit

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter PlotRulesViewModelTests -v q`

Expected: 3 测试 PASS。

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Design/PlotRulesViewModel.cs \
        tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Design/PlotRulesViewModelTests.cs
git commit -m "feat(design): PlotRulesViewModel + 3 测试"
```

---

### Task 10：CreativeMaterialsViewModel + 测试

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Design/CreativeMaterialsViewModel.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Design/CreativeMaterialsViewModelTests.cs`

#### Step 10.1：写测试

`tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Design/CreativeMaterialsViewModelTests.cs`：

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Design.Templates;
using TM.Services.Modules.ProjectData.Modules.Design.CreativeMaterials;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels.Design;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Design;

public class CreativeMaterialsViewModelTests
{
    [Fact]
    public void PageTitle_is_创意素材库()
    {
        var (vm, _) = NewVm();
        Assert.Equal("创意素材库", vm.PageTitle);
    }

    [Fact]
    public async Task AddCategory_then_AddItem_persists()
    {
        var (vm, _) = NewVm();
        await vm.LoadAsync();
        await vm.AddCategoryAsync("玄幻");
        vm.SelectedCategory = vm.Categories[0];
        await vm.AddNewItemInCurrentCategoryCommand.ExecuteAsync(null);
        vm.SelectedItem!.Name = "仙侠开篇模板";

        await vm.UpdateSelectedItemCommand.ExecuteAsync(null);
        await vm.LoadAsync();

        Assert.Contains(vm.Items, i => i.Name == "仙侠开篇模板");
    }

    [Fact]
    public async Task CreateNewItem_assigns_default_icon()
    {
        var (vm, _) = NewVm();
        await vm.LoadAsync();
        await vm.AddCategoryAsync("玄幻");
        vm.SelectedCategory = vm.Categories[0];

        await vm.AddNewItemInCurrentCategoryCommand.ExecuteAsync(null);

        Assert.NotNull(vm.SelectedItem);
        Assert.Equal("💡", vm.SelectedItem!.Icon);
    }

    private static (CreativeMaterialsViewModel vm, string root) NewVm()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-cm-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var schema = new CreativeMaterialsSchema();
        var adapter = new ModuleDataAdapter<CreativeMaterialCategory, CreativeMaterialData>(schema, root);
        return (new CreativeMaterialsViewModel(adapter), root);
    }
}
```

#### Step 10.2：跑测试验证失败

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter CreativeMaterialsViewModelTests -v q`

Expected: FAIL with "CreativeMaterialsViewModel not found"。

#### Step 10.3：实装 VM

`src/Tianming.Desktop.Avalonia/ViewModels/Design/CreativeMaterialsViewModel.cs`：

```csharp
using TM.Services.Modules.ProjectData.Models.Design.Templates;
using TM.Services.Modules.ProjectData.Modules.Design.CreativeMaterials;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels.Design;

public sealed partial class CreativeMaterialsViewModel
    : DataManagementViewModel<CreativeMaterialCategory, CreativeMaterialData, CreativeMaterialsSchema>
{
    public CreativeMaterialsViewModel(ModuleDataAdapter<CreativeMaterialCategory, CreativeMaterialData> adapter)
        : base(adapter) { }
}
```

#### Step 10.4：跑测试 + commit

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter CreativeMaterialsViewModelTests -v q`

Expected: 3 测试 PASS。

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Design/CreativeMaterialsViewModel.cs \
        tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Design/CreativeMaterialsViewModelTests.cs
git commit -m "feat(design): CreativeMaterialsViewModel + 3 测试"
```

---

### Task 11：DesignModulePage view（6 页共用一个 view）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Views/Design/DesignModulePage.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Views/Design/DesignModulePage.axaml.cs`

> **设计决策**：6 页页面 axaml 完全等价（只是 DataContext = 不同的派生 VM），所以**只造一个 view**，叫 `DesignModulePage`，内部就是 `<controls:CategoryDataPageView/>`。在 App.axaml DataTemplate 把 6 个 VM 全部映射到这一个 view。

#### Step 11.1：实装 view

`src/Tianming.Desktop.Avalonia/Views/Design/DesignModulePage.axaml`：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:Tianming.Desktop.Avalonia.Controls"
             x:Class="Tianming.Desktop.Avalonia.Views.Design.DesignModulePage"
             x:CompileBindings="False">
  <controls:CategoryDataPageView DataContext="{Binding}"/>
</UserControl>
```

`src/Tianming.Desktop.Avalonia/Views/Design/DesignModulePage.axaml.cs`：

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Tianming.Desktop.Avalonia.Views.Design;

public partial class DesignModulePage : UserControl
{
    public DesignModulePage()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

#### Step 11.2：跑 build

Run: `dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj -v q`

Expected: 0 Warning / 0 Error。

#### Step 11.3：commit

```bash
git add src/Tianming.Desktop.Avalonia/Views/Design/DesignModulePage.axaml \
        src/Tianming.Desktop.Avalonia/Views/Design/DesignModulePage.axaml.cs
git commit -m "feat(design): DesignModulePage view (6 设计页共用)"
```

---

### Task 12：DI 注册 6 schema / 6 adapter / 6 VM + 6 PageRegistry + ICurrentProjectService

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml` 追加 6 条 DataTemplate

#### Step 12.1：修改 AvaloniaShellServiceCollectionExtensions.cs

整段替换为（按顺序追加）：

```csharp
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TM.Framework.Appearance;
using TM.Services.Framework.AI.SemanticKernel;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Models.Design.Factions;
using TM.Services.Modules.ProjectData.Models.Design.Location;
using TM.Services.Modules.ProjectData.Models.Design.Plot;
using TM.Services.Modules.ProjectData.Models.Design.Templates;
using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Modules.Design.CharacterRules;
using TM.Services.Modules.ProjectData.Modules.Design.CreativeMaterials;
using TM.Services.Modules.ProjectData.Modules.Design.FactionRules;
using TM.Services.Modules.ProjectData.Modules.Design.LocationRules;
using TM.Services.Modules.ProjectData.Modules.Design.PlotRules;
using TM.Services.Modules.ProjectData.Modules.Design.WorldRules;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.Shell;
using Tianming.Desktop.Avalonia.Theme;
using Tianming.Desktop.Avalonia.ViewModels;
using Tianming.Desktop.Avalonia.ViewModels.Design;
using Tianming.Desktop.Avalonia.ViewModels.Shell;
using Tianming.Desktop.Avalonia.Views;
using Tianming.Desktop.Avalonia.Views.Design;
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
        s.AddSingleton<ICurrentProjectService, CurrentProjectService>();

        // M5：系统代理 → HttpClient 装配
        s.AddSingleton<AvaloniaSystemHttpProxy>();
        s.AddHttpClient("tianming")
            .ConfigurePrimaryHttpMessageHandler(sp => new System.Net.Http.SocketsHttpHandler
            {
                Proxy = sp.GetRequiredService<AvaloniaSystemHttpProxy>(),
                UseProxy = true,
            });
        s.AddSingleton<System.Net.Http.HttpClient>(sp =>
            sp.GetRequiredService<System.Net.Http.IHttpClientFactory>().CreateClient("tianming"));

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

        // Infra probes / runtime
        s.AddSingleton<IRuntimeInfoProvider, RuntimeInfoProvider>();
        s.AddSingleton<IBreadcrumbSource, NavigationBreadcrumbSource>();
        s.AddSingleton<IKeychainHealthProbe, KeychainHealthProbe>();
        s.AddSingleton<IOnnxHealthProbe>(_ => new OnnxHealthProbe(EmbeddingSettings.Default));

        // ViewModels
        s.AddSingleton<MainWindowViewModel>();
        s.AddSingleton<ThreeColumnLayoutViewModel>();
        s.AddSingleton<LeftNavViewModel>();
        s.AddSingleton<RightConversationViewModel>();
        s.AddSingleton<AppChromeViewModel>();
        s.AddSingleton<AppStatusBarViewModel>();
        s.AddTransient<WelcomeViewModel>();
        s.AddTransient<DashboardViewModel>();
        s.AddTransient<PlaceholderViewModel>();

        // M4.1 设计模块：6 schema (singleton) + 6 adapter (transient) + 6 VM (transient)
        s.AddSingleton<WorldRulesSchema>();
        s.AddSingleton<CharacterRulesSchema>();
        s.AddSingleton<FactionRulesSchema>();
        s.AddSingleton<LocationRulesSchema>();
        s.AddSingleton<PlotRulesSchema>();
        s.AddSingleton<CreativeMaterialsSchema>();

        s.AddTransient(sp => new ModuleDataAdapter<WorldRulesCategory, WorldRulesData>(
            sp.GetRequiredService<WorldRulesSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddTransient(sp => new ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData>(
            sp.GetRequiredService<CharacterRulesSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddTransient(sp => new ModuleDataAdapter<FactionRulesCategory, FactionRulesData>(
            sp.GetRequiredService<FactionRulesSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddTransient(sp => new ModuleDataAdapter<LocationRulesCategory, LocationRulesData>(
            sp.GetRequiredService<LocationRulesSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddTransient(sp => new ModuleDataAdapter<PlotRulesCategory, PlotRulesData>(
            sp.GetRequiredService<PlotRulesSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddTransient(sp => new ModuleDataAdapter<CreativeMaterialCategory, CreativeMaterialData>(
            sp.GetRequiredService<CreativeMaterialsSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));

        s.AddTransient<WorldRulesViewModel>();
        s.AddTransient<CharacterRulesViewModel>();
        s.AddTransient<FactionRulesViewModel>();
        s.AddTransient<LocationRulesViewModel>();
        s.AddTransient<PlotRulesViewModel>();
        s.AddTransient<CreativeMaterialsViewModel>();

        return s;
    }

    private static PageRegistry RegisterPages(PageRegistry reg)
    {
        reg.Register<WelcomeViewModel,     WelcomeView>(PageKeys.Welcome);
        reg.Register<DashboardViewModel,   DashboardView>(PageKeys.Dashboard);
        reg.Register<PlaceholderViewModel, PlaceholderView>(PageKeys.Settings);

        // M4.1：6 设计页（VM 不同，View 全部用 DesignModulePage）
        reg.Register<WorldRulesViewModel,        DesignModulePage>(PageKeys.DesignWorld);
        reg.Register<CharacterRulesViewModel,    DesignModulePage>(PageKeys.DesignCharacter);
        reg.Register<FactionRulesViewModel,      DesignModulePage>(PageKeys.DesignFaction);
        reg.Register<LocationRulesViewModel,     DesignModulePage>(PageKeys.DesignLocation);
        reg.Register<PlotRulesViewModel,         DesignModulePage>(PageKeys.DesignPlot);
        reg.Register<CreativeMaterialsViewModel, DesignModulePage>(PageKeys.DesignMaterials);
        return reg;
    }
}
```

#### Step 12.2：修改 App.axaml 追加 6 个 DataTemplate

在 `App.axaml` 的 `<Application.DataTemplates>` 节追加（在 `vshell:RightConversationViewModel` DataTemplate 之后）：

```xml
    <!-- M4.1 设计模块 6 VM → DesignModulePage（共用 view） -->
    <DataTemplate DataType="vmd:WorldRulesViewModel">
      <vd:DesignModulePage/>
    </DataTemplate>
    <DataTemplate DataType="vmd:CharacterRulesViewModel">
      <vd:DesignModulePage/>
    </DataTemplate>
    <DataTemplate DataType="vmd:FactionRulesViewModel">
      <vd:DesignModulePage/>
    </DataTemplate>
    <DataTemplate DataType="vmd:LocationRulesViewModel">
      <vd:DesignModulePage/>
    </DataTemplate>
    <DataTemplate DataType="vmd:PlotRulesViewModel">
      <vd:DesignModulePage/>
    </DataTemplate>
    <DataTemplate DataType="vmd:CreativeMaterialsViewModel">
      <vd:DesignModulePage/>
    </DataTemplate>
```

并在 `<Application ...>` 根标签里追加命名空间：

```xml
             xmlns:vmd="using:Tianming.Desktop.Avalonia.ViewModels.Design"
             xmlns:vd="using:Tianming.Desktop.Avalonia.Views.Design"
```

#### Step 12.3：跑 build + test

Run: `dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj -v q && dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --no-build -v q`

Expected: build 0/0；test 全过。

#### Step 12.4：commit

```bash
git add src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs \
        src/Tianming.Desktop.Avalonia/App.axaml
git commit -m "feat(di): M4.1 6 schema/adapter/VM + PageRegistry + DataTemplate 注册"
```

---

### Task 13：LeftNav 启用 6 项设计页

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/Shell/LeftNavViewModel.cs`

#### Step 13.1：实装

替换 `LeftNavViewModel.cs` 的 `Groups.Add(new NavRailGroup("写作", ...))` 调用为：

```csharp
        Groups.Add(new NavRailGroup("写作", new List<NavRailItem>
        {
            new(PageKeys.Welcome,   "欢迎",     "home"),
            new(PageKeys.Dashboard, "仪表盘",   "layout-dashboard"),
        }));

        Groups.Add(new NavRailGroup("设计", new List<NavRailItem>
        {
            new(PageKeys.DesignWorld,     "世界观", "🌍"),
            new(PageKeys.DesignCharacter, "角色",   "👤"),
            new(PageKeys.DesignFaction,   "势力",   "⚔️"),
            new(PageKeys.DesignLocation,  "地点",   "📍"),
            new(PageKeys.DesignPlot,      "剧情",   "📖"),
            new(PageKeys.DesignMaterials, "创意素材", "💡"),
        }));

        Groups.Add(new NavRailGroup("工具", new List<NavRailItem>
        {
            new(new PageKey("conversation"),"AI 对话", "message-square", IsEnabled: false),
            new(new PageKey("validation"),  "校验",   "shield-check",   IsEnabled: false),
            new(new PageKey("packaging"),   "打包",   "package",        IsEnabled: false),
            new(PageKeys.Settings,          "设置",   "settings"),
        }));
```

> 注：去掉原有的 4 个 IsEnabled=false 占位项（草稿/大纲/角色/世界观）。设计组用 emoji icon glyph（长度 ≤ 2，IconGlyphIsShortConverter 会显示）。

#### Step 13.2：跑 build

Run: `dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj -v q`

Expected: 0 Warning / 0 Error。

#### Step 13.3：commit

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Shell/LeftNavViewModel.cs
git commit -m "feat(left-nav): 启用 M4.1 设计模块 6 项导航"
```

---

### Task 14：冒烟启动 + 全测试验收

**Files:** 无修改，仅运行验证

#### Step 14.1：build all + run all tests

Run:
```bash
dotnet build Tianming.MacMigration.sln -v q
dotnet test Tianming.MacMigration.sln --no-build -v q
```

Expected:
- build 0/0
- test 通过总数 ≈ 1312 baseline + 3 (DataManagementViewModel cmds) + 2 (CurrentProjectService) + 4 (World) + 3 (Character) + 3 (Faction) + 3 (Location) + 3 (Plot) + 3 (CreativeMaterials) = **1336+** 全过

#### Step 14.2：dotnet run 冒烟

Run（后台 5-10s 后 kill）：

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer/.worktrees/m4-1
dotnet run --project src/Tianming.Desktop.Avalonia -c Release > /tmp/m4-1-smoke.log 2>&1 &
PID=$!
sleep 8
kill $PID 2>/dev/null || true
grep -i "exception\|error" /tmp/m4-1-smoke.log | grep -v "OnnxRuntimeException\|HttpClientFactory" || echo "smoke OK"
```

Expected：
- 启动 8 秒无新 Exception（既有的 onnx / httpclient 探针 warning 可忽略）
- 切到设计页（手工，启动后人工点）能渲染左/中/右三栏布局

#### Step 14.3：人工冒烟（可选，由 controller 执行）

启动 app（GUI 内）：
1. 进入 Dashboard
2. 点击左栏 "设计 > 世界观" → 期望进入 CategoryDataPageView 三栏
3. 点 "+ 新建分类" → 列表出现 "新分类 1"
4. 选中分类，点 "+ 在当前分类新建" → 中栏列表出现 "新条目"
5. 在右栏表单填 PowerSystem，点 "保存" → 无崩
6. 切到 "设计 > 角色" → schema 切换，字段变为 20 项 (含 CharacterType ComboBox)
7. 切到 "设计 > 地点" → 字段变为 11 项 (含 Landmarks/Resources/Dangers Tags 输入)
8. 关闭 app → 重开 → 数据恢复

#### Step 14.4：commit (none — verification step only)

无 commit。

---

## Self-Review Checklist

### Spec coverage

- [x] M4.1 spec Task M4.1.1 "共享 CategoryDataPageView + 基类 VM" → Task 1 + Task 3
- [x] M4.1 spec Task M4.1.2 "六个 Rules 页面" → Task 5-10
- [x] M4.1 spec "PageKey 注册" → Task 4 + Task 12
- [x] M4.1 spec "DI 注册 6 adapter" → Task 12
- [x] M4.1 spec "LeftNav 启用 6 项" → Task 13
- [x] Mac_UI/03 pseudocode "LeftTree + MainForm + Footer" → CategoryDataPageView 三栏 + Save/Delete 按钮（Footer 简化为页面顶部按钮）
- [x] M4.1 Gate "硬子闭环：录入 6 个 data.json 并重开恢复" → Task 14 step 14.3 人工冒烟覆盖

### Placeholder scan

- [x] 无 "TODO/FIXME/implement later" 在新增代码（FieldRowBuilder code-behind 完整给出，CategoryDataPageView axaml 完整给出）
- [x] 每个 step 都给出完整代码块（无 "类似 Task N" 引用）
- [x] 命令名经源生成器规则验证：`DeleteSelectedItem` 方法 → `DeleteSelectedItemCommand` 属性
- [x] 反射 binding 路径完整（DataContext = SelectedItem, Binding(field.PropertyName)）
- [x] 6 个 schema 类名与 csproj link 名一致（已 grep 实测）

### Type consistency

- [x] `IModuleSchema<TCategory, TData>` 没有 `Save` 方法 — adapter 内部 `_store.AddDataAsync` 已处理持久化（基类 `UpdateAsync` 调 adapter.UpdateAsync）
- [x] `DataManagementViewModel<TCategory, TData, TSchema>` 3 个类型参数；6 个派生 VM 均显式给出 3 个实参（不省 TSchema）
- [x] `WorldRulesCategory` 等 6 个 Category 类型已通过 M4.0 csproj link 进入 Tianming.ProjectData 程序集；引用即可
- [x] `CreativeMaterialsSchema` 用的是 `CreativeMaterialCategory` / `CreativeMaterialData`（单数；非 "Materials"）；plan Task 10 中已使用单数
- [x] `IsEnabled` / `Icon` / `Name` 都是 `ICategory` 接口公开成员；XAML `{Binding Icon}` / `{Binding Name}` 合法
- [x] CommunityToolkit.Mvvm `[RelayCommand]` 命名规则：`private async Task DeleteSelectedItem()` → `DeleteSelectedItemCommand` 属性（不带 Async 后缀；同步方法亦同）
- [x] `AddCategoryAsync(string)` 与基类已有 `public async Task<bool> AddCategoryAsync(string name)` 重名 — `[RelayCommand] private async Task AddCategoryWithDefaultName()` 用不同方法名避免源生成器命名冲突
- [x] `App.axaml` xmlns 新增 `vmd:` `vd:` 命名空间一致引用 Design 子命名空间

### Step size

- [x] 每个 Task = 1 commit；总计 14 task = 14 commit + 1 plan commit
- [x] 单 Task 内最多 4 step；每 step 单一动作（写测试 / 验证失败 / 实装 / 验证通过）
- [x] 6 个 VM Task 形式完全同构，可在 subagent-driven-development 模式下并行派发

### Tests

- [x] DataManagementViewModel 命令包装：3 个新测试
- [x] CurrentProjectService：2 个新测试
- [x] WorldRulesViewModel：4 个
- [x] CharacterRulesViewModel：3 个
- [x] FactionRulesViewModel：3 个
- [x] LocationRulesViewModel：3 个（含 Tags 字段断言）
- [x] PlotRulesViewModel：3 个
- [x] CreativeMaterialsViewModel：3 个
- [x] 合计新增 **24 测试**；baseline 1312 → 预期 1336 全过
- [x] 不破坏既有任何测试（DataManagementViewModelTests 只追加，未修改既有断言）

### 不引新 NuGet

- [x] CommunityToolkit.Mvvm / Avalonia 11.0.10 / Microsoft.Extensions.DI 都已存在
- [x] 反射 binding 用 `Avalonia.Data.Binding` 内置类型，无新引用
- [x] 6 个 schema 用的 namespace 都在 Tianming.ProjectData 程序集（已 reference）

### 不动 M4.0 接口

- [x] `DataManagementViewModel.Commands.cs` 是 partial 文件**追加**到既有 `partial class`，不改既有方法签名
- [x] `IModuleSchema` 接口不改
- [x] `ModuleDataAdapter` 不改
- [x] Schema POCO 不改

---

## Commit 序列

1. `docs(plan): M4.1 设计模块 6 页 step-level plan`（Phase 1 完成时）
2. `feat(avalonia): DataManagementViewModel 命令包装 (RelayCommand partial)`
3. `feat(avalonia): ICurrentProjectService 占位实现 (M4 后续接 FileProjectManager)`
4. `feat(controls): CategoryDataPageView 三栏共享控件 (M4.1 + M4.2 复用)`
5. `feat(nav): M4.1 design.* 6 PageKey 入仓`
6. `feat(design): WorldRulesViewModel + 4 测试`
7. `feat(design): CharacterRulesViewModel + 3 测试`
8. `feat(design): FactionRulesViewModel + 3 测试`
9. `feat(design): LocationRulesViewModel + 3 测试 (含 Tags 字段验证)`
10. `feat(design): PlotRulesViewModel + 3 测试`
11. `feat(design): CreativeMaterialsViewModel + 3 测试`
12. `feat(design): DesignModulePage view (6 设计页共用)`
13. `feat(di): M4.1 6 schema/adapter/VM + PageRegistry + DataTemplate 注册`
14. `feat(left-nav): 启用 M4.1 设计模块 6 项导航`

总 1 plan + 13 impl commit。
