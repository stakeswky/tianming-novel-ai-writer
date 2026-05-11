using System.Collections.Generic;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ContentGenerationPreparerTests
{
    [Fact]
    public async Task PrepareStrictAsync_validates_and_normalizes_persisted_content()
    {
        var preparer = CreatePreparer();
        var rawContent = """
        <think>草稿推理不应落盘</think>
        # 临时标题

        林衡束起黑发，推开山门。
        ---CHANGES---
        {
          "CharacterStateChanges": [
            { "CharacterId": "C7M3VT2K9P4NA", "NewLevel": "A", "NewAbilities": [], "LostAbilities": [], "RelationshipChanges": {}, "NewMentalState": "镇定", "KeyEvent": "推开山门", "Importance": "important" }
          ],
          "ConflictProgress": [],
          "ForeshadowingActions": [],
          "NewPlotPoints": [
            { "Keywords": ["山门"], "Context": "林衡推开山门", "InvolvedCharacters": ["C7M3VT2K9P4NA"], "Importance": "normal", "Storyline": "main" }
          ],
          "LocationStateChanges": [],
          "FactionStateChanges": [],
          "TimeProgression": { "TimePeriod": "第一日", "ElapsedTime": "片刻", "KeyTimeEvent": "入山", "Importance": "normal" },
          "CharacterMovements": [],
          "ItemTransfers": []
        }
        """;

        var result = await preparer.PrepareStrictAsync(
            "vol1_ch12",
            rawContent,
            new FactSnapshot
            {
                CharacterStates = [new CharacterStateSnapshot { Id = "C7M3VT2K9P4NA", Name = "林衡", Stage = "B" }],
                CharacterDescriptions =
                {
                    ["C7M3VT2K9P4NA"] = new CharacterCoreDescription { Name = "林衡", HairColor = "黑发" }
                }
            },
            packagedTitle: "雪夜入山",
            entityNameMap: new Dictionary<string, string> { ["C7M3VT2K9P4NA"] = "林衡" });

        Assert.True(result.GateResult.Success, string.Join("; ", result.GateResult.GetAllFailures()));
        Assert.Equal("vol1_ch12", result.ChapterId);
        Assert.Equal("# 第12章 雪夜入山\n\n林衡束起黑发，推开山门。", result.PersistedContent);
        Assert.Contains("[角色]林衡: 推开山门", result.Summary);
        Assert.Contains("[情节]林衡推开山门", result.Summary);
        Assert.Contains("[时间]第一日 经过片刻", result.Summary);
    }

    [Fact]
    public async Task PrepareStrictAsync_returns_gate_failure_without_persisting_invalid_content()
    {
        var preparer = CreatePreparer();

        var result = await preparer.PrepareStrictAsync(
            "vol1_ch13",
            "只有正文，没有CHANGES。",
            new FactSnapshot(),
            packagedTitle: "失败样例");

        Assert.False(result.GateResult.Success);
        Assert.Empty(result.PersistedContent);
        Assert.Contains(result.GateResult.Failures, failure => failure.Type == FailureType.Protocol);
    }

    private static ContentGenerationPreparer CreatePreparer()
    {
        return new ContentGenerationPreparer(
            new GenerationGate(new LedgerConsistencyChecker(), new LedgerRuleSetProvider()));
    }
}
