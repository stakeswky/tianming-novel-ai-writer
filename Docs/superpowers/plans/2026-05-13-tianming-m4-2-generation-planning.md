# M4.2 生成规划 4 schema 页 + 1 ChapterPipelinePage（step-level plan）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 在 M4.1 schema-driven adapter / `CategoryDataPageView` / `DataManagementViewModel<,,>` 基线之上，落地生成规划 4 个 schema 页（战略大纲 / 分卷设计 / 章节规划 / 章节蓝图）+ 1 个 `ChapterPipelinePage` 静态布局（5 列信息区，M4.4/M6 接真逻辑）。顺手修 M4.1 遗留的 NumericTextBox 没有 int↔string 转换器的短板（Generate 4 schema 共 8 个 Number 字段，不修无法落盘）。

**Architecture:** 完全复刻 M4.1 形态——4 个一行派生 VM（继承 `DataManagementViewModel<TCategory, TData, TSchema>`）+ 共用 `DesignModulePage` view + 5 PageKey + DI 注册 4 schema/adapter/VM + DataTemplate 5 条 + LeftNav 新增"生成"组 5 项。`CategoryDataPageView` code-behind 的 `CreateNumericTextBox` 注入 `NumberStringConverter`（int↔string TwoWay；空字符串→0；非法字符串保留旧值）。`ChapterPipelinePage` 是独立 view + thin VM（构造时填 mock 数据列表），按 Mac_UI/06 草图横向 5 列布局（章节选择 / Fact Snapshot 预览 / 生成节奏 step list / CHANGES 预览占位 / 操作历史占位），所有交互按钮 `IsEnabled=False` 并打 "M4.4 接入" badge。

**Tech Stack:** Avalonia 11.0.10 / CommunityToolkit.Mvvm 8.2.2 / Microsoft.Extensions.DI 8.0.1 / xUnit 2.9.2。不引新 NuGet。

---

## Scope Alignment（Explore 输出 — 已实测确认）

### M4.1 已就绪 API（Lane B 已 commit 到 m4.2 base）

- **`DataManagementViewModel<TCategory, TData, TSchema>`** 基类公开：`Schema` / `PageTitle` / `PageIcon` / `Fields` / `Categories` / `Items` / `SelectedCategory` / `SelectedItem` / `IsLoading` / `LoadAsync` / `AddCategoryAsync(string)` / `AddNewItemAsync(string, string)` / `UpdateSelectedItemAsync` / `DeleteSelectedItemAsync` / `CascadeDeleteCategoryAsync(string)` / `ItemsInSelectedCategory()`；`partial` 文件 `DataManagementViewModel.Commands.cs` 加了 `DeleteSelectedItemCommand` / `UpdateSelectedItemCommand` / `AddNewItemInCurrentCategoryCommand` / `AddCategoryWithDefaultNameCommand` 4 个 RelayCommand。
- **`IModuleSchema<TCategory, TData>`**：`PageTitle` / `PageIcon` / `ModuleRelativePath` / `Fields` (IReadOnlyList<FieldDescriptor>) / `CreateNewItem()` / `CreateNewCategory(string)` / `BuildAIPromptContext(IReadOnlyList<TData>)`
- **`FieldType` 枚举** = SingleLineText / MultiLineText / Number / Tags / Enum / Boolean；`FieldDescriptor` 是 record。
- **`ModuleDataAdapter<TCategory, TData>`** ctor: `(IModuleSchema<TCategory, TData>, string projectRoot)`
- **`CategoryDataPageView`** code-behind 反射构建表单：`CreateNumericTextBox` 当前**无 converter**，对 `int` 属性绑定 TextBox.Text 会触发 Avalonia 默认 string→int 反向（但没正向 int→string，TextBox 显示空），且 LostFocus 时把空字符串塞回 int 属性会抛 FormatException——**M4.1 没踩到是因为 6 个 Design schema 0 个 Number 字段**；M4.2 共有 8 个 Number 字段（见下）。
- **`DesignModulePage`** axaml = 简单 wrapper `<controls:CategoryDataPageView DataContext="{Binding}"/>`；可直接复用作 M4.2 generation 页的 view（VM 不同，view 共用即可）。

### M4.0 已就绪的 4 个 Generation Schema（已 grep 实测）

| Schema | Module Namespace | ModuleRelativePath | Fields 数 | Number 字段 |
|---|---|---|---|---|
| `OutlineSchema` | `TM.Services.Modules.ProjectData.Modules.Generate.Outline` | `Generate/StrategicOutline` | 11 | TotalChapterCount (1) |
| `VolumeDesignSchema` | `TM.Services.Modules.ProjectData.Modules.Generate.VolumeDesign` | `Generate/VolumeDesign` | 20 | VolumeNumber / TargetChapterCount / StartChapter / EndChapter (4) |
| `ChapterPlanningSchema` | `TM.Services.Modules.ProjectData.Modules.Generate.ChapterPlanning` | `Generate/ChapterPlanning` | 18 | ChapterNumber (1) |
| `BlueprintSchema` | `TM.Services.Modules.ProjectData.Modules.Generate.Blueprint` | `Generate/ChapterBlueprint` | 17 | SceneNumber (1) |

POCO Category/Data 类型对照：
- `OutlineCategory` / `OutlineData` → namespace `TM.Services.Modules.ProjectData.Models.Generate.StrategicOutline`
- `VolumeDesignCategory` / `VolumeDesignData` → namespace `TM.Services.Modules.ProjectData.Models.Generate.VolumeDesign`
- `ChapterCategory` / `ChapterData` → namespace `TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning`
- `BlueprintCategory` / `BlueprintData` → namespace `TM.Services.Modules.ProjectData.Models.Generate.ChapterBlueprint`

> 注意：`BlueprintData` 的 `Cast/Locations/Factions/ItemsClues` schema 用 `FieldType.MultiLineText`（"ID 列表，逗号分隔"），不是 `Tags`——POCO 已经是 `string` 类型，**避免** Tags 转换器把字符串变 `List<string>` 导致类型不匹配。这是 schema 故意的折中（M4 阶段），直接复用即可。

### Mac_UI/04 设计要点（结合 pseudocode 04）

- 4 个 schema 页通用 = 三栏 CategoryDataPageView（左分类树 / 中条目列表 / 右动态表单）——视觉与 03 设计模块完全一致，**不需要造新 view**。
- 04 pseudocode 列了 "tabs([大纲, 卷设计, 章节规划, 内容配置])"——M4.2 用 LeftNav 子项替代 tabs（与设计组同构），符合"个人自用减包装"。
- 04 pseudocode 还提 "一键成书 button (disabledUntil M6.8)" 和 "OneClickProgressPanel"——本次跳过（M6.8 范围，且天命已删 M6）。
- ContentConfig（pseudocode 04 第 4 tab）= 全书目标字数 / 风格 / 视角 / 章数；M4 blueprint Task M4.2.1 写的是"四页 = Outline / VolumeDesign / ChapterPlanning / ContentConfig"——但 M4.0 已固化的 schema 是 **Blueprint**（章节蓝图）而非 ContentConfig。决策：**用 BlueprintSchema 作为第 4 页**（M4.0 真值源），ContentConfig 留 M4.4/M4.5 再开（个人自用 2500 字/章 + 风格"悬疑"用默认值即可）；M4 blueprint 里的"内容配置"概念已被 BlueprintSchema.PacingCurve / EstimatedWordCount / ItemsClues 等字段事实覆盖。

### Mac_UI/06 章节生成管道设计要点

pseudocode 06 详尽列了 ChapterGenerationPipeline 真逻辑（PortableFactSnapshotExtractor / ChangesProtocolParser / GenerationGate / WAL recovery 等）。**本次 M4.2 scope 只做静态布局**：
- 横向 5 列：① 启用章节（ListBox 选章）② 准备清单（Fact Snapshot 预览，mock）③ 生成节奏（step list "FactSnapshot / 生成 / Humanize / CHANGES / Gate / WAL / 保存"）④ CHANGES 预览（占位 TextBlock）⑤ 操作历史（占位 ListBox）
- 所有交互按钮（"开始生成 / 应用到章节"）`IsEnabled="False"` + 提示 badge "M4.4 接入"
- VM 只持有列名常量 + 一个 mock chapter 列表（["第 1 章 风起", "第 2 章 相遇", "第 3 章 决意"]）便于冒烟看到列表非空。

### Baseline

- `git log --oneline -5`：m4.2/generation-planning 分支基于 m4-2 worktree commit 17bd0fb（含 M4.1 全部 13 commit + Dashboard fix + M5 + M4.0）
- `dotnet build Tianming.MacMigration.sln`：0/0
- `dotnet test`：1336 全过（ProjectData 273 + AI 144 + Avalonia 128 + Framework 791）

> **注**：Avalonia tests 128 = M4.1 后真实数；M4.1 plan 里口算 104 偏低，实测当前是 128（M4.1 加了 24 个）。本 plan 以 **1336** 为真实 baseline。

---

## File Structure

```
src/Tianming.Desktop.Avalonia/
├── Controls/
│   ├── DynamicFieldConverters.cs                    # ADD NumberStringConverter (int↔string)（MODIFY）
│   └── CategoryDataPageView.axaml.cs                # CreateNumericTextBox 注入 NumberStringConverter（MODIFY）
├── ViewModels/
│   └── Generate/
│       ├── OutlineViewModel.cs                      # NEW
│       ├── VolumeDesignViewModel.cs                 # NEW
│       ├── ChapterPlanningViewModel.cs              # NEW
│       ├── BlueprintViewModel.cs                    # NEW
│       └── ChapterPipelineViewModel.cs              # NEW (含 mock chapters)
├── Views/
│   └── Generate/
│       ├── (无 schema 页 axaml — 复用 Design/DesignModulePage.axaml)
│       └── ChapterPipelinePage.axaml(.cs)           # NEW 独立 5 列布局
├── Navigation/
│   └── PageKeys.cs                                  # 追加 5 个 generate.* PageKey（MODIFY）
├── ViewModels/Shell/
│   └── LeftNavViewModel.cs                          # 新增"生成"NavRailGroup 5 项（MODIFY）
├── App.axaml                                        # 追加 5 条 DataTemplate + xmlns vmg/vg（MODIFY）
└── AvaloniaShellServiceCollectionExtensions.cs      # 注册 4 schema/adapter/VM + ChapterPipelineVM + 5 PageRegistry（MODIFY）

tests/Tianming.Desktop.Avalonia.Tests/
├── Controls/
│   └── NumberStringConverterTests.cs                # NEW（4 测试 — int↔string roundtrip / 空串 / 非法值 / 0）
└── ViewModels/Generate/
    ├── OutlineViewModelTests.cs                     # NEW（≥3）
    ├── VolumeDesignViewModelTests.cs                # NEW（≥3 含 Number 字段往返断言）
    ├── ChapterPlanningViewModelTests.cs             # NEW（≥3）
    ├── BlueprintViewModelTests.cs                   # NEW（≥3）
    └── ChapterPipelineViewModelTests.cs             # NEW（≥3 smoke：标题 / mock 章节 / 5 列名）
```

**职责单一性：**
- 4 个 schema VM 完全 thin shell（ctor + base 调用）—— M4.1 已验证
- `NumberStringConverter` 只做 int↔string 双向，不动其他 FieldType
- `ChapterPipelineViewModel` 不依赖 ChapterGenerationPipeline 真服务（M4.4 接），只持 mock 列表 + 列名常量
- `ChapterPipelinePage` 是独立 view 不和 CategoryDataPageView 复用——5 列布局形态不同

---

## 任务步骤

### Task 1：NumberStringConverter + CategoryDataPageView 修复

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/Controls/DynamicFieldConverters.cs`
- Modify: `src/Tianming.Desktop.Avalonia/Controls/CategoryDataPageView.axaml.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/Controls/NumberStringConverterTests.cs`

#### Step 1.1：写 converter 测试

`tests/Tianming.Desktop.Avalonia.Tests/Controls/NumberStringConverterTests.cs`：

```csharp
using System.Globalization;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class NumberStringConverterTests
{
    [Fact]
    public void Convert_int_to_string_roundtrips()
    {
        var c = NumberStringConverter.Instance;
        Assert.Equal("42", c.Convert(42, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Convert_zero_renders_as_empty_string()
    {
        // 0 默认值显示为空串，避免新建项目时大量 "0" 显眼
        var c = NumberStringConverter.Instance;
        Assert.Equal(string.Empty, c.Convert(0, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_empty_or_whitespace_returns_zero()
    {
        var c = NumberStringConverter.Instance;
        Assert.Equal(0, c.ConvertBack(string.Empty, typeof(int), null, CultureInfo.InvariantCulture));
        Assert.Equal(0, c.ConvertBack("   ",       typeof(int), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_invalid_string_returns_zero_not_throw()
    {
        var c = NumberStringConverter.Instance;
        // 不抛 FormatException——非法输入回退到 0；UI 用户体验比抛异常好
        Assert.Equal(0, c.ConvertBack("abc",  typeof(int), null, CultureInfo.InvariantCulture));
        Assert.Equal(0, c.ConvertBack("12.5", typeof(int), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_valid_int_string_parses()
    {
        var c = NumberStringConverter.Instance;
        Assert.Equal(123, c.ConvertBack("123", typeof(int), null, CultureInfo.InvariantCulture));
    }
}
```

#### Step 1.2：跑测试验证失败

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter NumberStringConverterTests -v q`

Expected: FAIL with "NumberStringConverter not found in namespace Tianming.Desktop.Avalonia.Controls"。

#### Step 1.3：实装 NumberStringConverter

在 `src/Tianming.Desktop.Avalonia/Controls/DynamicFieldConverters.cs` 末尾追加（保留现有 `TagsListStringConverter` / `FieldTypeEqualsConverter`）：

```csharp
/// <summary>int ↔ string 双向转换器（Number 字段 TextBox 用）。
/// 设计：0 显示为空串；非法字符串回退到 0 不抛。</summary>
public sealed class NumberStringConverter : IValueConverter
{
    public static readonly NumberStringConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null) return string.Empty;
        if (value is int i) return i == 0 ? string.Empty : i.ToString(culture);
        if (value is long l) return l == 0 ? string.Empty : l.ToString(culture);
        return value.ToString() ?? string.Empty;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string s || string.IsNullOrWhiteSpace(s)) return 0;
        return int.TryParse(s, NumberStyles.Integer, culture, out var n) ? n : 0;
    }
}
```

并在文件顶部 using 区追加（如尚未有）：

```csharp
using System.Globalization;
```

#### Step 1.4：跑测试验证通过

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter NumberStringConverterTests -v q`

Expected: 5 测试 PASS。

#### Step 1.5：CategoryDataPageView 注入 converter

在 `src/Tianming.Desktop.Avalonia/Controls/CategoryDataPageView.axaml.cs` 修改 `CreateNumericTextBox`：

Old:
```csharp
    private static TextBox CreateNumericTextBox(FieldDescriptor field, object? item)
    {
        // 简化：用 TextBox + 数字格式。NumericUpDown 在 Avalonia 11 行为略不一致，先 TextBox 凑合。
        var tb = new TextBox { Watermark = field.Placeholder ?? "0" };
        BindProperty(tb, TextBox.TextProperty, field.PropertyName, item, converter: null);
        return tb;
    }
```

New:
```csharp
    private static TextBox CreateNumericTextBox(FieldDescriptor field, object? item)
    {
        // M4.2 修复 M4.1 短板：Number 字段 POCO 是 int，需要 int↔string converter。
        var tb = new TextBox { Watermark = field.Placeholder ?? "0" };
        BindProperty(tb, TextBox.TextProperty, field.PropertyName, item, converter: NumberStringConverter.Instance);
        return tb;
    }
```

#### Step 1.6：跑 build + 全测试不退化

Run:
```bash
dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj -v q
dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --no-build -v q
```

Expected: build 0/0；test 128 + 5 = **133** Avalonia 测试 PASS（全部）。

#### Step 1.7：commit

```bash
git add src/Tianming.Desktop.Avalonia/Controls/DynamicFieldConverters.cs \
        src/Tianming.Desktop.Avalonia/Controls/CategoryDataPageView.axaml.cs \
        tests/Tianming.Desktop.Avalonia.Tests/Controls/NumberStringConverterTests.cs
git commit -m "fix(controls): NumberStringConverter 修复 M4.1 Number 字段 int↔string 短板"
```

---

### Task 2：5 个 PageKey 新增

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs`

#### Step 2.1：实装

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

    // M4.2 生成规划（4 schema + 1 pipeline）
    public static readonly PageKey GenerateOutline   = new("generate.outline");
    public static readonly PageKey GenerateVolume    = new("generate.volume");
    public static readonly PageKey GenerateChapter   = new("generate.chapter");
    public static readonly PageKey GenerateBlueprint = new("generate.blueprint");
    public static readonly PageKey GeneratePipeline  = new("generate.pipeline");
}
```

#### Step 2.2：跑 build

Run: `dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj -v q`

Expected: 0 Warning / 0 Error。

#### Step 2.3：commit

```bash
git add src/Tianming.Desktop.Avalonia/Navigation/PageKeys.cs
git commit -m "feat(nav): M4.2 generate.* 5 PageKey 入仓"
```

---

### Task 3：OutlineViewModel + 测试

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Generate/OutlineViewModel.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Generate/OutlineViewModelTests.cs`

#### Step 3.1：写测试

`tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Generate/OutlineViewModelTests.cs`：

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Generate.StrategicOutline;
using TM.Services.Modules.ProjectData.Modules.Generate.Outline;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels.Generate;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Generate;

public class OutlineViewModelTests
{
    [Fact]
    public void PageTitle_and_icon_proxy_to_schema()
    {
        var (vm, _) = NewVm();
        Assert.Equal("战略大纲", vm.PageTitle);
        Assert.Equal("📖", vm.PageIcon);
    }

    [Fact]
    public void Fields_include_TotalChapterCount_as_Number()
    {
        var (vm, _) = NewVm();
        var f = vm.Fields.Single(x => x.PropertyName == "TotalChapterCount");
        Assert.Equal(FieldType.Number, f.Type);
    }

    [Fact]
    public async Task AddCategory_then_AddItem_then_set_TotalChapterCount_persists()
    {
        var (vm, _) = NewVm();
        await vm.LoadAsync();
        await vm.AddCategoryAsync("主线");
        vm.SelectedCategory = vm.Categories[0];
        await vm.AddNewItemInCurrentCategoryCommand.ExecuteAsync(null);
        vm.SelectedItem!.Name = "山河长安·正稿";
        vm.SelectedItem.TotalChapterCount = 60;
        vm.SelectedItem.OneLineOutline = "凡人逆天改命";

        await vm.UpdateSelectedItemCommand.ExecuteAsync(null);
        await vm.LoadAsync();

        var loaded = vm.Items.Single(i => i.Name == "山河长安·正稿");
        Assert.Equal(60, loaded.TotalChapterCount);
        Assert.Equal("凡人逆天改命", loaded.OneLineOutline);
    }

    private static (OutlineViewModel vm, string root) NewVm()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-out-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var schema = new OutlineSchema();
        var adapter = new ModuleDataAdapter<OutlineCategory, OutlineData>(schema, root);
        return (new OutlineViewModel(adapter), root);
    }
}
```

#### Step 3.2：跑测试验证失败

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter OutlineViewModelTests -v q`

Expected: FAIL with "OutlineViewModel not found"。

#### Step 3.3：实装 VM

`src/Tianming.Desktop.Avalonia/ViewModels/Generate/OutlineViewModel.cs`：

```csharp
using TM.Services.Modules.ProjectData.Models.Generate.StrategicOutline;
using TM.Services.Modules.ProjectData.Modules.Generate.Outline;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels;

namespace Tianming.Desktop.Avalonia.ViewModels.Generate;

public sealed partial class OutlineViewModel
    : DataManagementViewModel<OutlineCategory, OutlineData, OutlineSchema>
{
    public OutlineViewModel(ModuleDataAdapter<OutlineCategory, OutlineData> adapter)
        : base(adapter) { }
}
```

#### Step 3.4：跑测试 + commit

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter OutlineViewModelTests -v q`

Expected: 3 测试 PASS。

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Generate/OutlineViewModel.cs \
        tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Generate/OutlineViewModelTests.cs
git commit -m "feat(generate): OutlineViewModel + 3 测试"
```

---

### Task 4：VolumeDesignViewModel + 测试

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Generate/VolumeDesignViewModel.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Generate/VolumeDesignViewModelTests.cs`

#### Step 4.1：写测试

`tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Generate/VolumeDesignViewModelTests.cs`：

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Generate.VolumeDesign;
using TM.Services.Modules.ProjectData.Modules.Generate.VolumeDesign;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels.Generate;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Generate;

public class VolumeDesignViewModelTests
{
    [Fact]
    public void PageTitle_is_分卷设计()
    {
        var (vm, _) = NewVm();
        Assert.Equal("分卷设计", vm.PageTitle);
    }

    [Fact]
    public void Fields_include_4_Number_fields()
    {
        var (vm, _) = NewVm();
        var numbers = vm.Fields.Where(f => f.Type == FieldType.Number).Select(f => f.PropertyName).ToList();
        Assert.Equal(4, numbers.Count);
        Assert.Contains("VolumeNumber",       numbers);
        Assert.Contains("TargetChapterCount", numbers);
        Assert.Contains("StartChapter",       numbers);
        Assert.Contains("EndChapter",         numbers);
    }

    [Fact]
    public async Task AddVolume_with_Number_fields_round_trips()
    {
        var (vm, _) = NewVm();
        await vm.LoadAsync();
        await vm.AddCategoryAsync("正稿");
        vm.SelectedCategory = vm.Categories[0];
        await vm.AddNewItemInCurrentCategoryCommand.ExecuteAsync(null);
        vm.SelectedItem!.Name = "第一卷·起势";
        vm.SelectedItem.VolumeNumber = 1;
        vm.SelectedItem.TargetChapterCount = 20;
        vm.SelectedItem.StartChapter = 1;
        vm.SelectedItem.EndChapter = 20;
        vm.SelectedItem.StageGoal = "主角入门";

        await vm.UpdateSelectedItemCommand.ExecuteAsync(null);
        await vm.LoadAsync();

        var loaded = vm.Items.Single(i => i.Name == "第一卷·起势");
        Assert.Equal(1, loaded.VolumeNumber);
        Assert.Equal(20, loaded.TargetChapterCount);
        Assert.Equal(20, loaded.EndChapter);
        Assert.Equal("主角入门", loaded.StageGoal);
    }

    private static (VolumeDesignViewModel vm, string root) NewVm()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-vol-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var schema = new VolumeDesignSchema();
        var adapter = new ModuleDataAdapter<VolumeDesignCategory, VolumeDesignData>(schema, root);
        return (new VolumeDesignViewModel(adapter), root);
    }
}
```

#### Step 4.2：跑测试验证失败

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter VolumeDesignViewModelTests -v q`

Expected: FAIL with "VolumeDesignViewModel not found"。

#### Step 4.3：实装 VM

`src/Tianming.Desktop.Avalonia/ViewModels/Generate/VolumeDesignViewModel.cs`：

```csharp
using TM.Services.Modules.ProjectData.Models.Generate.VolumeDesign;
using TM.Services.Modules.ProjectData.Modules.Generate.VolumeDesign;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels;

namespace Tianming.Desktop.Avalonia.ViewModels.Generate;

public sealed partial class VolumeDesignViewModel
    : DataManagementViewModel<VolumeDesignCategory, VolumeDesignData, VolumeDesignSchema>
{
    public VolumeDesignViewModel(ModuleDataAdapter<VolumeDesignCategory, VolumeDesignData> adapter)
        : base(adapter) { }
}
```

#### Step 4.4：跑测试 + commit

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter VolumeDesignViewModelTests -v q`

Expected: 3 测试 PASS。

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Generate/VolumeDesignViewModel.cs \
        tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Generate/VolumeDesignViewModelTests.cs
git commit -m "feat(generate): VolumeDesignViewModel + 3 测试 (含 4 Number 字段往返断言)"
```

---

### Task 5：ChapterPlanningViewModel + 测试

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Generate/ChapterPlanningViewModel.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Generate/ChapterPlanningViewModelTests.cs`

#### Step 5.1：写测试

`tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Generate/ChapterPlanningViewModelTests.cs`：

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels.Generate;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Generate;

public class ChapterPlanningViewModelTests
{
    [Fact]
    public void PageTitle_is_章节规划()
    {
        var (vm, _) = NewVm();
        Assert.Equal("章节规划", vm.PageTitle);
    }

    [Fact]
    public void Fields_include_3_Tags_for_ReferencedNames()
    {
        var (vm, _) = NewVm();
        var tags = vm.Fields.Where(f => f.Type == FieldType.Tags).Select(f => f.PropertyName).ToList();
        Assert.Equal(3, tags.Count);
        Assert.Contains("ReferencedCharacterNames", tags);
        Assert.Contains("ReferencedFactionNames",   tags);
        Assert.Contains("ReferencedLocationNames",  tags);
    }

    [Fact]
    public async Task AddChapter_with_ChapterNumber_and_Tags_round_trips()
    {
        var (vm, _) = NewVm();
        await vm.LoadAsync();
        await vm.AddCategoryAsync("正稿");
        vm.SelectedCategory = vm.Categories[0];
        await vm.AddNewItemInCurrentCategoryCommand.ExecuteAsync(null);
        vm.SelectedItem!.Name = "第 1 章 风起青萍";
        vm.SelectedItem.ChapterNumber = 1;
        vm.SelectedItem.ChapterTitle = "风起青萍";
        vm.SelectedItem.MainGoal = "主角拜师";
        vm.SelectedItem.ReferencedCharacterNames = new() { "李无心", "张老师" };

        await vm.UpdateSelectedItemCommand.ExecuteAsync(null);
        await vm.LoadAsync();

        var loaded = vm.Items.Single(i => i.Name == "第 1 章 风起青萍");
        Assert.Equal(1, loaded.ChapterNumber);
        Assert.Equal("风起青萍", loaded.ChapterTitle);
        Assert.Equal(2, loaded.ReferencedCharacterNames.Count);
        Assert.Contains("李无心", loaded.ReferencedCharacterNames);
    }

    private static (ChapterPlanningViewModel vm, string root) NewVm()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-chp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var schema = new ChapterPlanningSchema();
        var adapter = new ModuleDataAdapter<ChapterCategory, ChapterData>(schema, root);
        return (new ChapterPlanningViewModel(adapter), root);
    }
}
```

#### Step 5.2：跑测试验证失败

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter ChapterPlanningViewModelTests -v q`

Expected: FAIL with "ChapterPlanningViewModel not found"。

#### Step 5.3：实装 VM

`src/Tianming.Desktop.Avalonia/ViewModels/Generate/ChapterPlanningViewModel.cs`：

```csharp
using TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels;

namespace Tianming.Desktop.Avalonia.ViewModels.Generate;

public sealed partial class ChapterPlanningViewModel
    : DataManagementViewModel<ChapterCategory, ChapterData, ChapterPlanningSchema>
{
    public ChapterPlanningViewModel(ModuleDataAdapter<ChapterCategory, ChapterData> adapter)
        : base(adapter) { }
}
```

#### Step 5.4：跑测试 + commit

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter ChapterPlanningViewModelTests -v q`

Expected: 3 测试 PASS。

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Generate/ChapterPlanningViewModel.cs \
        tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Generate/ChapterPlanningViewModelTests.cs
git commit -m "feat(generate): ChapterPlanningViewModel + 3 测试"
```

---

### Task 6：BlueprintViewModel + 测试

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Generate/BlueprintViewModel.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Generate/BlueprintViewModelTests.cs`

#### Step 6.1：写测试

`tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Generate/BlueprintViewModelTests.cs`：

```csharp
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterBlueprint;
using TM.Services.Modules.ProjectData.Modules.Generate.Blueprint;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels.Generate;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Generate;

public class BlueprintViewModelTests
{
    [Fact]
    public void PageTitle_is_章节蓝图()
    {
        var (vm, _) = NewVm();
        Assert.Equal("章节蓝图", vm.PageTitle);
    }

    [Fact]
    public void Fields_include_SceneNumber_as_Number()
    {
        var (vm, _) = NewVm();
        var f = vm.Fields.Single(x => x.PropertyName == "SceneNumber");
        Assert.Equal(FieldType.Number, f.Type);
    }

    [Fact]
    public async Task AddBlueprint_round_trips()
    {
        var (vm, _) = NewVm();
        await vm.LoadAsync();
        await vm.AddCategoryAsync("第 1 章");
        vm.SelectedCategory = vm.Categories[0];
        await vm.AddNewItemInCurrentCategoryCommand.ExecuteAsync(null);
        vm.SelectedItem!.Name = "场景 1·开场";
        vm.SelectedItem.SceneNumber = 1;
        vm.SelectedItem.SceneTitle = "开场";
        vm.SelectedItem.OneLineStructure = "突遭追杀 → 反击 → 拜师";
        vm.SelectedItem.Opening = "深夜小巷的脚步声";

        await vm.UpdateSelectedItemCommand.ExecuteAsync(null);
        await vm.LoadAsync();

        var loaded = vm.Items.Single(i => i.Name == "场景 1·开场");
        Assert.Equal(1, loaded.SceneNumber);
        Assert.Equal("开场", loaded.SceneTitle);
        Assert.Equal("突遭追杀 → 反击 → 拜师", loaded.OneLineStructure);
    }

    private static (BlueprintViewModel vm, string root) NewVm()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-bp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var schema = new BlueprintSchema();
        var adapter = new ModuleDataAdapter<BlueprintCategory, BlueprintData>(schema, root);
        return (new BlueprintViewModel(adapter), root);
    }
}
```

#### Step 6.2：跑测试验证失败

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter BlueprintViewModelTests -v q`

Expected: FAIL with "BlueprintViewModel not found"。

#### Step 6.3：实装 VM

`src/Tianming.Desktop.Avalonia/ViewModels/Generate/BlueprintViewModel.cs`：

```csharp
using TM.Services.Modules.ProjectData.Models.Generate.ChapterBlueprint;
using TM.Services.Modules.ProjectData.Modules.Generate.Blueprint;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels;

namespace Tianming.Desktop.Avalonia.ViewModels.Generate;

public sealed partial class BlueprintViewModel
    : DataManagementViewModel<BlueprintCategory, BlueprintData, BlueprintSchema>
{
    public BlueprintViewModel(ModuleDataAdapter<BlueprintCategory, BlueprintData> adapter)
        : base(adapter) { }
}
```

#### Step 6.4：跑测试 + commit

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter BlueprintViewModelTests -v q`

Expected: 3 测试 PASS。

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Generate/BlueprintViewModel.cs \
        tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Generate/BlueprintViewModelTests.cs
git commit -m "feat(generate): BlueprintViewModel + 3 测试"
```

---

### Task 7：ChapterPipelineViewModel + 测试

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/Generate/ChapterPipelineViewModel.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Generate/ChapterPipelineViewModelTests.cs`

> **设计决策**：本次只做静态布局 + mock 数据，VM 不构造注入 `ChapterGenerationPipeline`/`PortableFactSnapshotExtractor`（M4.4 接入）。VM 持有：①标题、②5 列名称、③`MockChapters` ObservableCollection（3 条）、④`SelectedChapter` 双向、⑤`GenerationSteps` 7 步常量列表（M6.x 接管时变成真实状态）。所有命令（"开始生成 / 应用"）这一阶段不实现——view 直接画 disabled button + "M4.4 接入" badge。

#### Step 7.1：写测试

`tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Generate/ChapterPipelineViewModelTests.cs`：

```csharp
using System.Linq;
using Tianming.Desktop.Avalonia.ViewModels.Generate;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Generate;

public class ChapterPipelineViewModelTests
{
    [Fact]
    public void PageTitle_is_章节生成管道()
    {
        var vm = new ChapterPipelineViewModel();
        Assert.Equal("章节生成管道", vm.PageTitle);
    }

    [Fact]
    public void MockChapters_has_three_items()
    {
        var vm = new ChapterPipelineViewModel();
        Assert.Equal(3, vm.MockChapters.Count);
        Assert.Contains(vm.MockChapters, c => c.Contains("第 1 章"));
    }

    [Fact]
    public void GenerationSteps_lists_seven_phases()
    {
        var vm = new ChapterPipelineViewModel();
        Assert.Equal(7, vm.GenerationSteps.Count);
        Assert.Equal("FactSnapshot",  vm.GenerationSteps[0]);
        Assert.Equal("生成",          vm.GenerationSteps[1]);
        Assert.Equal("Humanize",      vm.GenerationSteps[2]);
        Assert.Equal("CHANGES",       vm.GenerationSteps[3]);
        Assert.Equal("Gate",          vm.GenerationSteps[4]);
        Assert.Equal("WAL",           vm.GenerationSteps[5]);
        Assert.Equal("保存",          vm.GenerationSteps[6]);
    }

    [Fact]
    public void IsImplemented_is_false_for_M4_2()
    {
        // M4.2 阶段所有真实命令都禁用；view 用这个 flag 控 IsEnabled
        var vm = new ChapterPipelineViewModel();
        Assert.False(vm.IsPipelineImplemented);
    }
}
```

#### Step 7.2：跑测试验证失败

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter ChapterPipelineViewModelTests -v q`

Expected: FAIL with "ChapterPipelineViewModel not found"。

#### Step 7.3：实装 VM

`src/Tianming.Desktop.Avalonia/ViewModels/Generate/ChapterPipelineViewModel.cs`：

```csharp
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Tianming.Desktop.Avalonia.ViewModels.Generate;

/// <summary>
/// M4.2 章节生成管道页 VM。
/// 仅静态布局 + mock 数据；真实 ChapterGenerationPipeline / FactSnapshot / CHANGES / Gate
/// 等接入留给 M4.4（pipeline 串联）/ M6.x（Canonicalizer / Humanize / WAL）。
/// </summary>
public partial class ChapterPipelineViewModel : ObservableObject
{
    public string PageTitle => "章节生成管道";
    public string PageIcon  => "⚙️";

    /// <summary>M4.2 阶段是否已接入真实管道。view 用此控制按钮 IsEnabled / badge 显示。</summary>
    public bool IsPipelineImplemented => false;

    /// <summary>左侧"启用章节"列：mock 列表，M4.4 替换为 ChapterPlanning 数据。</summary>
    public ObservableCollection<string> MockChapters { get; } = new()
    {
        "第 1 章 风起青萍",
        "第 2 章 相遇",
        "第 3 章 决意",
    };

    [ObservableProperty]
    private string? _selectedChapter;

    /// <summary>生成节奏 step list（Mac_UI/06 中心列）。</summary>
    public IReadOnlyList<string> GenerationSteps { get; } = new[]
    {
        "FactSnapshot",
        "生成",
        "Humanize",
        "CHANGES",
        "Gate",
        "WAL",
        "保存",
    };

    /// <summary>5 个列标题（页面顶端横向卡片标题）。</summary>
    public IReadOnlyList<string> ColumnTitles { get; } = new[]
    {
        "① 启用章节",
        "② 准备清单",
        "③ 生成节奏",
        "④ CHANGES 预览",
        "⑤ 操作历史",
    };

    /// <summary>M4.4 接入提示文本。</summary>
    public string PipelineDisabledHint => "M4.4 串联 ChapterGenerationPipeline 后启用";
}
```

#### Step 7.4：跑测试 + commit

Run: `dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --filter ChapterPipelineViewModelTests -v q`

Expected: 4 测试 PASS。

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Generate/ChapterPipelineViewModel.cs \
        tests/Tianming.Desktop.Avalonia.Tests/ViewModels/Generate/ChapterPipelineViewModelTests.cs
git commit -m "feat(generate): ChapterPipelineViewModel + 4 测试 (静态布局 + mock 数据)"
```

---

### Task 8：ChapterPipelinePage view（独立 5 列布局）

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/Views/Generate/ChapterPipelinePage.axaml`
- Create: `src/Tianming.Desktop.Avalonia/Views/Generate/ChapterPipelinePage.axaml.cs`

#### Step 8.1：实装 axaml

`src/Tianming.Desktop.Avalonia/Views/Generate/ChapterPipelinePage.axaml`：

```xml
<UserControl xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:controls="using:Tianming.Desktop.Avalonia.Controls"
             x:Class="Tianming.Desktop.Avalonia.Views.Generate.ChapterPipelinePage"
             x:CompileBindings="False"
             FontFamily="{DynamicResource FontUI}">
  <DockPanel LastChildFill="True" Background="{DynamicResource SurfaceCanvasBrush}">
    <!-- 顶栏：标题 + M4.4 接入提示 badge -->
    <Border DockPanel.Dock="Top" Padding="20,16" Background="{DynamicResource SurfacePanelBrush}">
      <StackPanel Orientation="Horizontal" Spacing="12">
        <TextBlock Text="{Binding PageIcon}"
                   FontSize="{DynamicResource FontSizeH1}"
                   VerticalAlignment="Center"/>
        <TextBlock Text="{Binding PageTitle}"
                   FontSize="{DynamicResource FontSizeH1}"
                   FontWeight="{DynamicResource FontWeightSemibold}"
                   Foreground="{DynamicResource TextPrimaryBrush}"
                   VerticalAlignment="Center"/>
        <controls:BadgePill Text="{Binding PipelineDisabledHint}"
                            VerticalAlignment="Center"/>
      </StackPanel>
    </Border>

    <!-- 5 列布局 -->
    <Grid ColumnDefinitions="220, 1, *, 1, *, 1, *, 1, 220" Margin="12">

      <!-- ① 启用章节 -->
      <controls:SectionCard Grid.Column="0" Header="① 启用章节" Subtitle="选择待生成章节">
        <ListBox ItemsSource="{Binding MockChapters}"
                 SelectedItem="{Binding SelectedChapter, Mode=TwoWay}"
                 Background="Transparent"
                 BorderThickness="0">
          <ListBox.ItemTemplate>
            <DataTemplate>
              <TextBlock Text="{Binding}" Margin="0,4"/>
            </DataTemplate>
          </ListBox.ItemTemplate>
        </ListBox>
      </controls:SectionCard>

      <Border Grid.Column="1" Background="{DynamicResource BorderBrush}"/>

      <!-- ② 准备清单（Fact Snapshot 占位） -->
      <controls:SectionCard Grid.Column="2" Header="② 准备清单" Subtitle="Fact Snapshot · 上下文">
        <StackPanel Spacing="6">
          <TextBlock Text="世界观：（待选章后由 PortableFactSnapshotExtractor 抽取）"
                     Foreground="{DynamicResource TextSecondaryBrush}"
                     TextWrapping="Wrap"/>
          <TextBlock Text="角色：（M4.4 接入）"
                     Foreground="{DynamicResource TextSecondaryBrush}"
                     TextWrapping="Wrap"/>
          <TextBlock Text="地点：（M4.4 接入）"
                     Foreground="{DynamicResource TextSecondaryBrush}"
                     TextWrapping="Wrap"/>
          <TextBlock Text="大纲 / 章节规划 / 蓝图：（M4.4 接入）"
                     Foreground="{DynamicResource TextSecondaryBrush}"
                     TextWrapping="Wrap"/>
        </StackPanel>
      </controls:SectionCard>

      <Border Grid.Column="3" Background="{DynamicResource BorderBrush}"/>

      <!-- ③ 生成节奏（7 step list） -->
      <controls:SectionCard Grid.Column="4" Header="③ 生成节奏" Subtitle="7 阶段流水">
        <ItemsControl ItemsSource="{Binding GenerationSteps}">
          <ItemsControl.ItemTemplate>
            <DataTemplate>
              <Border Padding="8,6" Margin="0,2"
                      Background="{DynamicResource SurfaceCanvasBrush}"
                      CornerRadius="{DynamicResource RadiusSm}">
                <TextBlock Text="{Binding}"
                           Foreground="{DynamicResource TextPrimaryBrush}"/>
              </Border>
            </DataTemplate>
          </ItemsControl.ItemTemplate>
        </ItemsControl>
      </controls:SectionCard>

      <Border Grid.Column="5" Background="{DynamicResource BorderBrush}"/>

      <!-- ④ CHANGES 预览（占位） -->
      <controls:SectionCard Grid.Column="6" Header="④ CHANGES 预览" Subtitle="生成结果 diff">
        <TextBlock Text="（M4.4 接入 ChangesProtocolParser 后渲染差异）"
                   Foreground="{DynamicResource TextSecondaryBrush}"
                   TextWrapping="Wrap"
                   VerticalAlignment="Center"
                   HorizontalAlignment="Center"/>
      </controls:SectionCard>

      <Border Grid.Column="7" Background="{DynamicResource BorderBrush}"/>

      <!-- ⑤ 操作历史 -->
      <controls:SectionCard Grid.Column="8" Header="⑤ 操作历史" Subtitle="执行日志">
        <DockPanel LastChildFill="True">
          <StackPanel DockPanel.Dock="Bottom" Orientation="Vertical" Spacing="6" Margin="0,8,0,0">
            <Button Classes="primary"
                    Content="开始生成"
                    HorizontalAlignment="Stretch"
                    IsEnabled="{Binding IsPipelineImplemented}"/>
            <Button Classes="ghost"
                    Content="应用到章节"
                    HorizontalAlignment="Stretch"
                    IsEnabled="{Binding IsPipelineImplemented}"/>
          </StackPanel>
          <TextBlock Text="（暂无历史）"
                     Foreground="{DynamicResource TextTertiaryBrush}"
                     VerticalAlignment="Top"/>
        </DockPanel>
      </controls:SectionCard>

    </Grid>
  </DockPanel>
</UserControl>
```

#### Step 8.2：实装 code-behind

`src/Tianming.Desktop.Avalonia/Views/Generate/ChapterPipelinePage.axaml.cs`：

```csharp
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Tianming.Desktop.Avalonia.Views.Generate;

public partial class ChapterPipelinePage : UserControl
{
    public ChapterPipelinePage()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
```

#### Step 8.3：跑 build

Run: `dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj -v q`

Expected: 0 Warning / 0 Error。

#### Step 8.4：commit

```bash
git add src/Tianming.Desktop.Avalonia/Views/Generate/ChapterPipelinePage.axaml \
        src/Tianming.Desktop.Avalonia/Views/Generate/ChapterPipelinePage.axaml.cs
git commit -m "feat(generate): ChapterPipelinePage 静态 5 列布局 (M4.4 接入真逻辑)"
```

---

### Task 9：DI 注册 4 schema/adapter/VM + ChapterPipelineVM + 5 PageRegistry + App.axaml DataTemplate

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs`
- Modify: `src/Tianming.Desktop.Avalonia/App.axaml`

#### Step 9.1：修改 AvaloniaShellServiceCollectionExtensions.cs

在文件顶部 using 区追加：

```csharp
using TM.Services.Modules.ProjectData.Models.Generate.ChapterBlueprint;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Models.Generate.StrategicOutline;
using TM.Services.Modules.ProjectData.Models.Generate.VolumeDesign;
using TM.Services.Modules.ProjectData.Modules.Generate.Blueprint;
using TM.Services.Modules.ProjectData.Modules.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Generate.Outline;
using TM.Services.Modules.ProjectData.Modules.Generate.VolumeDesign;
using Tianming.Desktop.Avalonia.ViewModels.Generate;
using Tianming.Desktop.Avalonia.Views.Generate;
```

在 `AddAvaloniaShell` 的 "// M4.1 设计模块：6 schema..." 块**之后**追加：

```csharp
        // M4.2 生成规划：4 schema (singleton) + 4 adapter (transient) + 4 VM (transient) + ChapterPipelineVM (transient)
        s.AddSingleton<OutlineSchema>();
        s.AddSingleton<VolumeDesignSchema>();
        s.AddSingleton<ChapterPlanningSchema>();
        s.AddSingleton<BlueprintSchema>();

        s.AddTransient(sp => new ModuleDataAdapter<OutlineCategory, OutlineData>(
            sp.GetRequiredService<OutlineSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddTransient(sp => new ModuleDataAdapter<VolumeDesignCategory, VolumeDesignData>(
            sp.GetRequiredService<VolumeDesignSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddTransient(sp => new ModuleDataAdapter<ChapterCategory, ChapterData>(
            sp.GetRequiredService<ChapterPlanningSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));
        s.AddTransient(sp => new ModuleDataAdapter<BlueprintCategory, BlueprintData>(
            sp.GetRequiredService<BlueprintSchema>(),
            sp.GetRequiredService<ICurrentProjectService>().ProjectRoot));

        s.AddTransient<OutlineViewModel>();
        s.AddTransient<VolumeDesignViewModel>();
        s.AddTransient<ChapterPlanningViewModel>();
        s.AddTransient<BlueprintViewModel>();
        s.AddTransient<ChapterPipelineViewModel>();
```

在 `RegisterPages(PageRegistry reg)` 的 "// M4.1：6 设计页..." 块**之后**追加：

```csharp
        // M4.2：4 schema 页（VM 不同，View 全部复用 DesignModulePage）+ 1 ChapterPipelinePage（独立 view）
        reg.Register<OutlineViewModel,          DesignModulePage>(PageKeys.GenerateOutline);
        reg.Register<VolumeDesignViewModel,     DesignModulePage>(PageKeys.GenerateVolume);
        reg.Register<ChapterPlanningViewModel,  DesignModulePage>(PageKeys.GenerateChapter);
        reg.Register<BlueprintViewModel,        DesignModulePage>(PageKeys.GenerateBlueprint);
        reg.Register<ChapterPipelineViewModel,  ChapterPipelinePage>(PageKeys.GeneratePipeline);
```

> 注：4 schema 页复用 `DesignModulePage` view（Views/Design 命名空间）—— M4.1 已 import；ChapterPipelinePage 在 Views/Generate 命名空间 —— Step 9.1 顶部已加 using。

#### Step 9.2：修改 App.axaml 追加 5 个 DataTemplate + xmlns

在 `<Application ...>` 根标签 xmlns 列表中追加：

```xml
             xmlns:vmg="using:Tianming.Desktop.Avalonia.ViewModels.Generate"
             xmlns:vg="using:Tianming.Desktop.Avalonia.Views.Generate"
```

在 `<Application.DataTemplates>` 节内"M4.1 设计模块"6 条 DataTemplate **之后**追加：

```xml
    <!-- M4.2 生成规划 4 schema VM → DesignModulePage（共用 view） -->
    <DataTemplate DataType="vmg:OutlineViewModel">
      <vd:DesignModulePage/>
    </DataTemplate>
    <DataTemplate DataType="vmg:VolumeDesignViewModel">
      <vd:DesignModulePage/>
    </DataTemplate>
    <DataTemplate DataType="vmg:ChapterPlanningViewModel">
      <vd:DesignModulePage/>
    </DataTemplate>
    <DataTemplate DataType="vmg:BlueprintViewModel">
      <vd:DesignModulePage/>
    </DataTemplate>
    <!-- ChapterPipelinePage 独立 view -->
    <DataTemplate DataType="vmg:ChapterPipelineViewModel">
      <vg:ChapterPipelinePage/>
    </DataTemplate>
```

#### Step 9.3：跑 build + 全测试

Run:
```bash
dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj -v q
dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --no-build -v q
```

Expected: build 0/0；全部 Avalonia 测试 PASS（128 baseline + 5 NumberStringConverter + 3 Outline + 3 Volume + 3 Chapter + 3 Blueprint + 4 Pipeline = **149**）。

#### Step 9.4：commit

```bash
git add src/Tianming.Desktop.Avalonia/AvaloniaShellServiceCollectionExtensions.cs \
        src/Tianming.Desktop.Avalonia/App.axaml
git commit -m "feat(di): M4.2 4 schema/adapter/VM + ChapterPipelineVM + 5 PageRegistry + DataTemplate"
```

---

### Task 10：LeftNav 启用"生成"5 项

**Files:**
- Modify: `src/Tianming.Desktop.Avalonia/ViewModels/Shell/LeftNavViewModel.cs`

#### Step 10.1：实装

在 `LeftNavViewModel.cs` 的 `Groups.Add(new NavRailGroup("设计", ...))` 块**之后**追加（在 `Groups.Add(new NavRailGroup("工具", ...))` **之前**）：

```csharp
        Groups.Add(new NavRailGroup("生成", new List<NavRailItem>
        {
            new(PageKeys.GenerateOutline,   "战略大纲",   "📖"),
            new(PageKeys.GenerateVolume,    "分卷设计",   "📚"),
            new(PageKeys.GenerateChapter,   "章节规划",   "📑"),
            new(PageKeys.GenerateBlueprint, "章节蓝图",   "🎬"),
            new(PageKeys.GeneratePipeline,  "章节生成管道", "⚙️"),
        }));
```

> 注：emoji icon glyph 长度 ≤ 2（"⚙️" length 2），`IconGlyphIsShortConverter` 会显示。

#### Step 10.2：跑 build + 测试不退化

Run:
```bash
dotnet build src/Tianming.Desktop.Avalonia/Tianming.Desktop.Avalonia.csproj -v q
dotnet test tests/Tianming.Desktop.Avalonia.Tests/Tianming.Desktop.Avalonia.Tests.csproj --no-build -v q
```

Expected: build 0/0；test 全过。

#### Step 10.3：commit

```bash
git add src/Tianming.Desktop.Avalonia/ViewModels/Shell/LeftNavViewModel.cs
git commit -m "feat(left-nav): 启用 M4.2 生成规划 5 项导航"
```

---

### Task 11：冒烟启动 + 全测试验收

**Files:** 无修改，仅运行验证

#### Step 11.1：build all + run all tests

Run:
```bash
dotnet build Tianming.MacMigration.sln -v q
dotnet test Tianming.MacMigration.sln --no-build -v q
```

Expected:
- build 0 Warning / 0 Error
- test 通过总数 = 1336 baseline + 5 (NumberStringConverter) + 3 (Outline) + 3 (VolumeDesign) + 3 (ChapterPlanning) + 3 (Blueprint) + 4 (ChapterPipeline) = **1357** 全过

#### Step 11.2：dotnet run 冒烟

Run（后台 5-10s 后 kill）：

```bash
cd /Users/jimmy/Downloads/tianming-novel-ai-writer/.worktrees/m4-2
dotnet run --project src/Tianming.Desktop.Avalonia -c Release > /tmp/m4-2-smoke.log 2>&1 &
PID=$!
sleep 8
kill $PID 2>/dev/null || true
grep -i "exception\|error" /tmp/m4-2-smoke.log | grep -v "OnnxRuntimeException\|HttpClientFactory" || echo "smoke OK"
```

Expected：
- 启动 8 秒无新 Exception（onnx / httpclient 探针 warning 可忽略）

#### Step 11.3：人工冒烟（可选 — 由 controller 执行）

启动 app（GUI 内）：
1. 进入 Dashboard
2. 点击左栏 "生成 > 战略大纲" → 进入 CategoryDataPageView 三栏；右栏表单含 "总章节数" Number 输入
3. 新建分类 → 新建条目 → 在 "总章节数" 填 "60" → 切到其他条目再切回 → 60 仍存在（**Number 字段绑定修复验证**）
4. 切到 "生成 > 分卷设计" → 4 个 Number 字段（VolumeNumber / TargetChapterCount / StartChapter / EndChapter）可输入
5. 切到 "生成 > 章节规划" → Tags 字段（ReferencedCharacterNames 等）可输入
6. 切到 "生成 > 章节蓝图" → schema 切换正常
7. 切到 "生成 > 章节生成管道" → 横向 5 列 SectionCard 显示；左列 mock 章节 3 条；中间 7 step list；"开始生成 / 应用到章节" 按钮 disabled
8. 关闭 app → 重开 → 4 schema 页数据恢复（Number 字段也恢复）

#### Step 11.4：commit (none — verification step only)

无 commit。

---

## Self-Review Checklist

### Spec coverage

- [x] M4 blueprint Task M4.2.1 "Outline / VolumeDesign / ChapterPlanning / ContentConfig 四页" → Task 3-6（ContentConfig 实际是 BlueprintSchema，已在 Scope Alignment 里解释）
- [x] M4 blueprint Task M4.2.2 "ChapterPipelinePage" → Task 7 (VM) + Task 8 (view)，标 DONE_WITH_CONCERNS（仅静态布局，真逻辑 M4.4 接）
- [x] M4 blueprint "PageKey / PageRegistry / LeftNavViewModel" → Task 2 + Task 9 + Task 10
- [x] M4 blueprint "DI 注册 4 adapter" → Task 9
- [x] Mac_UI/04 pseudocode "tabs(...)" → LeftNav 5 项替代 tabs（视觉等价）
- [x] Mac_UI/06 pseudocode "5 列布局 / 7 step list / 生成 + 应用 button" → Task 8 axaml 5 SectionCard 列 + 7 step ItemsControl + 2 disabled button
- [x] Lane B concern #1 "NumericTextBox 短板" → Task 1（NumberStringConverter + 5 测试）

### Placeholder scan

- [x] 无 "TODO / 待定 / implement later"（ChapterPipelinePage 占位文案明确写 "M4.4 接入"，是设计意图非占位）
- [x] 每个 Task 给出完整代码（VM 9 行无省略；axaml 5 列全部填出 SectionCard 嵌套；converter 完整 Convert / ConvertBack）
- [x] 命令名经源生成器规则验证：M4.1 已验证 `DeleteSelectedItem` → `DeleteSelectedItemCommand` 正确生成
- [x] 反射 binding 路径完整（M4.1 已落地的 BindProperty 反射逻辑直接复用）
- [x] 4 schema 类名与 M4.0 link 名 1:1（已 grep src/Tianming.ProjectData/Modules/Generate 实测：OutlineSchema / VolumeDesignSchema / ChapterPlanningSchema / BlueprintSchema）
- [x] 4 Category/Data 类型 namespace 实测（StrategicOutline / VolumeDesign / ChapterPlanning / ChapterBlueprint）

### Type consistency

- [x] `IModuleSchema<TCategory, TData>` 不动；4 schema 继承一致
- [x] `DataManagementViewModel<TCategory, TData, TSchema>` 3 个类型参数；4 派生 VM 均显式给 3 个实参
- [x] `ChapterData` 而非 "ChapterPlanningData"（POCO 实测名）；`ChapterCategory` 而非 "ChapterPlanningCategory"
- [x] `OutlineCategory` / `OutlineData` 在 `Models.Generate.StrategicOutline` namespace
- [x] `BlueprintCategory` / `BlueprintData` 在 `Models.Generate.ChapterBlueprint` namespace（注意 namespace 含 "Chapter" 前缀，但类型名不含）
- [x] `NumberStringConverter` 双向类型：Convert(int → string)，ConvertBack(string → int 默认 0)；非法值返 0 不抛
- [x] `ChapterPipelineViewModel` 不继承 `DataManagementViewModel<,,>`（无 schema），直接 `ObservableObject`
- [x] App.axaml xmlns `vmg:` `vg:` 命名空间一致引用 Generate 子命名空间
- [x] LeftNav `NavRailItem(PageKey, string label, string iconGlyph, bool IsEnabled = true)` ctor 签名（M4.1 已用）
- [x] PageRegistry `Register<TViewModel, TView>(PageKey)` 泛型方法签名（M4.1 已用）

### Step size

- [x] 每个 Task = 1 commit；总计 11 task = 11 commit + 1 plan commit
- [x] 单 Task 内最多 7 step（Task 1 含 converter + 修复 + 测试，是最大块；其余 ≤ 4 step）
- [x] 4 schema VM Task 形式完全同构（Task 3-6），可在 subagent-driven-development 模式下并行派发

### Tests

- [x] NumberStringConverter：5 个测试（含 0→空串 / 空串→0 / 非法值→0 / 正向解析 / 反向 roundtrip）
- [x] OutlineViewModel：3 个（PageTitle / Number 字段 schema / 落盘 roundtrip with TotalChapterCount）
- [x] VolumeDesignViewModel：3 个（PageTitle / 4 Number 字段 schema 断言 / 落盘 roundtrip with 4 Number 字段）
- [x] ChapterPlanningViewModel：3 个（PageTitle / 3 Tags 字段 schema 断言 / 落盘 roundtrip with ChapterNumber + Tags）
- [x] BlueprintViewModel：3 个（PageTitle / SceneNumber schema / 落盘 roundtrip）
- [x] ChapterPipelineViewModel：4 个（PageTitle / Mock 章节 3 条 / 7 GenerationSteps / IsPipelineImplemented = false）
- [x] 合计新增 **21 测试**；baseline 1336 → 预期 1357 全过
- [x] 不破坏既有任何测试

### 不引新 NuGet

- [x] CommunityToolkit.Mvvm / Avalonia 11.0.10 / Microsoft.Extensions.DI 都已存在
- [x] NumberStringConverter 用 `System.Globalization` + `Avalonia.Data.Converters.IValueConverter` 内置
- [x] 4 个 schema 用的 namespace 都在 Tianming.ProjectData 程序集（已 reference）

### 不动 M4.0 / M4.1 已落地代码（除 Number 修复）

- [x] `IModuleSchema` / `ModuleDataAdapter` / `FieldDescriptor` / `FieldType` 不改
- [x] M4.0 4 个 Generate schema 不改
- [x] M4.1 6 个 design VM / DesignModulePage / DataManagementViewModel 基类不改
- [x] CategoryDataPageView 只改 `CreateNumericTextBox` 1 行（注 converter），不动其他控件构建逻辑
- [x] DynamicFieldConverters 只**追加** `NumberStringConverter`，不改 `TagsListStringConverter` / `FieldTypeEqualsConverter`

### 与 Lane M4.3 协作

- [x] App.axaml：M4.2 在 M4.1 DataTemplate 之后追加 5 条；M4.3 会在更后位置追加 Editor*（textual append，无冲突）
- [x] AvaloniaShellServiceCollectionExtensions：M4.2 在 M4.1 DI 块之后追加 schema/VM/PageRegistry；M4.3 会再后续追加（textual append，无冲突）
- [x] PageKeys.cs：M4.2 在 design.* 之后追加 generate.*；M4.3 会再追加 editor.*（textual append，无冲突）
- [x] LeftNavViewModel.cs：M4.2 在"设计"组后插"生成"组；M4.3 在"工具"组内启用"草稿"项 —— 不同位置追加，merge 时由 controller 解 textual conflict（如有）
- [x] **不预先 sync** M4.3 branch

---

## Commit 序列

1. `docs(plan): M4.2 生成规划 4+1 页 step-level plan`（Phase 1 完成时）
2. `fix(controls): NumberStringConverter 修复 M4.1 Number 字段 int↔string 短板`
3. `feat(nav): M4.2 generate.* 5 PageKey 入仓`
4. `feat(generate): OutlineViewModel + 3 测试`
5. `feat(generate): VolumeDesignViewModel + 3 测试 (含 4 Number 字段往返断言)`
6. `feat(generate): ChapterPlanningViewModel + 3 测试`
7. `feat(generate): BlueprintViewModel + 3 测试`
8. `feat(generate): ChapterPipelineViewModel + 4 测试 (静态布局 + mock 数据)`
9. `feat(generate): ChapterPipelinePage 静态 5 列布局 (M4.4 接入真逻辑)`
10. `feat(di): M4.2 4 schema/adapter/VM + ChapterPipelineVM + 5 PageRegistry + DataTemplate`
11. `feat(left-nav): 启用 M4.2 生成规划 5 项导航`

总 **1 plan + 10 impl commit**。
