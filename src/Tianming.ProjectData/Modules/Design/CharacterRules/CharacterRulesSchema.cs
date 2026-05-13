using System.Collections.Generic;
using System.Linq;
using TM.Framework.Common.Helpers.Id;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace TM.Services.Modules.ProjectData.Modules.Design.CharacterRules;

public sealed class CharacterRulesSchema : IModuleSchema<CharacterRulesCategory, CharacterRulesData>
{
    public string PageTitle => "角色规则";
    public string PageIcon => "👤";
    public string ModuleRelativePath => "Design/Elements/Characters";

    public IReadOnlyList<FieldDescriptor> Fields { get; } = new[]
    {
        new FieldDescriptor("Name",                  "角色名称",     FieldType.SingleLineText, true,  "如：林衡"),
        new FieldDescriptor("CharacterType",         "角色类型",     FieldType.Enum,           false, null, EnumOptions: new[] { "主角", "主要角色", "重要配角", "次要配角", "龙套" }),
        new FieldDescriptor("Gender",                "性别",         FieldType.SingleLineText, false, null),
        new FieldDescriptor("Age",                   "年龄",         FieldType.SingleLineText, false, null),
        new FieldDescriptor("Identity",              "身份",         FieldType.SingleLineText, false, null),
        new FieldDescriptor("Race",                  "种族",         FieldType.SingleLineText, false, null),
        new FieldDescriptor("Appearance",            "外貌",         FieldType.MultiLineText,  false, null),
        new FieldDescriptor("Want",                  "外在目标 (Want)", FieldType.MultiLineText, false, null),
        new FieldDescriptor("Need",                  "内在需求 (Need)", FieldType.MultiLineText, false, null),
        new FieldDescriptor("FlawBelief",            "缺点/信念",    FieldType.MultiLineText,  false, null),
        new FieldDescriptor("GrowthPath",            "成长轨迹",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("TargetCharacterName",   "关系对象",     FieldType.SingleLineText, false, null),
        new FieldDescriptor("RelationshipType",      "关系类型",     FieldType.SingleLineText, false, null),
        new FieldDescriptor("EmotionDynamic",        "情感动态",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("CombatSkills",          "战斗技能",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("NonCombatSkills",       "非战斗技能",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("SpecialAbilities",      "特殊能力",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("SignatureItems",        "标志性物品",   FieldType.MultiLineText,  false, null),
        new FieldDescriptor("CommonItems",           "日常物品",     FieldType.MultiLineText,  false, null),
        new FieldDescriptor("PersonalAssets",        "个人资产",     FieldType.MultiLineText,  false, null),
    };

    public CharacterRulesData CreateNewItem() => new()
    {
        Id = ShortIdGenerator.New("D"),
        Name = string.Empty,
        IsEnabled = true,
    };

    public CharacterRulesCategory CreateNewCategory(string name) => new()
    {
        Id = ShortIdGenerator.New("C"),
        Name = name,
        Icon = "👤",
        Level = 1,
        Order = 0,
        IsBuiltIn = false,
        IsEnabled = true,
    };

    public string BuildAIPromptContext(IReadOnlyList<CharacterRulesData> existing)
        => string.Join("\n---\n", existing.Select(x => $"{x.Name} ({x.CharacterType}): {x.Identity}"));
}
