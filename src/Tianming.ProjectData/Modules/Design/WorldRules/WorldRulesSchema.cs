using System.Collections.Generic;
using System.Linq;
using TM.Framework.Common.Helpers.Id;
using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace TM.Services.Modules.ProjectData.Modules.Design.WorldRules;

public sealed class WorldRulesSchema : IModuleSchema<WorldRulesCategory, WorldRulesData>
{
    public string PageTitle => "世界观规则";
    public string PageIcon => "🌍";
    public string ModuleRelativePath => "Design/GlobalSettings/WorldRules";

    public IReadOnlyList<FieldDescriptor> Fields { get; } = new[]
    {
        new FieldDescriptor("Name",           "规则名称",   FieldType.SingleLineText, true,  "如：九州大陆"),
        new FieldDescriptor("OneLineSummary", "一句话简介", FieldType.SingleLineText, false, null),
        new FieldDescriptor("PowerSystem",    "力量体系",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Cosmology",      "宇宙观",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("SpecialLaws",    "特殊法则",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("HardRules",      "硬性规则",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("SoftRules",      "软性规则",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("AncientEra",     "远古时期",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("KeyEvents",      "关键事件",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("ModernHistory",  "近代史",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("StatusQuo",      "当下格局",   FieldType.MultiLineText,  false, null),
    };

    public WorldRulesData CreateNewItem() => new()
    {
        Id = ShortIdGenerator.New("D"),
        Name = string.Empty,
        IsEnabled = true,
    };

    public WorldRulesCategory CreateNewCategory(string name) => new()
    {
        Id = ShortIdGenerator.New("C"),
        Name = name,
        Icon = "🌍",
        Level = 1,
        Order = 0,
        IsBuiltIn = false,
        IsEnabled = true,
    };

    public string BuildAIPromptContext(IReadOnlyList<WorldRulesData> existing)
        => string.Join("\n---\n", existing.Select(x => $"{x.Name}: {x.OneLineSummary}"));
}
