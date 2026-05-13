using System.Collections.Generic;
using System.Linq;
using TM.Framework.Common.Helpers.Id;
using TM.Services.Modules.ProjectData.Models.Design.Factions;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace TM.Services.Modules.ProjectData.Modules.Design.FactionRules;

public sealed class FactionRulesSchema : IModuleSchema<FactionRulesCategory, FactionRulesData>
{
    public string PageTitle => "势力规则";
    public string PageIcon => "⚔️";
    public string ModuleRelativePath => "Design/Elements/Factions";

    public IReadOnlyList<FieldDescriptor> Fields { get; } = new[]
    {
        new FieldDescriptor("Name",               "势力名称",   FieldType.SingleLineText, true,  "如：青云宗"),
        new FieldDescriptor("FactionType",        "势力类型",   FieldType.SingleLineText, false, "宗门 / 帮派 / 王国 / 商会..."),
        new FieldDescriptor("Goal",               "核心目标",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("StrengthTerritory",  "实力/地盘", FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Leader",             "首领",       FieldType.SingleLineText, false, null),
        new FieldDescriptor("CoreMembers",        "核心成员",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("MemberTraits",       "成员特征",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Allies",             "盟友",       FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Enemies",            "敌对",       FieldType.MultiLineText,  false, null),
        new FieldDescriptor("NeutralCompetitors", "中立竞争者", FieldType.MultiLineText,  false, null),
    };

    public FactionRulesData CreateNewItem() => new()
    {
        Id = ShortIdGenerator.New("D"),
        Name = string.Empty,
        IsEnabled = true,
    };

    public FactionRulesCategory CreateNewCategory(string name) => new()
    {
        Id = ShortIdGenerator.New("C"),
        Name = name,
        Icon = "⚔️",
        Level = 1,
        Order = 0,
        IsBuiltIn = false,
        IsEnabled = true,
    };

    public string BuildAIPromptContext(IReadOnlyList<FactionRulesData> existing)
        => string.Join("\n---\n", existing.Select(x => $"{x.Name} ({x.FactionType}): {x.Goal}"));
}
