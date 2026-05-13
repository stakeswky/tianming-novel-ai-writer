using TM.Services.Modules.ProjectData.Modules.Design.WorldRules;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Xunit;

namespace Tianming.ProjectData.Tests.Modules.Design;

public class WorldRulesSchemaTests
{
    [Fact]
    public void Fields_count_matches_form_design()
    {
        var schema = new WorldRulesSchema();
        Assert.Equal(11, schema.Fields.Count);
    }

    [Fact]
    public void PageTitle_and_relative_path_are_set()
    {
        var schema = new WorldRulesSchema();
        Assert.Equal("世界观规则", schema.PageTitle);
        Assert.Equal("Design/GlobalSettings/WorldRules", schema.ModuleRelativePath);
    }

    [Fact]
    public void CreateNewItem_returns_data_with_data_id_prefix()
    {
        var schema = new WorldRulesSchema();
        var item = schema.CreateNewItem();
        Assert.StartsWith("D", item.Id);
        Assert.True(item.IsEnabled);
    }

    [Fact]
    public void CreateNewCategory_uses_supplied_name_and_category_prefix()
    {
        var schema = new WorldRulesSchema();
        var cat = schema.CreateNewCategory("修真大陆");
        Assert.Equal("修真大陆", cat.Name);
        Assert.StartsWith("C", cat.Id);
        Assert.True(cat.IsEnabled);
    }

    [Fact]
    public void Name_field_is_required()
    {
        var schema = new WorldRulesSchema();
        var name = schema.Fields.Single(f => f.PropertyName == "Name");
        Assert.True(name.Required);
    }
}
