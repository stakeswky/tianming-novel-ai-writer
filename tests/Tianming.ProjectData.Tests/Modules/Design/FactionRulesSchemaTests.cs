using TM.Services.Modules.ProjectData.Modules.Design.FactionRules;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Xunit;

namespace Tianming.ProjectData.Tests.Modules.Design;

public class FactionRulesSchemaTests
{
    [Fact]
    public void Fields_count_matches_form_design()
    {
        var schema = new FactionRulesSchema();
        Assert.Equal(10, schema.Fields.Count);
    }

    [Fact]
    public void PageTitle_and_relative_path_are_set()
    {
        var schema = new FactionRulesSchema();
        Assert.Equal("势力规则", schema.PageTitle);
        Assert.Equal("Design/Elements/Factions", schema.ModuleRelativePath);
    }

    [Fact]
    public void CreateNewItem_returns_data_with_data_id_prefix()
    {
        var schema = new FactionRulesSchema();
        var item = schema.CreateNewItem();
        Assert.StartsWith("D", item.Id);
        Assert.True(item.IsEnabled);
    }

    [Fact]
    public void CreateNewCategory_uses_supplied_name_and_category_prefix()
    {
        var schema = new FactionRulesSchema();
        var cat = schema.CreateNewCategory("宗门");
        Assert.Equal("宗门", cat.Name);
        Assert.StartsWith("C", cat.Id);
    }
}
