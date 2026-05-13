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
