using TM.Services.Modules.ProjectData.Modules.Design.PlotRules;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Xunit;

namespace Tianming.ProjectData.Tests.Modules.Design;

public class PlotRulesSchemaTests
{
    [Fact]
    public void Fields_count_matches_form_design()
    {
        var schema = new PlotRulesSchema();
        Assert.Equal(20, schema.Fields.Count);
    }

    [Fact]
    public void PageTitle_and_relative_path_are_set()
    {
        var schema = new PlotRulesSchema();
        Assert.Equal("剧情规则", schema.PageTitle);
        Assert.Equal("Design/Elements/Plot", schema.ModuleRelativePath);
    }

    [Fact]
    public void CreateNewItem_returns_data_with_data_id_prefix()
    {
        var schema = new PlotRulesSchema();
        var item = schema.CreateNewItem();
        Assert.StartsWith("D", item.Id);
        Assert.True(item.IsEnabled);
    }

    [Fact]
    public void CreateNewCategory_uses_supplied_name_and_category_prefix()
    {
        var schema = new PlotRulesSchema();
        var cat = schema.CreateNewCategory("主线事件");
        Assert.Equal("主线事件", cat.Name);
        Assert.StartsWith("C", cat.Id);
    }
}
