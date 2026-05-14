using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;
using TM.Services.Modules.ProjectData.Tracking.Layers;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Layers;

public class EntityLayerTests
{
    [Fact]
    public async Task Flags_level_regression_without_loss_event()
    {
        var layer = new EntityLayer();
        var changes = new ChapterChanges
        {
            CharacterStateChanges =
            [
                new CharacterStateChange
                {
                    CharacterId = "char-001",
                    NewLevel = "B",
                    KeyEvent = string.Empty
                }
            ]
        };
        var snapshot = new FactSnapshot
        {
            CharacterStates =
            [
                new CharacterStateSnapshot
                {
                    Id = "char-001",
                    Stage = "A"
                }
            ]
        };

        var issues = await layer.CheckAsync(changes, snapshot, LedgerRuleSet.CreateUniversalDefault());

        var issue = Assert.Single(issues);
        Assert.Equal("Entity", issue.Layer);
        Assert.Equal("LevelRegression", issue.IssueType);
    }

    [Fact]
    public async Task Flags_item_holder_mismatch_against_snapshot()
    {
        var layer = new EntityLayer();
        var changes = new ChapterChanges
        {
            ItemTransfers =
            [
                new ItemTransferChange
                {
                    ItemId = "item-001",
                    FromHolder = "char-b",
                    ToHolder = "char-c"
                }
            ]
        };
        var snapshot = new FactSnapshot
        {
            ItemStates =
            [
                new ItemStateSnapshot
                {
                    Id = "item-001",
                    CurrentHolder = "char-a"
                }
            ]
        };

        var issues = await layer.CheckAsync(changes, snapshot, LedgerRuleSet.CreateUniversalDefault());

        var issue = Assert.Single(issues);
        Assert.Equal("Entity", issue.Layer);
        Assert.Equal("ItemOwnershipMismatch", issue.IssueType);
    }
}
