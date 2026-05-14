using System.Collections.Generic;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Humanize;
using TM.Services.Modules.ProjectData.Humanize.Rules;
using Xunit;

namespace Tianming.ProjectData.Tests.Humanize;

public class HumanizePipelineTests
{
    [Fact]
    public async Task Pipeline_runs_rules_by_priority()
    {
        var pipeline = new HumanizePipeline(new IHumanizeRule[]
        {
            new PunctuationRule(),
            new PhraseReplaceRule(new Dictionary<string, string> { ["总而言之"] = string.Empty }),
        });
        var result = await pipeline.RunAsync("总而言之,他赢了.", new HumanizeContext());
        Assert.DoesNotContain("总而言之", result);
        Assert.Contains("。", result);
    }

    [Fact]
    public async Task Empty_pipeline_returns_input()
    {
        var pipeline = new HumanizePipeline(System.Array.Empty<IHumanizeRule>());
        var result = await pipeline.RunAsync("x", new HumanizeContext());
        Assert.Equal("x", result);
    }
}
