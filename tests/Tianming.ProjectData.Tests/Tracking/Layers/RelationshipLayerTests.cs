using System.Collections.Generic;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;
using TM.Services.Modules.ProjectData.Tracking.Layers;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Layers;

public class RelationshipLayerTests
{
    [Fact]
    public async Task Flags_trust_delta_over_limit()
    {
        var layer = new RelationshipLayer();
        var changes = new ChapterChanges
        {
            CharacterStateChanges =
            [
                new CharacterStateChange
                {
                    CharacterId = "char-001",
                    RelationshipChanges = new Dictionary<string, RelationshipChange>
                    {
                        ["char-002"] = new() { TrustDelta = 99 }
                    }
                }
            ]
        };

        var issues = await layer.CheckAsync(changes, new FactSnapshot(), LedgerRuleSet.CreateUniversalDefault());

        var issue = Assert.Single(issues);
        Assert.Equal("Relationship", issue.Layer);
        Assert.Equal("TrustDeltaExceedsLimit", issue.IssueType);
    }

    [Fact]
    public async Task Flags_relationship_contradictions()
    {
        var layer = new RelationshipLayer();
        var changes = new ChapterChanges
        {
            CharacterStateChanges =
            [
                new CharacterStateChange
                {
                    CharacterId = "char-001",
                    RelationshipChanges = new Dictionary<string, RelationshipChange>
                    {
                        ["char-002"] = new() { Relation = "盟友" }
                    }
                },
                new CharacterStateChange
                {
                    CharacterId = "char-002",
                    RelationshipChanges = new Dictionary<string, RelationshipChange>
                    {
                        ["char-001"] = new() { Relation = "enemy" }
                    }
                }
            ]
        };

        var issues = await layer.CheckAsync(changes, new FactSnapshot(), LedgerRuleSet.CreateUniversalDefault());

        var issue = Assert.Single(issues);
        Assert.Equal("Relationship", issue.Layer);
        Assert.Equal("RelationshipContradiction", issue.IssueType);
    }
}
