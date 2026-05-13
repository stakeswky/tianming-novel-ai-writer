using System.Collections.Generic;
using System.Linq;
using TM.Framework.Common.Helpers.Id;
using TM.Services.Modules.ProjectData.Models.Design.Templates;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace TM.Services.Modules.ProjectData.Modules.Design.CreativeMaterials;

public sealed class CreativeMaterialsSchema : IModuleSchema<CreativeMaterialCategory, CreativeMaterialData>
{
    public string PageTitle => "创意素材库";
    public string PageIcon => "💡";
    public string ModuleRelativePath => "Design/Templates/CreativeMaterials";

    public IReadOnlyList<FieldDescriptor> Fields { get; } = new[]
    {
        new FieldDescriptor("Name",                   "素材名称",     FieldType.SingleLineText, true,  "如：仙侠开篇模板"),
        new FieldDescriptor("Icon",                   "图标",         FieldType.SingleLineText, false, "Emoji 或 Lucide 名"),
        new FieldDescriptor("SourceBookName",         "来源书名",     FieldType.SingleLineText, false, null),
        new FieldDescriptor("Genre",                  "题材",         FieldType.SingleLineText, false, "玄幻/都市/穿越..."),
        new FieldDescriptor("OverallIdea",            "整体构思",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("WorldBuildingMethod",    "建构方法",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("PowerSystemDesign",      "力量体系设计", FieldType.MultiLineText,  false, null),
        new FieldDescriptor("EnvironmentDescription", "环境描写",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("FactionDesign",          "势力设计",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("WorldviewHighlights",    "世界观亮点",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("ProtagonistDesign",      "主角设计",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("SupportingRoles",        "配角",         FieldType.MultiLineText,  false, null),
        new FieldDescriptor("CharacterRelations",     "人物关系",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("GoldenFingerDesign",     "金手指设计",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("CharacterHighlights",    "角色亮点",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("PlotStructure",          "情节结构",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("ConflictDesign",         "冲突设计",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("ClimaxArrangement",      "高潮安排",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("ForeshadowingTechnique", "铺垫技法",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("PlotHighlights",         "情节亮点",     FieldType.MultiLineText,  false, null),
    };

    public CreativeMaterialData CreateNewItem() => new()
    {
        Id = ShortIdGenerator.New("D"),
        Name = string.Empty,
        Icon = "💡",
        IsEnabled = true,
    };

    public CreativeMaterialCategory CreateNewCategory(string name) => new()
    {
        Id = ShortIdGenerator.New("C"),
        Name = name,
        Icon = "📁",
        Level = 1,
        Order = 0,
        IsBuiltIn = false,
        IsEnabled = true,
    };

    public string BuildAIPromptContext(IReadOnlyList<CreativeMaterialData> existing)
        => string.Join("\n---\n", existing.Select(x => $"{x.Name} ({x.Genre}): {x.OverallIdea}"));
}
