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

        var c1 = adapter.GetCategories().Single(c => c.Name == "C1");
        var c1Data = adapter.GetDataForCategory(c1).ToList();

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
