using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Generate.VolumeDesign;
using TM.Services.Modules.ProjectData.Modules.Generate.VolumeDesign;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels.Generate;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Generate;

public class VolumeDesignViewModelTests
{
    [Fact]
    public void PageTitle_is_分卷设计()
    {
        var (vm, _) = NewVm();
        Assert.Equal("分卷设计", vm.PageTitle);
    }

    [Fact]
    public void Fields_include_4_Number_fields()
    {
        var (vm, _) = NewVm();
        var numbers = vm.Fields.Where(f => f.Type == FieldType.Number).Select(f => f.PropertyName).ToList();
        Assert.Equal(4, numbers.Count);
        Assert.Contains("VolumeNumber",       numbers);
        Assert.Contains("TargetChapterCount", numbers);
        Assert.Contains("StartChapter",       numbers);
        Assert.Contains("EndChapter",         numbers);
    }

    [Fact]
    public async Task AddVolume_with_Number_fields_round_trips()
    {
        var (vm, _) = NewVm();
        await vm.LoadAsync();
        await vm.AddCategoryAsync("正稿");
        vm.SelectedCategory = vm.Categories[0];
        await vm.AddNewItemInCurrentCategoryCommand.ExecuteAsync(null);
        vm.SelectedItem!.Name = "第一卷·起势";
        vm.SelectedItem.VolumeNumber = 1;
        vm.SelectedItem.TargetChapterCount = 20;
        vm.SelectedItem.StartChapter = 1;
        vm.SelectedItem.EndChapter = 20;
        vm.SelectedItem.StageGoal = "主角入门";

        await vm.UpdateSelectedItemCommand.ExecuteAsync(null);
        await vm.LoadAsync();

        var loaded = vm.Items.Single(i => i.Name == "第一卷·起势");
        Assert.Equal(1, loaded.VolumeNumber);
        Assert.Equal(20, loaded.TargetChapterCount);
        Assert.Equal(20, loaded.EndChapter);
        Assert.Equal("主角入门", loaded.StageGoal);
    }

    private static (VolumeDesignViewModel vm, string root) NewVm()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-vol-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var schema = new VolumeDesignSchema();
        var adapter = new ModuleDataAdapter<VolumeDesignCategory, VolumeDesignData>(schema, root);
        return (new VolumeDesignViewModel(adapter), root);
    }
}
