using System.Collections.Generic;
using System.Linq;
using TM.Framework.Common.Helpers.Id;
using TM.Services.Modules.ProjectData.Models.Design.Plot;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace TM.Services.Modules.ProjectData.Modules.Design.PlotRules;

public sealed class PlotRulesSchema : IModuleSchema<PlotRulesCategory, PlotRulesData>
{
    public string PageTitle => "剧情规则";
    public string PageIcon => "📖";
    public string ModuleRelativePath => "Design/Elements/Plot";

    public IReadOnlyList<FieldDescriptor> Fields { get; } = new[]
    {
        new FieldDescriptor("Name",                 "事件名称",   FieldType.SingleLineText, true,  "如：青云山初会"),
        new FieldDescriptor("TargetVolume",         "目标卷",     FieldType.SingleLineText, false, null),
        new FieldDescriptor("AssignedVolume",       "实际卷",     FieldType.SingleLineText, false, null),
        new FieldDescriptor("OneLineSummary",       "一句话简介", FieldType.SingleLineText, false, null),
        new FieldDescriptor("EventType",            "事件类型",   FieldType.SingleLineText, false, "冲突/转折/铺垫..."),
        new FieldDescriptor("StoryPhase",           "故事阶段",   FieldType.SingleLineText, false, "开端/发展/高潮..."),
        new FieldDescriptor("PrerequisitesTrigger", "前置触发",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("MainCharacters",       "主要人物",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("KeyNpcs",              "关键 NPC",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Location",             "地点",       FieldType.SingleLineText, false, null),
        new FieldDescriptor("TimeDuration",         "时间持续",   FieldType.SingleLineText, false, null),
        new FieldDescriptor("StepTitle",            "步骤标题",   FieldType.SingleLineText, false, null),
        new FieldDescriptor("Goal",                 "目标",       FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Conflict",             "冲突",       FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Result",               "结果",       FieldType.MultiLineText,  false, null),
        new FieldDescriptor("EmotionCurve",         "情感曲线",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("MainPlotPush",         "主线推进",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("CharacterGrowth",      "角色成长",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("WorldReveal",          "世界揭示",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("RewardsClues",         "奖励/线索", FieldType.MultiLineText,  false, null),
    };

    public PlotRulesData CreateNewItem() => new()
    {
        Id = ShortIdGenerator.New("D"),
        Name = string.Empty,
        IsEnabled = true,
    };

    public PlotRulesCategory CreateNewCategory(string name) => new()
    {
        Id = ShortIdGenerator.New("C"),
        Name = name,
        Icon = "📖",
        Level = 1,
        Order = 0,
        IsBuiltIn = false,
        IsEnabled = true,
    };

    public string BuildAIPromptContext(IReadOnlyList<PlotRulesData> existing)
        => string.Join("\n---\n", existing.Select(x => $"{x.Name}: {x.OneLineSummary}"));
}
