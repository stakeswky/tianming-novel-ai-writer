using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;
using TM.Services.Modules.ProjectData.Tracking.Layers;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking.Layers;

public class TimelineLayerTests
{
    [Fact]
    public async Task Flags_conflict_status_rollback()
    {
        var layer = new TimelineLayer();
        var changes = new ChapterChanges
        {
            ConflictProgress =
            [
                new ConflictProgressChange
                {
                    ConflictId = "conflict-001",
                    NewStatus = "推进中"
                }
            ]
        };
        var snapshot = new FactSnapshot
        {
            ConflictProgress =
            [
                new ConflictProgressSnapshot
                {
                    Id = "conflict-001",
                    Status = "已解决"
                }
            ]
        };

        var issues = await layer.CheckAsync(changes, snapshot, LedgerRuleSet.CreateUniversalDefault());

        var issue = Assert.Single(issues);
        Assert.Equal("Timeline", issue.Layer);
        Assert.Equal("ConflictStatusSkip", issue.IssueType);
    }

    [Fact]
    public async Task Flags_movement_start_location_mismatch()
    {
        var layer = new TimelineLayer();
        var changes = new ChapterChanges
        {
            CharacterMovements =
            [
                new CharacterMovementChange
                {
                    CharacterId = "char-001",
                    FromLocation = "loc-b",
                    ToLocation = "loc-c"
                }
            ]
        };
        var snapshot = new FactSnapshot
        {
            CharacterLocations =
            [
                new CharacterLocationSnapshot
                {
                    CharacterId = "char-001",
                    CurrentLocation = "loc-a"
                }
            ]
        };

        var issues = await layer.CheckAsync(changes, snapshot, LedgerRuleSet.CreateUniversalDefault());

        var issue = Assert.Single(issues);
        Assert.Equal("Timeline", issue.Layer);
        Assert.Equal("MovementStartLocationMismatch", issue.IssueType);
    }
}
