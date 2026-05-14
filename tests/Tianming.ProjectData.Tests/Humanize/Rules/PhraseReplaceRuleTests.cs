using System.Collections.Generic;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Humanize;
using TM.Services.Modules.ProjectData.Humanize.Rules;
using Xunit;

namespace Tianming.ProjectData.Tests.Humanize.Rules;

public class PhraseReplaceRuleTests
{
    [Fact]
    public async Task Replaces_first_match_per_phrase_pair()
    {
        var rule = new PhraseReplaceRule(new Dictionary<string, string>
        {
            ["总而言之"] = "",
            ["综上所述"] = "",
            ["不可否认"] = "",
        });
        var ctx = new HumanizeContext { InputText = "总而言之，他赢了。综上所述，未来可期。" };
        var output = await rule.ApplyAsync(ctx.InputText, ctx);
        Assert.DoesNotContain("总而言之", output);
        Assert.DoesNotContain("综上所述", output);
    }

    [Fact]
    public async Task Empty_dict_returns_input_unchanged()
    {
        var rule = new PhraseReplaceRule(new Dictionary<string, string>());
        var output = await rule.ApplyAsync("any text", new HumanizeContext());
        Assert.Equal("any text", output);
    }

    [Fact]
    public void Has_name_and_priority()
    {
        var rule = new PhraseReplaceRule(new Dictionary<string, string>());
        Assert.Equal("PhraseReplace", rule.Name);
        Assert.Equal(10, rule.Priority);
    }
}
