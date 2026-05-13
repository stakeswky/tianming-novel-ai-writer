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
