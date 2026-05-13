using System.Collections.Generic;
using System.Linq;
using TM.Framework.Common.Helpers.Id;
using TM.Services.Modules.ProjectData.Models.Design.Location;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace TM.Services.Modules.ProjectData.Modules.Design.LocationRules;

public sealed class LocationRulesSchema : IModuleSchema<LocationRulesCategory, LocationRulesData>
{
    public string PageTitle => "地点规则";
    public string PageIcon => "📍";
    public string ModuleRelativePath => "Design/Elements/Locations";

    public IReadOnlyList<FieldDescriptor> Fields { get; } = new[]
    {
        new FieldDescriptor("Name",                  "地点名称",     FieldType.SingleLineText, true,  "如：青云山"),
        new FieldDescriptor("LocationType",          "地点类型",     FieldType.SingleLineText, false, "城市/山脉/秘境..."),
        new FieldDescriptor("Description",           "描述",         FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Scale",                 "规模",         FieldType.SingleLineText, false, null),
        new FieldDescriptor("Terrain",               "地形",         FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Climate",               "气候",         FieldType.SingleLineText, false, null),
        new FieldDescriptor("Landmarks",             "地标",         FieldType.Tags,           false, "逗号分隔"),
        new FieldDescriptor("Resources",             "资源",         FieldType.Tags,           false, "逗号分隔"),
        new FieldDescriptor("HistoricalSignificance","历史意义",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Dangers",               "危险",         FieldType.Tags,           false, "逗号分隔"),
        new FieldDescriptor("FactionId",             "所属势力 ID",  FieldType.SingleLineText, false, null),
    };

    public LocationRulesData CreateNewItem() => new()
    {
        Id = ShortIdGenerator.New("D"),
        Name = string.Empty,
        IsEnabled = true,
    };

    public LocationRulesCategory CreateNewCategory(string name) => new()
    {
        Id = ShortIdGenerator.New("C"),
        Name = name,
        Icon = "📍",
        Level = 1,
        Order = 0,
        IsBuiltIn = false,
        IsEnabled = true,
    };

    public string BuildAIPromptContext(IReadOnlyList<LocationRulesData> existing)
        => string.Join("\n---\n", existing.Select(x => $"{x.Name} ({x.LocationType}): {x.Description}"));
}
