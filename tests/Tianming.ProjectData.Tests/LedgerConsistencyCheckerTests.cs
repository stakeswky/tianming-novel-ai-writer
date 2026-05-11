using System.Linq;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class LedgerConsistencyCheckerTests
{
    [Fact]
    public void Validate_reports_conflict_status_rollbacks()
    {
        var checker = new LedgerConsistencyChecker();
        var changes = new ChapterChanges
        {
            ConflictProgress =
            [
                new ConflictProgressChange
                {
                    ConflictId = "K7M3VT2K9P4NA",
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
                    Id = "K7M3VT2K9P4NA",
                    Status = "已解决"
                }
            ]
        };

        var result = checker.Validate(changes, snapshot, LedgerRuleSet.CreateUniversalDefault());

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.IssueType == "ConflictStatusSkip");
    }

    [Fact]
    public void Validate_reports_character_level_regression_without_loss_event()
    {
        var checker = new LedgerConsistencyChecker();
        var changes = new ChapterChanges
        {
            CharacterStateChanges =
            [
                new CharacterStateChange
                {
                    CharacterId = "C7M3VT2K9P4NA",
                    NewLevel = "B",
                    KeyEvent = ""
                }
            ]
        };
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

        var result = checker.Validate(changes, snapshot, LedgerRuleSet.CreateUniversalDefault());

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.IssueType == "LevelRegression");
    }

    [Fact]
    public void ValidateStructuralOnly_reports_movement_chain_break_and_relationship_contradiction()
    {
        var checker = new LedgerConsistencyChecker();
        var changes = new ChapterChanges
        {
            CharacterMovements =
            [
                new CharacterMovementChange
                {
                    CharacterId = "C7M3VT2K9P4NA",
                    FromLocation = "L7M3VT2K9P4NA",
                    ToLocation = "L7M3VT2K9P4NB"
                },
                new CharacterMovementChange
                {
                    CharacterId = "C7M3VT2K9P4NA",
                    FromLocation = "L7M3VT2K9P4NC",
                    ToLocation = "L7M3VT2K9P4ND"
                }
            ],
            CharacterStateChanges =
            [
                new CharacterStateChange
                {
                    CharacterId = "C7M3VT2K9P4NA",
                    RelationshipChanges =
                    {
                        ["C7M3VT2K9P4NB"] = new RelationshipChange { Relation = "盟友" }
                    }
                },
                new CharacterStateChange
                {
                    CharacterId = "C7M3VT2K9P4NB",
                    RelationshipChanges =
                    {
                        ["C7M3VT2K9P4NA"] = new RelationshipChange { Relation = "enemy" }
                    }
                }
            ]
        };

        var result = checker.ValidateStructuralOnly(changes);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.IssueType == "MovementChainBreak");
        Assert.Contains(result.Issues, issue => issue.IssueType == "RelationshipContradiction");
    }

    [Fact]
    public void Validate_reports_item_holder_mismatch_against_snapshot()
    {
        var checker = new LedgerConsistencyChecker();
        var changes = new ChapterChanges
        {
            ItemTransfers =
            [
                new ItemTransferChange
                {
                    ItemId = "I7M3VT2K9P4NA",
                    FromHolder = "C7M3VT2K9P4NB",
                    ToHolder = "C7M3VT2K9P4NC"
                }
            ]
        };
        var snapshot = new FactSnapshot
        {
            ItemStates =
            [
                new ItemStateSnapshot
                {
                    Id = "I7M3VT2K9P4NA",
                    CurrentHolder = "C7M3VT2K9P4NA"
                }
            ]
        };

        var result = checker.Validate(changes, snapshot, LedgerRuleSet.CreateUniversalDefault());

        Assert.False(result.Success);
        Assert.Contains(result.Issues, issue => issue.IssueType == "ItemOwnershipMismatch");
    }
}
