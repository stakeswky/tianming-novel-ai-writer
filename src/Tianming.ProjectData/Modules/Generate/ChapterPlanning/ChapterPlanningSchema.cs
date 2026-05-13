using System.Collections.Generic;
using System.Linq;
using TM.Framework.Common.Helpers.Id;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace TM.Services.Modules.ProjectData.Modules.Generate.ChapterPlanning;

public sealed class ChapterPlanningSchema : IModuleSchema<ChapterCategory, ChapterData>
{
    public string PageTitle => "章节规划";
    public string PageIcon => "📑";
    public string ModuleRelativePath => "Generate/ChapterPlanning";

    public IReadOnlyList<FieldDescriptor> Fields { get; } = new[]
    {
        new FieldDescriptor("Name",                     "章节名称",     FieldType.SingleLineText, true,  "用于内部识别"),
        new FieldDescriptor("ChapterTitle",             "章节标题",     FieldType.SingleLineText, false, "正稿章节名"),
        new FieldDescriptor("ChapterNumber",            "章节号",       FieldType.Number,         false, null),
        new FieldDescriptor("Volume",                   "所属卷",       FieldType.SingleLineText, false, null),
        new FieldDescriptor("EstimatedWordCount",       "预计字数",     FieldType.SingleLineText, false, null),
        new FieldDescriptor("ChapterTheme",             "章节主题",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("ReaderExperienceGoal",     "读者体验目标", FieldType.MultiLineText,  false, null),
        new FieldDescriptor("MainGoal",                 "主要目标",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("ResistanceSource",         "阻力来源",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("KeyTurn",                  "关键转折",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Hook",                     "钩子",         FieldType.MultiLineText,  false, null),
        new FieldDescriptor("WorldInfoDrop",            "世界观投递",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("CharacterArcProgress",     "角色弧光推进", FieldType.MultiLineText,  false, null),
        new FieldDescriptor("MainPlotProgress",         "主线推进",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Foreshadowing",            "铺垫",         FieldType.MultiLineText,  false, null),
        new FieldDescriptor("ReferencedCharacterNames", "出场角色",     FieldType.Tags,           false, "逗号分隔"),
        new FieldDescriptor("ReferencedFactionNames",   "出场势力",     FieldType.Tags,           false, "逗号分隔"),
        new FieldDescriptor("ReferencedLocationNames",  "出场地点",     FieldType.Tags,           false, "逗号分隔"),
    };

    public ChapterData CreateNewItem() => new()
    {
        Id = ShortIdGenerator.New("D"),
        Name = string.Empty,
        IsEnabled = true,
    };

    public ChapterCategory CreateNewCategory(string name) => new()
    {
        Id = ShortIdGenerator.New("C"),
        Name = name,
        Icon = "📑",
        Level = 1,
        Order = 0,
        IsBuiltIn = false,
        IsEnabled = true,
    };

    public string BuildAIPromptContext(IReadOnlyList<ChapterData> existing)
        => string.Join("\n---\n", existing.Select(x => $"第{x.ChapterNumber}章 {x.ChapterTitle}: {x.MainGoal}"));
}
