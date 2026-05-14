using System.Collections.Generic;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Humanize;
using TM.Services.Modules.ProjectData.Humanize.Rules;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Generation;

public class HumanizePipelineIntegrationTests
{
    [Fact]
    public async Task PrepareStrictAsync_applies_humanize_and_canonicalize_before_gate_validation()
    {
        var pipeline = new HumanizePipeline(new IHumanizeRule[]
        {
            new PhraseReplaceRule(new Dictionary<string, string> { ["总而言之"] = string.Empty }),
        });
        var preparer = new ContentGenerationPreparer(
            new GenerationGate(new LedgerConsistencyChecker(), new LedgerRuleSetProvider()),
            pipeline);

        var result = await preparer.PrepareStrictAsync(
            "vol1_ch1",
            """
            总而言之，他赢了。
            ---CHANGES---
            {
              "角色状态变化": []
            }
            """,
            new FactSnapshot());

        Assert.True(result.GateResult.Success, string.Join("; ", result.GateResult.GetAllFailures()));
        Assert.Equal("# 第1章\n\n他赢了。", result.PersistedContent);
        Assert.NotNull(result.ParsedChanges);
    }
}
