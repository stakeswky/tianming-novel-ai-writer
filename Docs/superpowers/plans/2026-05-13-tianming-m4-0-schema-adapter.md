# M4.0 Schema-driven 模块数据 adapter 层（step-level plan）

日期：2026-05-13
分支：`m4.0/schema-adapter`（lane A）
基于：`Docs/superpowers/plans/2026-05-12-tianming-m4-module-pages.md` Task M4.0（仅前半，不含 ChapterGenerationPipeline；controller 明确把 pipeline 排除到 M4.5/M6）
依据 spec：`Docs/superpowers/specs/2026-05-12-tianming-m4-module-pages-design.md`（来自 `m2-m6/specs-2026-05-12` 分支；M4 视觉真值源章节参考）
范围：portable 层 + Avalonia VM 基类；不动 M3 视觉，不引新 NuGet，不写 SK / Kernel / Conversation。

## Scope Alignment（Explore 输出）

### 关键类型签名

- `TM.Framework.Common.Models.IEnableable` —— `bool IsEnabled { get; set; }`
- `TM.Framework.Common.Models.ICategory : IEnableable` —— `string Id { get; set; } / string Name { get; } / string Icon { get; } / string? ParentCategory { get; } / int Level { get; } / int Order { get; set; } / bool IsBuiltIn { get; set; }`
- `TM.Framework.Common.Models.IDataItem : IEnableable` —— `string Id { get; set; } / string Name { get; set; } / string Category { get; set; } / string CategoryId { get; set; }`
- `TM.Framework.Common.Models.ISourceBookBound` —— `string? SourceBookId { get; set; }`
- `TM.Framework.Common.Models.IDependencyTracked` —— `string Id { get; set; } / Dictionary<string, int> DependencyModuleVersions { get; set; }`
- `TM.Framework.Common.Helpers.Id.ShortIdGenerator.New(string prefix)` —— 返回 `<prefix-first-upper-char><12 base32>`
- `TM.Services.Modules.ProjectData.Modules.FileModuleDataStore<TCategory, TData>` 公开 API（实测）：
  - ctor `(string moduleDirectory, string categoriesFileName, string builtInCategoriesFileName, string dataFileName)`
  - `Task LoadAsync()`
  - `IReadOnlyList<TCategory> GetCategories()`
  - `IReadOnlyList<TData> GetData()`
  - `Task<bool> AddCategoryAsync(TCategory)`
  - `Task AddDataAsync(TData)`
  - `Task<bool> DeleteDataAsync(string dataId)`
  - `Task<CascadeDeleteResult> CascadeDeleteCategoryAsync(string categoryName)`
  - `record struct CascadeDeleteResult(int CategoriesDeleted, int DataDeleted)`
  - **没有** `UpdateDataAsync` —— adapter 层用 delete + add 模拟（与 blueprint 一致）

### POCO 现状（10 个三元组）

所有 *Data + *Category 已在 `Services/Modules/ProjectData/Models/Design,Generate/**` 下，**全部 WPF-free**。
*Data 全部已通过 csproj `<Compile Include Link>` 链入 `src/Tianming.ProjectData/`（见现 csproj 第 44-53 行）。
*Category **未链入**——本次需要追加 10 行 link。

| Schema | Data 类 | Category 类 | 旧目录 |
|---|---|---|---|
| WorldRulesSchema | WorldRulesData | WorldRulesCategory | Services/Modules/ProjectData/Models/Design/Worldview/ |
| CharacterRulesSchema | CharacterRulesData | CharacterRulesCategory | .../Design/Characters/ |
| FactionRulesSchema | FactionRulesData | FactionRulesCategory | .../Design/Factions/ |
| LocationRulesSchema | LocationRulesData | LocationRulesCategory | .../Design/Location/ |
| PlotRulesSchema | PlotRulesData | PlotRulesCategory | .../Design/Plot/ |
| CreativeMaterialsSchema | CreativeMaterialData | CreativeMaterialCategory | .../Design/Templates/ |
| OutlineSchema | OutlineData | OutlineCategory | .../Generate/StrategicOutline/ |
| VolumeDesignSchema | VolumeDesignData | VolumeDesignCategory | .../Generate/VolumeDesign/ |
| ChapterPlanningSchema | ChapterData | ChapterCategory | .../Generate/ChapterPlanning/ |
| BlueprintSchema | BlueprintData | BlueprintCategory | .../Generate/ChapterBlueprint/ |

POCO 字段直接源于 `[JsonPropertyName]`，无须反推 WPF VM 的 `Form*`（实测 WorldRules VM 的 `FormPowerSystem ↔ data.PowerSystem` 等映射 1:1）。

### Avalonia VM 基类落点

- `src/Tianming.Desktop.Avalonia/ViewModels/DataManagementViewModel.cs` —— 新建
- 复用 `CommunityToolkit.Mvvm.ComponentModel.ObservableObject` 源生成器
- 不引 SK / Kernel / WPF；仅依赖 `ModuleDataAdapter<,>` + `IModuleSchema<,,>`
- 现有 VM 都是 `partial class ... : ObservableObject` + `[ObservableProperty]` 字段写法，遵循同风格

### 测试基础设施

- `tests/Tianming.ProjectData.Tests/` 已用 xUnit，内联 `TempDirectory` helper（私有 sealed class，per-test 文件夹）
- `tests/Tianming.Desktop.Avalonia.Tests/` 已用 xUnit + Avalonia.Headless.XUnit；VM 单测可直接构造 + 断言，无需 headless app

### Baseline

- `dotnet build` → 0 warn / 0 err
- `dotnet test` → 1227 total（ProjectData 218 + AI 144 + Framework 781 + Avalonia 84）

---

## 任务步骤

### Step 1：IModuleSchema + FieldDescriptor + ModuleDataAdapter（核心）

**Goal:** 在 portable 层（`Tianming.ProjectData`）建 schema 抽象 + adapter 薄壳。

**Files:**
- Create: `src/Tianming.ProjectData/Modules/Schema/FieldDescriptor.cs`
- Create: `src/Tianming.ProjectData/Modules/Schema/IModuleSchema.cs`
- Create: `src/Tianming.ProjectData/Modules/Schema/ModuleDataAdapter.cs`
- Create: `tests/Tianming.ProjectData.Tests/Modules/Schema/ModuleDataAdapterTests.cs`

#### Step 1.1：测试先行 —— ModuleDataAdapterTests

`tests/Tianming.ProjectData.Tests/Modules/Schema/ModuleDataAdapterTests.cs`:

```csharp
using TM.Framework.Common.Models;
using TM.Services.Modules.ProjectData.Modules;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Xunit;

namespace Tianming.ProjectData.Tests.Modules.Schema;

public class ModuleDataAdapterTests
{
    [Fact]
    public async Task Ctor_uses_schema_ModuleRelativePath_to_build_module_directory()
    {
        using var workspace = new TempDirectory();
        var adapter = new ModuleDataAdapter<FakeCategory, FakeData>(new FakeSchema("Design/Foo/Bar"), workspace.Path);

        await adapter.LoadAsync();

        // moduleDirectory 不应被直接暴露；这里我们靠"添加后保存的文件路径包含 schema 相对路径"间接验证
        await adapter.AddCategoryAsync(new FakeCategory { Name = "C1", IsEnabled = true });
        var expectedCategoriesFile = System.IO.Path.Combine(workspace.Path, "Design", "Foo", "Bar", "categories.json");
        Assert.True(System.IO.File.Exists(expectedCategoriesFile));
    }

    [Fact]
    public async Task AddAsync_then_GetData_returns_item()
    {
        using var workspace = new TempDirectory();
        var adapter = new ModuleDataAdapter<FakeCategory, FakeData>(new FakeSchema(), workspace.Path);
        await adapter.LoadAsync();
        await adapter.AddCategoryAsync(new FakeCategory { Name = "C1", IsEnabled = true });

        await adapter.AddAsync(new FakeData { Name = "D1", Category = "C1", IsEnabled = true });

        var data = Assert.Single(adapter.GetData());
        Assert.Equal("D1", data.Name);
    }

    [Fact]
    public async Task AddCategoryAsync_then_GetCategories_returns_category()
    {
        using var workspace = new TempDirectory();
        var adapter = new ModuleDataAdapter<FakeCategory, FakeData>(new FakeSchema(), workspace.Path);
        await adapter.LoadAsync();

        var ok = await adapter.AddCategoryAsync(new FakeCategory { Name = "C1", IsEnabled = true });

        Assert.True(ok);
        Assert.Single(adapter.GetCategories(), c => c.Name == "C1");
    }

    [Fact]
    public async Task DeleteAsync_removes_item()
    {
        using var workspace = new TempDirectory();
        var adapter = new ModuleDataAdapter<FakeCategory, FakeData>(new FakeSchema(), workspace.Path);
        await adapter.LoadAsync();
        await adapter.AddCategoryAsync(new FakeCategory { Name = "C1", IsEnabled = true });
        var item = new FakeData { Name = "D1", Category = "C1", IsEnabled = true };
        await adapter.AddAsync(item);

        var removed = await adapter.DeleteAsync(item.Id);

        Assert.True(removed);
        Assert.Empty(adapter.GetData());
    }

    [Fact]
    public async Task UpdateAsync_replaces_item_fields_keeping_id()
    {
        using var workspace = new TempDirectory();
        var adapter = new ModuleDataAdapter<FakeCategory, FakeData>(new FakeSchema(), workspace.Path);
        await adapter.LoadAsync();
        await adapter.AddCategoryAsync(new FakeCategory { Name = "C1", IsEnabled = true });
        var item = new FakeData { Name = "D1", Category = "C1", IsEnabled = true };
        await adapter.AddAsync(item);
        var originalId = item.Id;

        item.Name = "D1-updated";
        await adapter.UpdateAsync(item);

        var stored = Assert.Single(adapter.GetData());
        Assert.Equal("D1-updated", stored.Name);
        Assert.Equal(originalId, stored.Id);
    }

    [Fact]
    public async Task CascadeDeleteCategoryAsync_returns_counts()
    {
        using var workspace = new TempDirectory();
        var adapter = new ModuleDataAdapter<FakeCategory, FakeData>(new FakeSchema(), workspace.Path);
        await adapter.LoadAsync();
        await adapter.AddCategoryAsync(new FakeCategory { Name = "C1", IsEnabled = true });
        await adapter.AddAsync(new FakeData { Name = "D1", Category = "C1", IsEnabled = true });
        await adapter.AddAsync(new FakeData { Name = "D2", Category = "C1", IsEnabled = true });

        var result = await adapter.CascadeDeleteCategoryAsync("C1");

        Assert.Equal(1, result.CategoriesDeleted);
        Assert.Equal(2, result.DataDeleted);
        Assert.Empty(adapter.GetData());
        Assert.Empty(adapter.GetCategories());
    }

    [Fact]
    public void Schema_property_returns_injected_schema()
    {
        using var workspace = new TempDirectory();
        var schema = new FakeSchema("Design/Foo");
        var adapter = new ModuleDataAdapter<FakeCategory, FakeData>(schema, workspace.Path);

        Assert.Same(schema, adapter.Schema);
    }

    [Fact]
    public async Task GetDataForCategory_filters_by_category_name()
    {
        using var workspace = new TempDirectory();
        var adapter = new ModuleDataAdapter<FakeCategory, FakeData>(new FakeSchema(), workspace.Path);
        await adapter.LoadAsync();
        await adapter.AddCategoryAsync(new FakeCategory { Name = "C1", IsEnabled = true });
        await adapter.AddCategoryAsync(new FakeCategory { Name = "C2", IsEnabled = true });
        await adapter.AddAsync(new FakeData { Name = "D1", Category = "C1", IsEnabled = true });
        await adapter.AddAsync(new FakeData { Name = "D2", Category = "C2", IsEnabled = true });
        await adapter.AddAsync(new FakeData { Name = "D3", Category = "C1", IsEnabled = true });

        var c1Data = adapter.GetDataForCategory(adapter.GetCategories().Single(c => c.Name == "C1")).ToList();

        Assert.Equal(2, c1Data.Count);
        Assert.All(c1Data, d => Assert.Equal("C1", d.Category));
    }

    public sealed class FakeCategory : ICategory
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string? ParentCategory { get; set; }
        public int Level { get; set; }
        public int Order { get; set; }
        public bool IsBuiltIn { get; set; }
        public bool IsEnabled { get; set; }
    }

    public sealed class FakeData : IDataItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }

    private sealed class FakeSchema : IModuleSchema<FakeCategory, FakeData>
    {
        public FakeSchema(string relativePath = "TestModule") => ModuleRelativePath = relativePath;
        public string PageTitle => "Test";
        public string PageIcon => "🧪";
        public string ModuleRelativePath { get; }
        public IReadOnlyList<FieldDescriptor> Fields { get; } = new[]
        {
            new FieldDescriptor("Name", "名称", FieldType.SingleLineText, true, null)
        };
        public FakeData CreateNewItem() => new() { IsEnabled = true };
        public FakeCategory CreateNewCategory(string name) => new() { Name = name, IsEnabled = true };
        public string BuildAIPromptContext(IReadOnlyList<FakeData> existing) => string.Join(",", existing.Select(x => x.Name));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-adapter-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            System.IO.Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Path))
                System.IO.Directory.Delete(Path, recursive: true);
        }
    }
}
```

#### Step 1.2：实装

`src/Tianming.ProjectData/Modules/Schema/FieldDescriptor.cs`:

```csharp
using System.Collections.Generic;

namespace TM.Services.Modules.ProjectData.Modules.Schema;

public enum FieldType
{
    SingleLineText,
    MultiLineText,
    Number,
    Tags,        // List<string>，逗号/空格分隔输入
    Enum,
    Boolean
}

public sealed record FieldDescriptor(
    string PropertyName,
    string Label,
    FieldType Type,
    bool Required,
    string? Placeholder,
    int? MaxLength = null,
    IReadOnlyList<string>? EnumOptions = null);
```

`src/Tianming.ProjectData/Modules/Schema/IModuleSchema.cs`:

```csharp
using System.Collections.Generic;
using TM.Framework.Common.Models;

namespace TM.Services.Modules.ProjectData.Modules.Schema;

/// <summary>
/// 模块 schema —— 描述一个 Category/Data 三元组的元数据。
/// 供 ModuleDataAdapter 和 DataManagementViewModel 使用，是 M4.1+ 页面统一渲染的源头。
/// </summary>
public interface IModuleSchema<TCategory, TData>
    where TCategory : class, ICategory
    where TData : class, IDataItem
{
    /// <summary>页面标题，例如"世界观规则"。</summary>
    string PageTitle { get; }

    /// <summary>页面图标（emoji 或 Lucide 名）。</summary>
    string PageIcon { get; }

    /// <summary>
    /// 相对项目根目录的模块路径，如 "Design/GlobalSettings/WorldRules"。
    /// FileModuleDataStore 会在此路径下读写 categories.json / built_in_categories.json / data.json。
    /// </summary>
    string ModuleRelativePath { get; }

    /// <summary>字段描述（驱动 DataFormView 动态渲染）。</summary>
    IReadOnlyList<FieldDescriptor> Fields { get; }

    /// <summary>创建带默认值的空白数据项。</summary>
    TData CreateNewItem();

    /// <summary>创建带默认值的空白分类。</summary>
    TCategory CreateNewCategory(string name);

    /// <summary>
    /// 为 AI 批量生成构造上下文文本（M4.1/M4.5 之后接入；M4.0 留接口）。
    /// </summary>
    string BuildAIPromptContext(IReadOnlyList<TData> existing);
}
```

`src/Tianming.ProjectData/Modules/Schema/ModuleDataAdapter.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TM.Framework.Common.Models;

namespace TM.Services.Modules.ProjectData.Modules.Schema;

/// <summary>
/// FileModuleDataStore 之上的薄壳：携带 IModuleSchema，提供 CRUD/CascadeDelete 等
/// 接口供 Avalonia VM 消费。Update 走 Delete+Add（FileModuleDataStore 无原子 Update）。
/// </summary>
public sealed class ModuleDataAdapter<TCategory, TData>
    where TCategory : class, ICategory
    where TData : class, IDataItem
{
    private readonly FileModuleDataStore<TCategory, TData> _store;

    public IModuleSchema<TCategory, TData> Schema { get; }

    public ModuleDataAdapter(IModuleSchema<TCategory, TData> schema, string projectRoot)
    {
        Schema = schema ?? throw new System.ArgumentNullException(nameof(schema));
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new System.ArgumentException("项目根目录不能为空", nameof(projectRoot));

        var moduleDir = System.IO.Path.Combine(projectRoot, schema.ModuleRelativePath);
        _store = new FileModuleDataStore<TCategory, TData>(
            moduleDirectory: moduleDir,
            categoriesFileName: "categories.json",
            builtInCategoriesFileName: "built_in_categories.json",
            dataFileName: "data.json");
    }

    public Task LoadAsync() => _store.LoadAsync();

    public IReadOnlyList<TCategory> GetCategories() => _store.GetCategories();

    public IReadOnlyList<TData> GetData() => _store.GetData();

    public Task<bool> AddCategoryAsync(TCategory category) => _store.AddCategoryAsync(category);

    public Task AddAsync(TData data) => _store.AddDataAsync(data);

    public Task<bool> DeleteAsync(string dataId) => _store.DeleteDataAsync(dataId);

    public Task<CascadeDeleteResult> CascadeDeleteCategoryAsync(string categoryName)
        => _store.CascadeDeleteCategoryAsync(categoryName);

    /// <summary>Update = Delete by Id + Add（保留原 Id）。</summary>
    public async Task UpdateAsync(TData data)
    {
        if (data == null) throw new System.ArgumentNullException(nameof(data));
        await _store.DeleteDataAsync(data.Id).ConfigureAwait(false);
        await _store.AddDataAsync(data).ConfigureAwait(false);
    }

    /// <summary>按 category.Name 过滤。FileModuleDataStore 数据项的 Category 字段存 name。</summary>
    public IEnumerable<TData> GetDataForCategory(TCategory category)
    {
        if (category == null) throw new System.ArgumentNullException(nameof(category));
        return _store.GetData().Where(d => d.Category == category.Name);
    }
}
```

#### Step 1.3：跑测试 + commit

- `dotnet build Tianming.MacMigration.sln -v q` → 0/0
- `dotnet test Tianming.MacMigration.sln -v q` → 1227 + 8 = 1235 通过
- Commit: `feat(projectdata): 模块数据 schema 抽象 + ModuleDataAdapter`

**验收：** adapter 测试 ≥ 8，全过；baseline 测试不破坏。

---

### Step 2：10 个 Schema POCO + csproj link（每 schema 一个子步骤）

每个 schema 都遵循同一模板：

1. csproj 加一行 link Category（Data 已链）
2. 写 `<Module>Schema.cs`
3. 写 `<Module>SchemaTests.cs`（≥ 3 测试：Fields 数量、PageTitle、CreateNewItem / CreateNewCategory）
4. `dotnet build` + `dotnet test` 全过
5. commit

#### Step 2.1：WorldRulesSchema

**csproj 追加：**

```xml
<Compile Include="../../Services/Modules/ProjectData/Models/Design/Worldview/WorldRulesCategory.cs"
         Link="Services/Modules/ProjectData/Models/Design/Worldview/WorldRulesCategory.cs" />
```

**File:** `src/Tianming.ProjectData/Modules/Design/WorldRules/WorldRulesSchema.cs`

字段（来自 `WorldRulesData` POCO）：Name / OneLineSummary / PowerSystem / Cosmology / SpecialLaws / HardRules / SoftRules / AncientEra / KeyEvents / ModernHistory / StatusQuo（11 个；Description 是 getter alias，不入 form）。

```csharp
using System.Collections.Generic;
using System.Linq;
using TM.Framework.Common.Helpers.Id;
using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace TM.Services.Modules.ProjectData.Modules.Design.WorldRules;

public sealed class WorldRulesSchema : IModuleSchema<WorldRulesCategory, WorldRulesData>
{
    public string PageTitle => "世界观规则";
    public string PageIcon => "🌍";
    public string ModuleRelativePath => "Design/GlobalSettings/WorldRules";

    public IReadOnlyList<FieldDescriptor> Fields { get; } = new[]
    {
        new FieldDescriptor("Name",           "规则名称",   FieldType.SingleLineText, true,  "如：九州大陆"),
        new FieldDescriptor("OneLineSummary", "一句话简介", FieldType.SingleLineText, false, null),
        new FieldDescriptor("PowerSystem",    "力量体系",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Cosmology",      "宇宙观",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("SpecialLaws",    "特殊法则",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("HardRules",      "硬性规则",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("SoftRules",      "软性规则",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("AncientEra",     "远古时期",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("KeyEvents",      "关键事件",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("ModernHistory",  "近代史",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("StatusQuo",      "当下格局",   FieldType.MultiLineText,  false, null),
    };

    public WorldRulesData CreateNewItem() => new()
    {
        Id = ShortIdGenerator.New("D"),
        Name = string.Empty,
        IsEnabled = true,
    };

    public WorldRulesCategory CreateNewCategory(string name) => new()
    {
        Id = ShortIdGenerator.New("C"),
        Name = name,
        Icon = "🌍",
        Level = 1,
        Order = 0,
        IsBuiltIn = false,
        IsEnabled = true,
    };

    public string BuildAIPromptContext(IReadOnlyList<WorldRulesData> existing)
        => string.Join("\n---\n", existing.Select(x => $"{x.Name}: {x.OneLineSummary}"));
}
```

**Test:** `tests/Tianming.ProjectData.Tests/Modules/Design/WorldRulesSchemaTests.cs`

```csharp
using TM.Services.Modules.ProjectData.Modules.Design.WorldRules;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Xunit;

namespace Tianming.ProjectData.Tests.Modules.Design;

public class WorldRulesSchemaTests
{
    [Fact]
    public void Fields_count_matches_form_design()
    {
        var schema = new WorldRulesSchema();
        Assert.Equal(11, schema.Fields.Count);
    }

    [Fact]
    public void PageTitle_and_relative_path_are_set()
    {
        var schema = new WorldRulesSchema();
        Assert.Equal("世界观规则", schema.PageTitle);
        Assert.Equal("Design/GlobalSettings/WorldRules", schema.ModuleRelativePath);
    }

    [Fact]
    public void CreateNewItem_returns_data_with_data_id_prefix()
    {
        var schema = new WorldRulesSchema();
        var item = schema.CreateNewItem();
        Assert.StartsWith("D", item.Id);
        Assert.True(item.IsEnabled);
    }

    [Fact]
    public void CreateNewCategory_uses_supplied_name_and_category_prefix()
    {
        var schema = new WorldRulesSchema();
        var cat = schema.CreateNewCategory("修真大陆");
        Assert.Equal("修真大陆", cat.Name);
        Assert.StartsWith("C", cat.Id);
    }

    [Fact]
    public void Name_field_is_required()
    {
        var schema = new WorldRulesSchema();
        var name = schema.Fields.Single(f => f.PropertyName == "Name");
        Assert.True(name.Required);
    }
}
```

**Commit:** `feat(projectdata): WorldRulesSchema + csproj link`

#### Step 2.2：CharacterRulesSchema

字段（来自 `CharacterRulesData`，去重计 19 个 + Name = 20）：Name / CharacterType / Gender / Age / Identity / Race / Appearance / Want / Need / FlawBelief / GrowthPath / TargetCharacterName / RelationshipType / EmotionDynamic / CombatSkills / NonCombatSkills / SpecialAbilities / SignatureItems / CommonItems / PersonalAssets。

CharacterType 是 enum（"主角" / "主要角色" / "重要配角" / "次要配角" / "龙套"），其他都是文本。

csproj link：`Services/Modules/ProjectData/Models/Design/Characters/CharacterRulesCategory.cs`

`src/Tianming.ProjectData/Modules/Design/CharacterRules/CharacterRulesSchema.cs`：同模板，PageTitle="角色规则" PageIcon="👤" ModuleRelativePath="Design/Elements/Characters" CategoryIcon="👤"，Fields 含 enum option。

测试 ≥ 5（Fields 数=20、PageTitle、CreateNewItem D 前缀、CreateNewCategory、CharacterType 是 enum）

Commit: `feat(projectdata): CharacterRulesSchema + csproj link`

#### Step 2.3：FactionRulesSchema

字段：Name / FactionType / Goal / StrengthTerritory / Leader / CoreMembers / MemberTraits / Allies / Enemies / NeutralCompetitors（10 个）

csproj link：`Services/Modules/ProjectData/Models/Design/Factions/FactionRulesCategory.cs`

PageTitle="势力规则" PageIcon="⚔️" ModuleRelativePath="Design/Elements/Factions"

测试 ≥ 3

Commit: `feat(projectdata): FactionRulesSchema + csproj link`

#### Step 2.4：LocationRulesSchema

字段：Name / LocationType / Description / Scale / Terrain / Climate / Landmarks(Tags) / Resources(Tags) / HistoricalSignificance / Dangers(Tags) / FactionId（11 个）

csproj link：`Services/Modules/ProjectData/Models/Design/Location/LocationRulesCategory.cs`

PageTitle="地点规则" PageIcon="📍" ModuleRelativePath="Design/Elements/Locations"

测试 ≥ 4（含 Tags 字段类型断言）

Commit: `feat(projectdata): LocationRulesSchema + csproj link`

#### Step 2.5：PlotRulesSchema

字段：Name / TargetVolume / AssignedVolume / OneLineSummary / EventType / StoryPhase / PrerequisitesTrigger / MainCharacters / KeyNpcs / Location / TimeDuration / StepTitle / Goal / Conflict / Result / EmotionCurve / MainPlotPush / CharacterGrowth / WorldReveal / RewardsClues（20 个）

csproj link：`Services/Modules/ProjectData/Models/Design/Plot/PlotRulesCategory.cs`

PageTitle="剧情规则" PageIcon="📖" ModuleRelativePath="Design/Elements/Plot"

测试 ≥ 3

Commit: `feat(projectdata): PlotRulesSchema + csproj link`

#### Step 2.6：CreativeMaterialsSchema

字段（来自 `CreativeMaterialData`）：Name / Icon / SourceBookName / Genre / OverallIdea / WorldBuildingMethod / PowerSystemDesign / EnvironmentDescription / FactionDesign / WorldviewHighlights / ProtagonistDesign / SupportingRoles / CharacterRelations / GoldenFingerDesign / CharacterHighlights / PlotStructure / ConflictDesign / ClimaxArrangement / ForeshadowingTechnique / PlotHighlights（20 个）

csproj link：`Services/Modules/ProjectData/Models/Design/Templates/CreativeMaterialCategory.cs`

注意：`CreativeMaterialData` **不继承 BusinessDataBase**，而是直接实现 `IDataItem / IIndexable / ISourceBookBound`，有自己的 `Icon / CreatedTime / ModifiedTime / SourceBookName / Genre / OverallIdea ...` 字段。

PageTitle="创意素材库" PageIcon="💡" ModuleRelativePath="Design/Templates/CreativeMaterials"

测试 ≥ 3

Commit: `feat(projectdata): CreativeMaterialsSchema + csproj link`

#### Step 2.7：OutlineSchema（Generation）

字段（来自 `OutlineData`）：Name / TotalChapterCount / EstimatedWordCount / OneLineOutline / EmotionalTone / PhilosophicalMotif / Theme / CoreConflict / EndingState / VolumeDivision / OutlineOverview（11 个）

csproj link：`Services/Modules/ProjectData/Models/Generate/StrategicOutline/OutlineCategory.cs`

PageTitle="战略大纲" PageIcon="📖" ModuleRelativePath="Generate/StrategicOutline"

TotalChapterCount 是 Number。

测试 ≥ 3

Commit: `feat(projectdata): OutlineSchema + csproj link`

#### Step 2.8：VolumeDesignSchema

字段：Name / VolumeNumber / VolumeTitle / VolumeTheme / StageGoal / EstimatedWordCount / TargetChapterCount / StartChapter / EndChapter / MainConflict / PressureSource / KeyEvents / OpeningState / EndingState / ReferencedCharacterNames(Tags) / ReferencedFactionNames(Tags) / ReferencedLocationNames(Tags) / ChapterAllocationOverview / PlotAllocation / ChapterGenerationHints（20 个）

csproj link：`Services/Modules/ProjectData/Models/Generate/VolumeDesign/VolumeDesignCategory.cs`

PageTitle="分卷设计" PageIcon="📚" ModuleRelativePath="Generate/VolumeDesign"

VolumeNumber / TargetChapterCount / StartChapter / EndChapter 是 Number。

测试 ≥ 3

Commit: `feat(projectdata): VolumeDesignSchema + csproj link`

#### Step 2.9：ChapterPlanningSchema

字段（来自 `ChapterData`）：Name / ChapterTitle / ChapterNumber / Volume / EstimatedWordCount / ChapterTheme / ReaderExperienceGoal / MainGoal / ResistanceSource / KeyTurn / Hook / WorldInfoDrop / CharacterArcProgress / MainPlotProgress / Foreshadowing / ReferencedCharacterNames(Tags) / ReferencedFactionNames(Tags) / ReferencedLocationNames(Tags)（18 个）

csproj link：`Services/Modules/ProjectData/Models/Generate/ChapterPlanning/ChapterCategory.cs`

PageTitle="章节规划" PageIcon="📑" ModuleRelativePath="Generate/ChapterPlanning"

ChapterNumber 是 Number。

测试 ≥ 3

Commit: `feat(projectdata): ChapterPlanningSchema + csproj link`

#### Step 2.10：BlueprintSchema

字段（来自 `BlueprintData`）：Name / ChapterId / OneLineStructure / PacingCurve / SceneNumber / SceneTitle / PovCharacter / EstimatedWordCount / Opening / Development / Turning / Ending / InfoDrop / Cast / Locations / Factions / ItemsClues（17 个）

csproj link：`Services/Modules/ProjectData/Models/Generate/ChapterBlueprint/BlueprintCategory.cs`

PageTitle="章节蓝图" PageIcon="🎬" ModuleRelativePath="Generate/ChapterBlueprint"

SceneNumber 是 Number。

测试 ≥ 3

Commit: `feat(projectdata): BlueprintSchema + csproj link`

---

### Step 3：DataManagementViewModel<TCategory, TData, TSchema> 基类（Avalonia）

**Goal:** 让 M4.1+ 各页面 VM 只需要继承这个泛型基类、指定三元组类型、注入 schema + adapter，即可获得 Categories / Items / SelectedItem / Add / Update / Delete / Reload 等公共行为。

**Files:**
- Create: `src/Tianming.Desktop.Avalonia/ViewModels/DataManagementViewModel.cs`
- Create: `tests/Tianming.Desktop.Avalonia.Tests/ViewModels/DataManagementViewModelTests.cs`

#### Step 3.1：测试先行

`tests/Tianming.Desktop.Avalonia.Tests/ViewModels/DataManagementViewModelTests.cs`：

```csharp
using TM.Framework.Common.Models;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels;

public class DataManagementViewModelTests
{
    [Fact]
    public async Task LoadAsync_populates_categories_and_items()
    {
        using var workspace = new TempDirectory();
        var (adapter, _) = await CreateAdapter(workspace.Path, withItems: true);
        var vm = new TestVm(adapter);

        await vm.LoadAsync();

        Assert.Single(vm.Categories);
        Assert.Equal(2, vm.Items.Count);
    }

    [Fact]
    public void PageTitle_proxies_to_schema()
    {
        using var workspace = new TempDirectory();
        var schema = new TestSchema { PageTitleValue = "测试页" };
        var adapter = new ModuleDataAdapter<TestCategory, TestData>(schema, workspace.Path);
        var vm = new TestVm(adapter);

        Assert.Equal("测试页", vm.PageTitle);
    }

    [Fact]
    public async Task AddNewItemAsync_creates_via_schema_and_persists()
    {
        using var workspace = new TempDirectory();
        var (adapter, _) = await CreateAdapter(workspace.Path);
        var vm = new TestVm(adapter);
        await vm.LoadAsync();

        var created = await vm.AddNewItemAsync(categoryName: "C1", name: "新项");

        Assert.NotNull(created);
        Assert.Equal("新项", created!.Name);
        Assert.Equal("C1", created.Category);
        Assert.Single(vm.Items, i => i.Name == "新项");
    }

    [Fact]
    public async Task DeleteSelectedItemAsync_removes_item_and_clears_selection()
    {
        using var workspace = new TempDirectory();
        var (adapter, _) = await CreateAdapter(workspace.Path, withItems: true);
        var vm = new TestVm(adapter);
        await vm.LoadAsync();
        vm.SelectedItem = vm.Items[0];
        var deletedId = vm.SelectedItem.Id;

        var ok = await vm.DeleteSelectedItemAsync();

        Assert.True(ok);
        Assert.Null(vm.SelectedItem);
        Assert.DoesNotContain(vm.Items, i => i.Id == deletedId);
    }

    [Fact]
    public async Task UpdateSelectedItemAsync_persists_and_refreshes()
    {
        using var workspace = new TempDirectory();
        var (adapter, _) = await CreateAdapter(workspace.Path, withItems: true);
        var vm = new TestVm(adapter);
        await vm.LoadAsync();
        vm.SelectedItem = vm.Items[0];
        vm.SelectedItem.Name = "改名后";

        await vm.UpdateSelectedItemAsync();

        await vm.LoadAsync();
        Assert.Single(vm.Items, i => i.Name == "改名后");
    }

    [Fact]
    public async Task AddCategoryAsync_proxies_to_schema_CreateNewCategory()
    {
        using var workspace = new TempDirectory();
        var (adapter, _) = await CreateAdapter(workspace.Path);
        var vm = new TestVm(adapter);
        await vm.LoadAsync();

        var ok = await vm.AddCategoryAsync("新分类");

        Assert.True(ok);
        Assert.Contains(vm.Categories, c => c.Name == "新分类");
    }

    private static async Task<(ModuleDataAdapter<TestCategory, TestData> adapter, TestSchema schema)> CreateAdapter(string root, bool withItems = false)
    {
        var schema = new TestSchema();
        var adapter = new ModuleDataAdapter<TestCategory, TestData>(schema, root);
        await adapter.LoadAsync();
        if (withItems)
        {
            await adapter.AddCategoryAsync(schema.CreateNewCategory("C1"));
            await adapter.AddAsync(new TestData { Name = "I1", Category = "C1", IsEnabled = true });
            await adapter.AddAsync(new TestData { Name = "I2", Category = "C1", IsEnabled = true });
        }
        return (adapter, schema);
    }

    private sealed class TestVm : DataManagementViewModel<TestCategory, TestData, TestSchema>
    {
        public TestVm(ModuleDataAdapter<TestCategory, TestData> adapter) : base(adapter) { }
    }

    public sealed class TestCategory : ICategory
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string? ParentCategory { get; set; }
        public int Level { get; set; } = 1;
        public int Order { get; set; }
        public bool IsBuiltIn { get; set; }
        public bool IsEnabled { get; set; } = true;
    }

    public sealed class TestData : IDataItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }

    public sealed class TestSchema : IModuleSchema<TestCategory, TestData>
    {
        public string PageTitle => PageTitleValue;
        public string PageTitleValue { get; set; } = "Test";
        public string PageIcon => "🧪";
        public string ModuleRelativePath => "TestModule";
        public IReadOnlyList<FieldDescriptor> Fields { get; } = new[]
        {
            new FieldDescriptor("Name", "名称", FieldType.SingleLineText, true, null)
        };
        public TestData CreateNewItem() => new()
        {
            Id = TM.Framework.Common.Helpers.Id.ShortIdGenerator.New("D"),
            IsEnabled = true,
        };
        public TestCategory CreateNewCategory(string name) => new()
        {
            Id = TM.Framework.Common.Helpers.Id.ShortIdGenerator.New("C"),
            Name = name,
            IsEnabled = true,
        };
        public string BuildAIPromptContext(IReadOnlyList<TestData> existing) => string.Empty;
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-dmvm-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            System.IO.Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Path))
                System.IO.Directory.Delete(Path, recursive: true);
        }
    }
}
```

#### Step 3.2：实装

`src/Tianming.Desktop.Avalonia/ViewModels/DataManagementViewModel.cs`：

```csharp
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using TM.Framework.Common.Models;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels;

/// <summary>
/// Schema-driven 数据管理 VM 基类。供 M4.1 设计模块 6 页、M4.2 生成规划 4 页继承。
/// 业务逻辑全部走 ModuleDataAdapter，不引 SK/Kernel/WPF。
/// </summary>
public abstract partial class DataManagementViewModel<TCategory, TData, TSchema> : ObservableObject
    where TCategory : class, ICategory
    where TData : class, IDataItem
    where TSchema : IModuleSchema<TCategory, TData>
{
    protected ModuleDataAdapter<TCategory, TData> Adapter { get; }

    public TSchema Schema => (TSchema)Adapter.Schema;

    public string PageTitle => Adapter.Schema.PageTitle;
    public string PageIcon => Adapter.Schema.PageIcon;
    public IReadOnlyList<FieldDescriptor> Fields => Adapter.Schema.Fields;

    public ObservableCollection<TCategory> Categories { get; } = new();
    public ObservableCollection<TData> Items { get; } = new();

    [ObservableProperty]
    private TCategory? _selectedCategory;

    [ObservableProperty]
    private TData? _selectedItem;

    [ObservableProperty]
    private bool _isLoading;

    protected DataManagementViewModel(ModuleDataAdapter<TCategory, TData> adapter)
    {
        Adapter = adapter ?? throw new System.ArgumentNullException(nameof(adapter));
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            await Adapter.LoadAsync().ConfigureAwait(false);
            Categories.Clear();
            foreach (var c in Adapter.GetCategories())
                Categories.Add(c);
            Items.Clear();
            foreach (var d in Adapter.GetData())
                Items.Add(d);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<bool> AddCategoryAsync(string name)
    {
        var cat = Adapter.Schema.CreateNewCategory(name);
        var ok = await Adapter.AddCategoryAsync(cat).ConfigureAwait(false);
        if (ok)
            Categories.Add(cat);
        return ok;
    }

    public async Task<TData?> AddNewItemAsync(string categoryName, string name)
    {
        var item = Adapter.Schema.CreateNewItem();
        item.Name = name;
        item.Category = categoryName;
        await Adapter.AddAsync(item).ConfigureAwait(false);
        Items.Add(item);
        return item;
    }

    public async Task UpdateSelectedItemAsync()
    {
        if (SelectedItem == null) return;
        await Adapter.UpdateAsync(SelectedItem).ConfigureAwait(false);
    }

    public async Task<bool> DeleteSelectedItemAsync()
    {
        if (SelectedItem == null) return false;
        var ok = await Adapter.DeleteAsync(SelectedItem.Id).ConfigureAwait(false);
        if (ok)
        {
            Items.Remove(SelectedItem);
            SelectedItem = null;
        }
        return ok;
    }

    public Task<CascadeDeleteResult> CascadeDeleteCategoryAsync(string categoryName)
        => Adapter.CascadeDeleteCategoryAsync(categoryName);

    public IEnumerable<TData> ItemsInSelectedCategory()
        => SelectedCategory == null
            ? Enumerable.Empty<TData>()
            : Adapter.GetDataForCategory(SelectedCategory);
}
```

#### Step 3.3：跑测试 + commit

- `dotnet build` → 0/0
- `dotnet test` → +6 测试通过
- Commit: `feat(avalonia): DataManagementViewModel 基类 (schema-driven)`

---

### Step 4：Self-review + 冒烟启动

#### Self-review checklist

- [ ] spec coverage：10 个 schema 三元组 + 1 接口 + 1 adapter + 1 VM 基类 = 13 个新组件，全部覆盖
- [ ] placeholder scan：grep "TODO" / "FIXME" 在新增文件中——只允许 "M4.x" 标注，不允许 `throw new NotImplementedException`
- [ ] type consistency：CharacterRulesCategory 等都通过 csproj link 走 namespace 已存在；Schema.cs 引用 namespace 一致
- [ ] step size：每个 schema 一个 commit，独立可回退
- [ ] tests：10 schema × ≥3 = 30+，adapter ≥ 8，VM ≥ 6 = 44+ 新增；baseline 不破坏
- [ ] 不引新 NuGet：核查 csproj 包列表

#### 冒烟

```bash
dotnet run --project src/Tianming.Desktop.Avalonia -c Release &
sleep 6
kill %1
```

预期：M3 视觉无变化，无新 Exception in log。

#### 最终验收

- `dotnet build Tianming.MacMigration.sln -v q` → 0/0
- `dotnet test Tianming.MacMigration.sln -v q` → 1227 + 44+ ≈ 1271+ 全过
- 11 个 impl commit（adapter + 10 schema + VM 基类）+ 1 plan commit

---

## Commit 序列

1. `docs(plan): M4.0 schema-driven adapter step-level plan`（Phase 1 完成时）
2. `feat(projectdata): 模块数据 schema 抽象 + ModuleDataAdapter`
3. `feat(projectdata): WorldRulesSchema + csproj link`
4. `feat(projectdata): CharacterRulesSchema + csproj link`
5. `feat(projectdata): FactionRulesSchema + csproj link`
6. `feat(projectdata): LocationRulesSchema + csproj link`
7. `feat(projectdata): PlotRulesSchema + csproj link`
8. `feat(projectdata): CreativeMaterialsSchema + csproj link`
9. `feat(projectdata): OutlineSchema + csproj link`
10. `feat(projectdata): VolumeDesignSchema + csproj link`
11. `feat(projectdata): ChapterPlanningSchema + csproj link`
12. `feat(projectdata): BlueprintSchema + csproj link`
13. `feat(avalonia): DataManagementViewModel 基类 (schema-driven)`
