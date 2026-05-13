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
    public void Fields_proxies_to_schema()
    {
        using var workspace = new TempDirectory();
        var schema = new TestSchema();
        var adapter = new ModuleDataAdapter<TestCategory, TestData>(schema, workspace.Path);
        var vm = new TestVm(adapter);

        Assert.Same(schema.Fields, vm.Fields);
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
        Assert.Contains(vm.Items, i => i.Name == "新项");
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
        Assert.Contains(vm.Items, i => i.Name == "改名后");
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

    [Fact]
    public async Task ItemsInSelectedCategory_returns_only_matching_items()
    {
        using var workspace = new TempDirectory();
        var (adapter, schema) = await CreateAdapter(workspace.Path);
        var vm = new TestVm(adapter);
        await vm.LoadAsync();
        await vm.AddCategoryAsync("C1");
        await vm.AddCategoryAsync("C2");
        await vm.AddNewItemAsync("C1", "在 C1");
        await vm.AddNewItemAsync("C2", "在 C2");

        vm.SelectedCategory = vm.Categories.Single(c => c.Name == "C1");
        var inC1 = vm.ItemsInSelectedCategory().ToList();

        Assert.Single(inC1);
        Assert.Equal("在 C1", inC1[0].Name);
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
            Id = "D" + Guid.NewGuid().ToString("N")[..8],
            IsEnabled = true,
        };
        public TestCategory CreateNewCategory(string name) => new()
        {
            Id = "C" + Guid.NewGuid().ToString("N")[..8],
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
