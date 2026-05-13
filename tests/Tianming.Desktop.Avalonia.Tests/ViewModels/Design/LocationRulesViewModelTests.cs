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
