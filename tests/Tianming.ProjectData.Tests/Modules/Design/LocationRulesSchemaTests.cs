using TM.Services.Modules.ProjectData.Modules.Design.LocationRules;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Xunit;

namespace Tianming.ProjectData.Tests.Modules.Design;

public class LocationRulesSchemaTests
{
    [Fact]
    public void Fields_count_matches_form_design()
    {
        var schema = new LocationRulesSchema();
        Assert.Equal(11, schema.Fields.Count);
    }

    [Fact]
    public void PageTitle_and_relative_path_are_set()
    {
        var schema = new LocationRulesSchema();
        Assert.Equal("地点规则", schema.PageTitle);
        Assert.Equal("Design/Elements/Locations", schema.ModuleRelativePath);
    }

    [Fact]
    public void Landmarks_Resources_Dangers_are_tags()
    {
        var schema = new LocationRulesSchema();
        Assert.Equal(FieldType.Tags, schema.Fields.Single(f => f.PropertyName == "Landmarks").Type);
        Assert.Equal(FieldType.Tags, schema.Fields.Single(f => f.PropertyName == "Resources").Type);
        Assert.Equal(FieldType.Tags, schema.Fields.Single(f => f.PropertyName == "Dangers").Type);
    }

    [Fact]
    public void CreateNewItem_returns_data_with_data_id_prefix()
    {
        var schema = new LocationRulesSchema();
        var item = schema.CreateNewItem();
        Assert.StartsWith("D", item.Id);
        Assert.True(item.IsEnabled);
    }

    [Fact]
    public void CreateNewCategory_uses_supplied_name_and_category_prefix()
    {
        var schema = new LocationRulesSchema();
        var cat = schema.CreateNewCategory("山脉");
        Assert.Equal("山脉", cat.Name);
        Assert.StartsWith("C", cat.Id);
    }
}
