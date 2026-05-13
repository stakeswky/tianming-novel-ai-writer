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
