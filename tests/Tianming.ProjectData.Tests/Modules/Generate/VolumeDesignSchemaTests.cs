using TM.Services.Modules.ProjectData.Modules.Generate.VolumeDesign;
using TM.Services.Modules.ProjectData.Modules.Schema;
using Xunit;

namespace Tianming.ProjectData.Tests.Modules.Generate;

public class VolumeDesignSchemaTests
{
    [Fact]
    public void Fields_count_matches_form_design()
    {
        var schema = new VolumeDesignSchema();
        Assert.Equal(20, schema.Fields.Count);
    }

    [Fact]
    public void PageTitle_and_relative_path_are_set()
    {
        var schema = new VolumeDesignSchema();
        Assert.Equal("分卷设计", schema.PageTitle);
        Assert.Equal("Generate/VolumeDesign", schema.ModuleRelativePath);
    }

    [Fact]
    public void Numeric_fields_have_number_type()
    {
        var schema = new VolumeDesignSchema();
        Assert.Equal(FieldType.Number, schema.Fields.Single(f => f.PropertyName == "VolumeNumber").Type);
        Assert.Equal(FieldType.Number, schema.Fields.Single(f => f.PropertyName == "TargetChapterCount").Type);
    }

    [Fact]
    public void CreateNewItem_returns_data_with_data_id_prefix()
    {
        var schema = new VolumeDesignSchema();
        var item = schema.CreateNewItem();
        Assert.StartsWith("D", item.Id);
        Assert.True(item.IsEnabled);
    }

    [Fact]
    public void CreateNewCategory_uses_supplied_name_and_category_prefix()
    {
        var schema = new VolumeDesignSchema();
        var cat = schema.CreateNewCategory("第一卷");
        Assert.Equal("第一卷", cat.Name);
        Assert.StartsWith("C", cat.Id);
    }
}
