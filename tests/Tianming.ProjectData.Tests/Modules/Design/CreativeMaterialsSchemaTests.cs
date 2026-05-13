using TM.Services.Modules.ProjectData.Modules.Design.CreativeMaterials;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Xunit;

namespace Tianming.ProjectData.Tests.Modules.Design;

public class CreativeMaterialsSchemaTests
{
    [Fact]
    public void Fields_count_matches_form_design()
    {
        var schema = new CreativeMaterialsSchema();
        Assert.Equal(20, schema.Fields.Count);
    }

    [Fact]
    public void PageTitle_and_relative_path_are_set()
    {
        var schema = new CreativeMaterialsSchema();
        Assert.Equal("创意素材库", schema.PageTitle);
        Assert.Equal("Design/Templates/CreativeMaterials", schema.ModuleRelativePath);
    }

    [Fact]
    public void CreateNewItem_returns_data_with_data_id_prefix_and_default_icon()
    {
        var schema = new CreativeMaterialsSchema();
        var item = schema.CreateNewItem();
        Assert.StartsWith("D", item.Id);
        Assert.Equal("💡", item.Icon);
        Assert.True(item.IsEnabled);
    }

    [Fact]
    public void CreateNewCategory_uses_supplied_name_and_category_prefix()
    {
        var schema = new CreativeMaterialsSchema();
        var cat = schema.CreateNewCategory("仙侠");
        Assert.Equal("仙侠", cat.Name);
        Assert.StartsWith("C", cat.Id);
    }
}
