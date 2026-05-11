using System.Linq;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class LedgerRuleSetProviderTests
{
    [Theory]
    [InlineData("玄幻修仙")]
    [InlineData("都市现实")]
    public void BuildRuleSetByGenre_enables_ability_loss_event_rules_for_supported_genres(string genre)
    {
        var ruleSet = LedgerRuleSetProvider.BuildRuleSetByGenre(genre);

        Assert.True(ruleSet.EnableAbilityLossRequiresEvent);
        Assert.Contains(ruleSet.AbilityLossKeywords, keyword => keyword.Contains("失去"));
    }

    [Fact]
    public void BuildRuleSetByGenre_returns_universal_defaults_for_unknown_genres()
    {
        var ruleSet = LedgerRuleSetProvider.BuildRuleSetByGenre("科幻");

        Assert.False(ruleSet.EnableAbilityLossRequiresEvent);
        Assert.True(ruleSet.EnableConflictFlowCheck);
        Assert.Equal(["pending", "active", "climax", "resolved"], ruleSet.ConflictStatusSequence);
    }
}
