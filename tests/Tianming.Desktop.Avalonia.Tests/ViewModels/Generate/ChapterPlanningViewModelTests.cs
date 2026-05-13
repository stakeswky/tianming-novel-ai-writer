using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels.Generate;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Generate;

public class ChapterPlanningViewModelTests
{
    [Fact]
    public void PageTitle_is_章节规划()
    {
        var (vm, _) = NewVm();
        Assert.Equal("章节规划", vm.PageTitle);
    }

    [Fact]
    public void Fields_include_3_Tags_for_ReferencedNames()
    {
        var (vm, _) = NewVm();
        var tags = vm.Fields.Where(f => f.Type == FieldType.Tags).Select(f => f.PropertyName).ToList();
        Assert.Equal(3, tags.Count);
        Assert.Contains("ReferencedCharacterNames", tags);
        Assert.Contains("ReferencedFactionNames",   tags);
        Assert.Contains("ReferencedLocationNames",  tags);
    }

    [Fact]
    public async Task AddChapter_with_ChapterNumber_and_Tags_round_trips()
    {
        var (vm, _) = NewVm();
        await vm.LoadAsync();
        await vm.AddCategoryAsync("正稿");
        vm.SelectedCategory = vm.Categories[0];
        await vm.AddNewItemInCurrentCategoryCommand.ExecuteAsync(null);
        vm.SelectedItem!.Name = "第 1 章 风起青萍";
        vm.SelectedItem.ChapterNumber = 1;
        vm.SelectedItem.ChapterTitle = "风起青萍";
        vm.SelectedItem.MainGoal = "主角拜师";
        vm.SelectedItem.ReferencedCharacterNames = new() { "李无心", "张老师" };

        await vm.UpdateSelectedItemCommand.ExecuteAsync(null);
        await vm.LoadAsync();

        var loaded = vm.Items.Single(i => i.Name == "第 1 章 风起青萍");
        Assert.Equal(1, loaded.ChapterNumber);
        Assert.Equal("风起青萍", loaded.ChapterTitle);
        Assert.Equal(2, loaded.ReferencedCharacterNames.Count);
        Assert.Contains("李无心", loaded.ReferencedCharacterNames);
    }

    private static (ChapterPlanningViewModel vm, string root) NewVm()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-chp-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var schema = new ChapterPlanningSchema();
        var adapter = new ModuleDataAdapter<ChapterCategory, ChapterData>(schema, root);
        return (new ChapterPlanningViewModel(adapter), root);
    }
}
