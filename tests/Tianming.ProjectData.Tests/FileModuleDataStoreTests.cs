using TM.Framework.Common.Models;
using TM.Services.Modules.ProjectData.Modules;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class FileModuleDataStoreTests
{
    [Fact]
    public async Task LoadAsync_merges_categories_assigns_ids_and_binds_data_category_ids()
    {
        using var workspace = new TempDirectory();
        var moduleDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "CharacterRules");
        Directory.CreateDirectory(moduleDir);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(moduleDir, "categories.json"),
            """[{ "Name": "主角", "Icon": "user", "Order": 2, "IsEnabled": true }]""");
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(moduleDir, "built_in_categories.json"),
            """[{ "Name": "配角", "Icon": "users", "Order": 1, "IsEnabled": true }]""");
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(moduleDir, "Characters.json"),
            """[{ "Name": "林衡", "Category": "主角", "IsEnabled": true }]""");
        var store = new FileModuleDataStore<TestCategory, TestData>(
            moduleDir,
            "categories.json",
            "built_in_categories.json",
            "Characters.json");

        await store.LoadAsync();

        var categories = store.GetCategories();
        var data = Assert.Single(store.GetData());
        Assert.Equal(["配角", "主角"], categories.Select(category => category.Name).ToArray());
        Assert.True(categories[0].IsBuiltIn);
        Assert.All(categories, category => Assert.False(string.IsNullOrWhiteSpace(category.Id)));
        Assert.False(string.IsNullOrWhiteSpace(data.Id));
        Assert.Equal(categories.Single(category => category.Name == "主角").Id, data.CategoryId);
    }

    [Fact]
    public async Task AddDataAsync_and_delete_data_persist_changes()
    {
        using var workspace = new TempDirectory();
        var store = CreateStore(workspace.Path);
        await store.LoadAsync();
        await store.AddCategoryAsync(new TestCategory { Name = "主角", IsEnabled = true });

        var item = new TestData { Name = "林衡", Category = "主角", IsEnabled = true };
        await store.AddDataAsync(item);
        var deleted = await store.DeleteDataAsync(item.Id);

        var reloaded = CreateStore(workspace.Path);
        await reloaded.LoadAsync();
        Assert.True(deleted);
        Assert.Empty(reloaded.GetData());
    }

    [Fact]
    public async Task CascadeDeleteCategoryAsync_removes_user_category_subtree_and_matching_data()
    {
        using var workspace = new TempDirectory();
        var store = CreateStore(workspace.Path);
        await store.LoadAsync();
        await store.AddCategoryAsync(new TestCategory { Name = "人物", IsEnabled = true });
        await store.AddCategoryAsync(new TestCategory { Name = "主角", ParentCategory = "人物", IsEnabled = true });
        await store.AddDataAsync(new TestData { Name = "林衡", Category = "主角", IsEnabled = true });
        await store.AddDataAsync(new TestData { Name = "陆澜", Category = "人物", IsEnabled = true });

        var result = await store.CascadeDeleteCategoryAsync("人物");

        Assert.Equal(2, result.CategoriesDeleted);
        Assert.Equal(2, result.DataDeleted);
        Assert.Empty(store.GetCategories());
        Assert.Empty(store.GetData());
    }

    [Fact]
    public async Task CascadeDeleteCategoryAsync_keeps_built_in_category_but_removes_direct_data()
    {
        using var workspace = new TempDirectory();
        var moduleDir = System.IO.Path.Combine(workspace.Path, "Modules", "Design", "Elements", "CharacterRules");
        Directory.CreateDirectory(moduleDir);
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(moduleDir, "built_in_categories.json"),
            """[{ "Name": "系统", "Icon": "lock", "Order": 0, "IsEnabled": true }]""");
        await File.WriteAllTextAsync(
            System.IO.Path.Combine(moduleDir, "Characters.json"),
            """[{ "Name": "天道", "Category": "系统", "IsEnabled": true }]""");
        var store = new FileModuleDataStore<TestCategory, TestData>(
            moduleDir,
            "categories.json",
            "built_in_categories.json",
            "Characters.json");
        await store.LoadAsync();

        var result = await store.CascadeDeleteCategoryAsync("系统");

        Assert.Equal(0, result.CategoriesDeleted);
        Assert.Equal(1, result.DataDeleted);
        Assert.Single(store.GetCategories());
        Assert.Empty(store.GetData());
    }

    private static FileModuleDataStore<TestCategory, TestData> CreateStore(string root)
    {
        var moduleDir = System.IO.Path.Combine(root, "Modules", "Design", "Elements", "CharacterRules");
        return new FileModuleDataStore<TestCategory, TestData>(
            moduleDir,
            "categories.json",
            "built_in_categories.json",
            "Characters.json");
    }

    public sealed class TestCategory : ICategory
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

    public sealed class TestData : IDataItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string CategoryId { get; set; } = string.Empty;
        public bool IsEnabled { get; set; }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-module-store-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
