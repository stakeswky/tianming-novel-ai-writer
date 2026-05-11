using System;
using System.Collections.Generic;

namespace TM.Services.Modules.ProjectData.Implementations.Tracking.Rules
{
    public sealed class LedgerRuleSetProvider
    {
        public LedgerRuleSet GetRuleSetForGate()
        {
            return LedgerRuleSet.CreateUniversalDefault();
        }

        public static LedgerRuleSet BuildRuleSetByGenre(string? genre)
        {
            if (string.IsNullOrWhiteSpace(genre))
                return LedgerRuleSet.CreateUniversalDefault();

            var g = genre.Trim();

            if (ContainsAny(g, "玄幻", "奇幻", "仙侠", "武侠"))
            {
                var ruleSet = LedgerRuleSet.CreateUniversalDefault();
                ruleSet.EnableAbilityLossRequiresEvent = true;
                ruleSet.AbilityLossKeywords = new List<string>
                {
                    "失去", "封印", "封禁", "剥夺", "退化", "降级", "丧失", "消散", "废功", "废除", "失效"
                };
                return ruleSet;
            }

            if (ContainsAny(g, "都市", "现实"))
            {
                var ruleSet = LedgerRuleSet.CreateUniversalDefault();
                ruleSet.EnableAbilityLossRequiresEvent = true;
                ruleSet.AbilityLossKeywords = new List<string>
                {
                    "失去", "封号", "停职", "冻结", "权限回收", "撤职", "取消资格", "禁用", "作废", "失效", "剥夺", "降级"
                };
                return ruleSet;
            }

            return LedgerRuleSet.CreateUniversalDefault();
        }

        private static bool ContainsAny(string text, params string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(text) || keywords == null || keywords.Length == 0)
                return false;

            foreach (var k in keywords)
            {
                if (!string.IsNullOrWhiteSpace(k) && text.Contains(k, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }
}
