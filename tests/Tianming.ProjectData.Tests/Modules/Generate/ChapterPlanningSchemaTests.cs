using TM.Services.Modules.ProjectData.Modules.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Xunit;

namespace Tianming.ProjectData.Tests.Modules.Generate;

public class ChapterPlanningSchemaTests
{
    [Fact]
    public void Fields_count_matches_form_design()
    {
        var schema = new ChapterPlanningSchema();
        Assert.Equal(18, schema.Fields.Count);
    }

    [Fact]
    public void PageTitle_and_relative_path_are_set()
    {
        var schema = new ChapterPlanningSchema();
        Assert.Equal("章节规划", schema.PageTitle);
        Assert.Equal("Generate/ChapterPlanning", schema.ModuleRelativePath);
    }

    [Fact]
    public void ChapterNumber_is_number()
    {
        var schema = new ChapterPlanningSchema();
        Assert.Equal(FieldType.Number, schema.Fields.Single(f => f.PropertyName == "ChapterNumber").Type);
    }

    [Fact]
    public void CreateNewItem_returns_data_with_data_id_prefix()
    {
        var schema = new ChapterPlanningSchema();
        var item = schema.CreateNewItem();
        Assert.StartsWith("D", item.Id);
        Assert.True(item.IsEnabled);
    }

    [Fact]
    public void CreateNewCategory_uses_supplied_name_and_category_prefix()
    {
        var schema = new ChapterPlanningSchema();
        var cat = schema.CreateNewCategory("主线章");
        Assert.Equal("主线章", cat.Name);
        Assert.StartsWith("C", cat.Id);
    }
}
