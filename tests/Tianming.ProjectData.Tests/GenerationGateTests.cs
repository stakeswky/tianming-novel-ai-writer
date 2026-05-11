using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class GenerationGateTests
{
    [Fact]
    public async Task ValidateAsync_returns_protocol_failure_when_changes_region_is_missing()
    {
        var gate = CreateGate();

        var result = await gate.ValidateAsync("CH001", "只有正文，没有变更块。", new FactSnapshot());

        Assert.False(result.Success);
        Assert.Contains(result.Failures, failure => failure.Type == FailureType.Protocol);
    }

    [Fact]
    public async Task ValidateAsync_returns_consistency_failure_for_description_contradictions()
    {
        var gate = CreateGate();
        var snapshot = new FactSnapshot
        {
            CharacterDescriptions =
            {
                ["C7M3VT2K9P4NA"] = new CharacterCoreDescription
                {
                    Name = "林衡",
                    HairColor = "黑发"
                }
            }
        };

        var result = await gate.ValidateAsync(
            "CH001",
            "林衡的金发被山风吹乱。\n---CHANGES---\n" + EmptyChangesJson(),
            snapshot);

        Assert.False(result.Success);
        Assert.Contains(result.Failures, failure =>
            failure.Type == FailureType.Consistency &&
            failure.Errors.Any(error => error.Contains("发色矛盾")));
    }

    [Fact]
    public async Task ValidateAsync_succeeds_for_valid_changes_and_matching_content()
    {
        var gate = CreateGate();
        var snapshot = new FactSnapshot
        {
            CharacterDescriptions =
            {
                ["C7M3VT2K9P4NA"] = new CharacterCoreDescription
                {
                    Name = "林衡",
                    HairColor = "黑发"
                }
            }
        };

        var result = await gate.ValidateAsync(
            "CH001",
            "林衡束起黑发，沿着山路继续前行。\n---CHANGES---\n" + EmptyChangesJson(),
            snapshot);

        Assert.True(result.Success, string.Join("; ", result.GetAllFailures()));
        Assert.NotNull(result.ParsedChanges);
        Assert.Equal("林衡束起黑发，沿着山路继续前行。", result.ContentWithoutChanges);
    }

    private static GenerationGate CreateGate()
    {
        return new GenerationGate(new LedgerConsistencyChecker(), new LedgerRuleSetProvider());
    }

    private static string EmptyChangesJson()
    {
        return """
        {
          "CharacterStateChanges": [],
          "ConflictProgress": [],
          "ForeshadowingActions": [],
          "NewPlotPoints": [],
          "LocationStateChanges": [],
          "FactionStateChanges": [],
          "TimeProgression": { "TimePeriod": "第一日", "ElapsedTime": "片刻", "KeyTimeEvent": "启程", "Importance": "normal" },
          "CharacterMovements": [],
          "ItemTransfers": []
        }
        """;
    }
}
