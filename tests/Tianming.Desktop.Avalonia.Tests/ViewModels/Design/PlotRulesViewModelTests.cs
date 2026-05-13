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
