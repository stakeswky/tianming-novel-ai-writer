using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Generate.StrategicOutline;
using TM.Services.Modules.ProjectData.Modules.Generate.Outline;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels.Generate;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Generate;

public class OutlineViewModelTests
{
    [Fact]
    public void PageTitle_and_icon_proxy_to_schema()
    {
        var (vm, _) = NewVm();
        Assert.Equal("战略大纲", vm.PageTitle);
        Assert.Equal("📖", vm.PageIcon);
    }

    [Fact]
    public void Fields_include_TotalChapterCount_as_Number()
    {
        var (vm, _) = NewVm();
        var f = vm.Fields.Single(x => x.PropertyName == "TotalChapterCount");
        Assert.Equal(FieldType.Number, f.Type);
    }

    [Fact]
    public async Task AddCategory_then_AddItem_then_set_TotalChapterCount_persists()
    {
        var (vm, _) = NewVm();
        await vm.LoadAsync();
        await vm.AddCategoryAsync("主线");
        vm.SelectedCategory = vm.Categories[0];
        await vm.AddNewItemInCurrentCategoryCommand.ExecuteAsync(null);
        vm.SelectedItem!.Name = "山河长安·正稿";
        vm.SelectedItem.TotalChapterCount = 60;
        vm.SelectedItem.OneLineOutline = "凡人逆天改命";

        await vm.UpdateSelectedItemCommand.ExecuteAsync(null);
        await vm.LoadAsync();

        var loaded = vm.Items.Single(i => i.Name == "山河长安·正稿");
        Assert.Equal(60, loaded.TotalChapterCount);
        Assert.Equal("凡人逆天改命", loaded.OneLineOutline);
    }

    private static (OutlineViewModel vm, string root) NewVm()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-out-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var schema = new OutlineSchema();
        var adapter = new ModuleDataAdapter<OutlineCategory, OutlineData>(schema, root);
        return (new OutlineViewModel(adapter), root);
    }
}
