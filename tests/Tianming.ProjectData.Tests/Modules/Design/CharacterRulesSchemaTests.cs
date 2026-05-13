using TM.Services.Modules.ProjectData.Modules.Design.CharacterRules;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Xunit;

namespace Tianming.ProjectData.Tests.Modules.Design;

public class CharacterRulesSchemaTests
{
    [Fact]
    public void Fields_count_matches_form_design()
    {
        var schema = new CharacterRulesSchema();
        Assert.Equal(20, schema.Fields.Count);
    }

    [Fact]
    public void PageTitle_and_relative_path_are_set()
    {
        var schema = new CharacterRulesSchema();
        Assert.Equal("角色规则", schema.PageTitle);
        Assert.Equal("Design/Elements/Characters", schema.ModuleRelativePath);
    }

    [Fact]
    public void CreateNewItem_returns_data_with_data_id_prefix()
    {
        var schema = new CharacterRulesSchema();
        var item = schema.CreateNewItem();
        Assert.StartsWith("D", item.Id);
        Assert.True(item.IsEnabled);
    }

    [Fact]
    public void CreateNewCategory_uses_supplied_name_and_category_prefix()
    {
        var schema = new CharacterRulesSchema();
        var cat = schema.CreateNewCategory("主角组");
        Assert.Equal("主角组", cat.Name);
        Assert.StartsWith("C", cat.Id);
    }

    [Fact]
    public void CharacterType_field_is_enum_with_5_options()
    {
        var schema = new CharacterRulesSchema();
        var ct = schema.Fields.Single(f => f.PropertyName == "CharacterType");
        Assert.Equal(FieldType.Enum, ct.Type);
        Assert.NotNull(ct.EnumOptions);
        Assert.Equal(5, ct.EnumOptions!.Count);
        Assert.Contains("主角", ct.EnumOptions);
    }
}
