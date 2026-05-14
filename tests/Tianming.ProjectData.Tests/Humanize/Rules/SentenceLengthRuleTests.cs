using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Humanize;
using TM.Services.Modules.ProjectData.Humanize.Rules;
using Xunit;

namespace Tianming.ProjectData.Tests.Humanize.Rules;

public class SentenceLengthRuleTests
{
    [Fact]
    public async Task Inserts_break_after_three_consecutive_long_sentences()
    {
        var rule = new SentenceLengthRule(longThreshold: 30);
        var longSen = new string('字', 50);
        var input = $"{longSen}。{longSen}。{longSen}。短句。";
        var output = await rule.ApplyAsync(input, new HumanizeContext());
        Assert.Contains("\n\n", output);
    }

    [Fact]
    public async Task Short_sentences_unchanged()
    {
        var rule = new SentenceLengthRule(longThreshold: 30);
        var input = "短句一。短句二。短句三。";
        var output = await rule.ApplyAsync(input, new HumanizeContext());
        Assert.DoesNotContain("\n\n", output);
    }
}
