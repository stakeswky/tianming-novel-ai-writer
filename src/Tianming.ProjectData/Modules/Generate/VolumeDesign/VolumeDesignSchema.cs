using System.Collections.Generic;
using System.Linq;
using TM.Framework.Common.Helpers.Id;
using TM.Services.Modules.ProjectData.Models.Generate.VolumeDesign;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace TM.Services.Modules.ProjectData.Modules.Generate.VolumeDesign;

public sealed class VolumeDesignSchema : IModuleSchema<VolumeDesignCategory, VolumeDesignData>
{
    public string PageTitle => "分卷设计";
    public string PageIcon => "📚";
    public string ModuleRelativePath => "Generate/VolumeDesign";

    public IReadOnlyList<FieldDescriptor> Fields { get; } = new[]
    {
        new FieldDescriptor("Name",                       "卷名称",       FieldType.SingleLineText, true,  "如：第一卷·起势"),
        new FieldDescriptor("VolumeNumber",               "卷号",         FieldType.Number,         false, null),
        new FieldDescriptor("VolumeTitle",                "卷标题",       FieldType.SingleLineText, false, null),
        new FieldDescriptor("VolumeTheme",                "卷主题",       FieldType.MultiLineText,  false, null),
        new FieldDescriptor("StageGoal",                  "阶段目标",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("EstimatedWordCount",         "预计字数",     FieldType.SingleLineText, false, null),
        new FieldDescriptor("TargetChapterCount",         "目标章节数",   FieldType.Number,         false, null),
        new FieldDescriptor("StartChapter",               "起始章",       FieldType.Number,         false, null),
        new FieldDescriptor("EndChapter",                 "结束章",       FieldType.Number,         false, null),
        new FieldDescriptor("MainConflict",               "主要冲突",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("PressureSource",             "压力来源",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("KeyEvents",                  "关键事件",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("OpeningState",               "开篇状态",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("EndingState",                "终局状态",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("ReferencedCharacterNames",   "出场角色",     FieldType.Tags,           false, "逗号分隔"),
        new FieldDescriptor("ReferencedFactionNames",     "出场势力",     FieldType.Tags,           false, "逗号分隔"),
        new FieldDescriptor("ReferencedLocationNames",    "出场地点",     FieldType.Tags,           false, "逗号分隔"),
        new FieldDescriptor("ChapterAllocationOverview",  "章节分配概览", FieldType.MultiLineText,  false, null),
        new FieldDescriptor("PlotAllocation",             "剧情分配",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("ChapterGenerationHints",     "生成提示",     FieldType.MultiLineText,  false, null),
    };

    public VolumeDesignData CreateNewItem() => new()
    {
        Id = ShortIdGenerator.New("D"),
        Name = string.Empty,
        IsEnabled = true,
    };

    public VolumeDesignCategory CreateNewCategory(string name) => new()
    {
        Id = ShortIdGenerator.New("C"),
        Name = name,
        Icon = "📚",
        Level = 1,
        Order = 0,
        IsBuiltIn = false,
        IsEnabled = true,
    };

    public string BuildAIPromptContext(IReadOnlyList<VolumeDesignData> existing)
        => string.Join("\n---\n", existing.Select(x => $"第{x.VolumeNumber}卷 {x.VolumeTitle}: {x.StageGoal}"));
}
