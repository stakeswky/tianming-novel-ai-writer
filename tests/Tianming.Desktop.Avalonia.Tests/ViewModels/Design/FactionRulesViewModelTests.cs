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
