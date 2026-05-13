using TM.Services.Modules.ProjectData.Modules.Generate.Outline;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Xunit;

namespace Tianming.ProjectData.Tests.Modules.Generate;

public class OutlineSchemaTests
{
    [Fact]
    public void Fields_count_matches_form_design()
    {
        var schema = new OutlineSchema();
        Assert.Equal(11, schema.Fields.Count);
    }

    [Fact]
    public void PageTitle_and_relative_path_are_set()
    {
        var schema = new OutlineSchema();
        Assert.Equal("战略大纲", schema.PageTitle);
        Assert.Equal("Generate/StrategicOutline", schema.ModuleRelativePath);
    }

    [Fact]
    public void TotalChapterCount_is_number()
    {
        var schema = new OutlineSchema();
        Assert.Equal(FieldType.Number, schema.Fields.Single(f => f.PropertyName == "TotalChapterCount").Type);
    }

    [Fact]
    public void CreateNewItem_returns_data_with_data_id_prefix()
    {
        var schema = new OutlineSchema();
        var item = schema.CreateNewItem();
        Assert.StartsWith("D", item.Id);
        Assert.True(item.IsEnabled);
    }

    [Fact]
    public void CreateNewCategory_uses_supplied_name_and_category_prefix()
    {
        var schema = new OutlineSchema();
        var cat = schema.CreateNewCategory("正稿");
        Assert.Equal("正稿", cat.Name);
        Assert.StartsWith("C", cat.Id);
    }
}
