using System.Collections.Generic;
using System.Linq;
using TM.Framework.Common.Helpers.Id;
using TM.Services.Modules.ProjectData.Models.Generate.StrategicOutline;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace TM.Services.Modules.ProjectData.Modules.Generate.Outline;

public sealed class OutlineSchema : IModuleSchema<OutlineCategory, OutlineData>
{
    public string PageTitle => "战略大纲";
    public string PageIcon => "📖";
    public string ModuleRelativePath => "Generate/StrategicOutline";

    public IReadOnlyList<FieldDescriptor> Fields { get; } = new[]
    {
        new FieldDescriptor("Name",                "大纲名称",     FieldType.SingleLineText, true,  "如：山河长安·正稿"),
        new FieldDescriptor("TotalChapterCount",   "总章节数",     FieldType.Number,         false, null),
        new FieldDescriptor("EstimatedWordCount",  "预计字数",     FieldType.SingleLineText, false, "如：120 万"),
        new FieldDescriptor("OneLineOutline",      "一句话大纲",   FieldType.SingleLineText, false, null),
        new FieldDescriptor("EmotionalTone",       "情感基调",     FieldType.SingleLineText, false, null),
        new FieldDescriptor("PhilosophicalMotif",  "哲学母题",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Theme",               "主题",         FieldType.MultiLineText,  false, null),
        new FieldDescriptor("CoreConflict",        "核心冲突",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("EndingState",         "终局状态",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("VolumeDivision",      "分卷划分",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("OutlineOverview",     "大纲概览",     FieldType.MultiLineText,  false, null),
    };

    public OutlineData CreateNewItem() => new()
    {
        Id = ShortIdGenerator.New("D"),
        Name = string.Empty,
        IsEnabled = true,
    };

    public OutlineCategory CreateNewCategory(string name) => new()
    {
        Id = ShortIdGenerator.New("C"),
        Name = name,
        Icon = "📖",
        Level = 1,
        Order = 0,
        IsBuiltIn = false,
        IsEnabled = true,
    };

    public string BuildAIPromptContext(IReadOnlyList<OutlineData> existing)
        => string.Join("\n---\n", existing.Select(x => $"{x.Name}: {x.OneLineOutline}"));
}
