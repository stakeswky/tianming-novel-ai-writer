using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Modules.Design.CharacterRules;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Tianming.Desktop.Avalonia.ViewModels.Design;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.Design;

public class CharacterRulesViewModelTests
{
    [Fact]
    public void Fields_include_CharacterType_enum()
    {
        var (vm, _) = NewVm();
        var ct = vm.Fields.Single(f => f.PropertyName == "CharacterType");
        Assert.Equal(FieldType.Enum, ct.Type);
        Assert.NotNull(ct.EnumOptions);
        Assert.Contains("主角", ct.EnumOptions!);
    }

    [Fact]
    public async Task LoadAsync_with_seeded_character_populates_items()
    {
        var (vm, root) = NewVm();
        var schema = vm.Schema;
        var adapter = new ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData>(schema, root);
        await adapter.LoadAsync();
        await adapter.AddCategoryAsync(schema.CreateNewCategory("主角"));
        var c = schema.CreateNewItem();
        c.Name = "张三";
        c.Category = "主角";
        c.CharacterType = "主角";
        await adapter.AddAsync(c);

        await vm.LoadAsync();

        Assert.Single(vm.Items, i => i.Name == "张三");
    }

    [Fact]
    public async Task DeleteSelectedItemCommand_removes_character()
    {
        var (vm, _) = NewVm();
        await vm.LoadAsync();
        await vm.AddCategoryAsync("主角");
        vm.SelectedCategory = vm.Categories[0];
        await vm.AddNewItemInCurrentCategoryCommand.ExecuteAsync(null);
        vm.SelectedItem!.Name = "李四";
        await vm.UpdateSelectedItemCommand.ExecuteAsync(null);

        await vm.DeleteSelectedItemCommand.ExecuteAsync(null);

        Assert.Empty(vm.Items);
    }

    private static (CharacterRulesViewModel vm, string root) NewVm()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tm-cr-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var schema = new CharacterRulesSchema();
        var adapter = new ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData>(schema, root);
        return (new CharacterRulesViewModel(adapter), root);
    }
}
