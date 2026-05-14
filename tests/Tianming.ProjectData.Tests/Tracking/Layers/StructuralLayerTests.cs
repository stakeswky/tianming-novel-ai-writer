using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;
using TM.Services.Modules.ProjectData.Tracking.Layers;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Layers;

public class StructuralLayerTests
{
    [Fact]
    public async Task Returns_null_changes_issue_with_structural_layer()
    {
        var layer = new StructuralLayer();

        var issues = await layer.CheckAsync(null!, new FactSnapshot(), LedgerRuleSet.CreateUniversalDefault());

        var issue = Assert.Single(issues);
        Assert.Equal("Structural", issue.Layer);
        Assert.Equal("NullChanges", issue.IssueType);
    }

    [Fact]
    public async Task Flags_movement_chain_break_as_structural()
    {
        var layer = new StructuralLayer();
        var changes = new ChapterChanges
        {
            CharacterMovements =
            [
                new CharacterMovementChange
                {
                    CharacterId = "char-001",
                    FromLocation = "loc-a",
                    ToLocation = "loc-b"
                },
                new CharacterMovementChange
                {
                    CharacterId = "char-001",
                    FromLocation = "loc-c",
                    ToLocation = "loc-d"
                }
            ]
        };

        var issues = await layer.CheckAsync(changes, new FactSnapshot(), LedgerRuleSet.CreateUniversalDefault());

        var issue = Assert.Single(issues);
        Assert.Equal("Structural", issue.Layer);
        Assert.Equal("MovementChainBreak", issue.IssueType);
        Assert.Contains("loc-b", issue.Expected);
        Assert.Contains("loc-c", issue.Actual);
    }
}
