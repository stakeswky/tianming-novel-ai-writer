using TM.Services.Modules.ProjectData.Modules.Generate.Blueprint;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Xunit;

namespace Tianming.ProjectData.Tests.Modules.Generate;

public class BlueprintSchemaTests
{
    [Fact]
    public void Fields_count_matches_form_design()
    {
        var schema = new BlueprintSchema();
        Assert.Equal(17, schema.Fields.Count);
    }

    [Fact]
    public void PageTitle_and_relative_path_are_set()
    {
        var schema = new BlueprintSchema();
        Assert.Equal("章节蓝图", schema.PageTitle);
        Assert.Equal("Generate/ChapterBlueprint", schema.ModuleRelativePath);
    }

    [Fact]
    public void SceneNumber_is_number()
    {
        var schema = new BlueprintSchema();
        Assert.Equal(FieldType.Number, schema.Fields.Single(f => f.PropertyName == "SceneNumber").Type);
    }

    [Fact]
    public void CreateNewItem_returns_data_with_data_id_prefix()
    {
        var schema = new BlueprintSchema();
        var item = schema.CreateNewItem();
        Assert.StartsWith("D", item.Id);
        Assert.True(item.IsEnabled);
    }

    [Fact]
    public void CreateNewCategory_uses_supplied_name_and_category_prefix()
    {
        var schema = new BlueprintSchema();
        var cat = schema.CreateNewCategory("正常蓝图");
        Assert.Equal("正常蓝图", cat.Name);
        Assert.StartsWith("C", cat.Id);
    }
}
