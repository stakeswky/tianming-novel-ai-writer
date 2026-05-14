using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Humanize;
using TM.Services.Modules.ProjectData.Humanize.Rules;
using Xunit;

namespace Tianming.ProjectData.Tests.Humanize.Rules;

public class PunctuationRuleTests
{
    [Fact]
    public async Task Converts_halfwidth_punctuation_in_chinese_context()
    {
        var rule = new PunctuationRule();
        var output = await rule.ApplyAsync("他说,你好.今天怎么样?", new HumanizeContext());
        Assert.Contains("，", output);
        Assert.Contains("。", output);
        Assert.Contains("？", output);
    }

    [Fact]
    public async Task Preserves_punctuation_inside_arabic_numbers()
    {
        var rule = new PunctuationRule();
        var output = await rule.ApplyAsync("收入是 3,500.50 元。", new HumanizeContext());
        Assert.Contains("3,500.50", output);
    }
}
