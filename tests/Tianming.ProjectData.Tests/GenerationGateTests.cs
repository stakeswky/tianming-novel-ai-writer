using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;
using TM.Services.Modules.ProjectData.Tracking.Layers;
using TM.Services.Modules.ProjectData.Tracking.Locator;
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

    [Fact]
    public async Task ValidateAsync_appends_layered_issues_when_optional_services_are_present()
    {
        var layered = new LayeredConsistencyChecker([new StubConsistencyLayer()]);
        var locator = new ConsistencyIssueLocator(new StubVectorSearchService());
        var gate = new GenerationGate(new LedgerConsistencyChecker(), new LedgerRuleSetProvider(), layered, locator);

        var result = await gate.ValidateAsync(
            "CH001",
            "林衡束起黑发，沿着山路继续前行。\n---CHANGES---\n" + EmptyChangesJson(),
            new FactSnapshot());

        Assert.True(result.Success, string.Join("; ", result.GetAllFailures()));
        var issue = Assert.Single(result.LayeredIssues!);
        Assert.Equal("Entity", issue.Layer);
        Assert.Equal(4, issue.ChunkPosition);
        Assert.InRange(issue.VectorScore, 0.74d, 0.76d);
    }

    [Fact]
    public async Task ValidateAsync_preserves_layered_issues_when_legacy_consistency_fails()
    {
        var layered = new LayeredConsistencyChecker([new StubConsistencyLayer()]);
        var locator = new ConsistencyIssueLocator(new StubVectorSearchService());
        var gate = new GenerationGate(new LedgerConsistencyChecker(), new LedgerRuleSetProvider(), layered, locator);
        var snapshot = new FactSnapshot
        {
            CharacterStates =
            [
                new CharacterStateSnapshot
                {
                    Id = "C7M3VT2K9P4NA",
                    Stage = "A"
                }
            ]
        };

        var result = await gate.ValidateAsync(
            "CH001",
            "林衡束起黑发，沿着山路继续前行。\n---CHANGES---\n" + LevelRegressionChangesJson(),
            snapshot);

        Assert.False(result.Success);
        Assert.Contains(result.Failures, failure =>
            failure.Type == FailureType.Consistency &&
            failure.Errors.Any(error => error.Contains("LevelRegression")));
        var issue = Assert.Single(result.LayeredIssues!);
        Assert.Equal("Entity", issue.Layer);
        Assert.Equal(4, issue.ChunkPosition);
    }

    private static GenerationGate CreateGate()
    {
        return new GenerationGate(new LedgerConsistencyChecker(), new LedgerRuleSetProvider());
    }

    private sealed class StubConsistencyLayer : IConsistencyLayer
    {
        public string LayerName => "Entity";

        public Task<IReadOnlyList<ConsistencyIssue>> CheckAsync(
            ChapterChanges changes,
            FactSnapshot factSnapshot,
            LedgerRuleSet ruleSet,
            System.Threading.CancellationToken ct = default)
        {
            IReadOnlyList<ConsistencyIssue> issues =
            [
                new()
                {
                    EntityId = "char-001",
                    IssueType = "LevelRegression",
                    Layer = LayerName
                }
            ];
            return Task.FromResult(issues);
        }
    }

    private sealed class StubVectorSearchService : IVectorSearchService
    {
        public VectorSearchMode CurrentMode => VectorSearchMode.Keyword;

        public Task<System.Collections.Generic.List<VectorSearchResult>> SearchAsync(string query, int topK = 5)
        {
            return Task.FromResult(new System.Collections.Generic.List<VectorSearchResult>
            {
                new()
                {
                    ChapterId = "CH001",
                    Position = 4,
                    Content = "char-001 在这一段出场。",
                    Score = 0.75d
                }
            });
        }

        public Task<System.Collections.Generic.List<VectorSearchResult>> SearchByChapterAsync(string chapterId, int topK = 2)
        {
            return Task.FromResult(new System.Collections.Generic.List<VectorSearchResult>
            {
                new()
                {
                    ChapterId = chapterId,
                    Position = 4,
                    Content = "char-001 在这一段出场。",
                    Score = 0.75d
                }
            });
        }
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

    private static string LevelRegressionChangesJson()
    {
        return """
        {
              "CharacterStateChanges": [
                {
              "CharacterId": "C7M3VT2K9P4NA",
              "NewLevel": "B",
              "KeyEvent": "",
              "RelationshipChanges": {}
            }
          ],
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
