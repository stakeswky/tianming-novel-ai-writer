using System.Collections.Generic;
using System.Linq;
using TM.Framework.Common.Helpers.Id;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterBlueprint;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace TM.Services.Modules.ProjectData.Modules.Generate.Blueprint;

public sealed class BlueprintSchema : IModuleSchema<BlueprintCategory, BlueprintData>
{
    public string PageTitle => "章节蓝图";
    public string PageIcon => "🎬";
    public string ModuleRelativePath => "Generate/ChapterBlueprint";

    public IReadOnlyList<FieldDescriptor> Fields { get; } = new[]
    {
        new FieldDescriptor("Name",                "蓝图名称",     FieldType.SingleLineText, true,  "用于内部识别"),
        new FieldDescriptor("ChapterId",           "对应章节 ID", FieldType.SingleLineText, false, null),
        new FieldDescriptor("OneLineStructure",    "一句话结构",   FieldType.SingleLineText, false, null),
        new FieldDescriptor("PacingCurve",         "节奏曲线",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("SceneNumber",         "场景号",       FieldType.Number,         false, null),
        new FieldDescriptor("SceneTitle",          "场景标题",     FieldType.SingleLineText, false, null),
        new FieldDescriptor("PovCharacter",        "视角人物",     FieldType.SingleLineText, false, null),
        new FieldDescriptor("EstimatedWordCount",  "预计字数",     FieldType.SingleLineText, false, null),
        new FieldDescriptor("Opening",             "开场",         FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Development",         "发展",         FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Turning",             "转折",         FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Ending",              "结尾",         FieldType.MultiLineText,  false, null),
        new FieldDescriptor("InfoDrop",            "信息投递",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Cast",                "登场角色",     FieldType.MultiLineText,  false, "ID 列表，逗号分隔"),
        new FieldDescriptor("Locations",           "场景地点",     FieldType.MultiLineText,  false, "ID 列表，逗号分隔"),
        new FieldDescriptor("Factions",            "涉及势力",     FieldType.MultiLineText,  false, "ID 列表，逗号分隔"),
        new FieldDescriptor("ItemsClues",          "物品/线索",   FieldType.MultiLineText,  false, "ID 列表，逗号分隔"),
    };

    public BlueprintData CreateNewItem() => new()
    {
        Id = ShortIdGenerator.New("D"),
        Name = string.Empty,
        IsEnabled = true,
    };

    public BlueprintCategory CreateNewCategory(string name) => new()
    {
        Id = ShortIdGenerator.New("C"),
        Name = name,
        Icon = "🎬",
        Level = 1,
        Order = 0,
        IsBuiltIn = false,
        IsEnabled = true,
    };

    public string BuildAIPromptContext(IReadOnlyList<BlueprintData> existing)
        => string.Join("\n---\n", existing.Select(x => $"{x.SceneTitle}: {x.OneLineStructure}"));
}
