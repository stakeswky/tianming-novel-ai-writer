using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ChapterTrackingDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_forwards_each_change_group_to_tracking_sink()
    {
        var sink = new RecordingTrackingSink();
        var dispatcher = new ChapterTrackingDispatcher(sink);

        await dispatcher.DispatchAsync("vol1_ch5", new ChapterChanges
        {
            CharacterStateChanges =
            [
                new CharacterStateChange { CharacterId = "C7M3VT2K9P4NA" }
            ],
            ConflictProgress =
            [
                new ConflictProgressChange { ConflictId = "K7M3VT2K9P4NA", NewStatus = "active" }
            ],
            NewPlotPoints =
            [
                new PlotPointChange { Context = "林衡入山" }
            ],
            ForeshadowingActions =
            [
                new ForeshadowingAction { ForeshadowId = "F7M3VT2K9P4NA", Action = "setup" },
                new ForeshadowingAction { ForeshadowId = "F7M3VT2K9P4NB", Action = "payoff" }
            ],
            LocationStateChanges =
            [
                new LocationStateChange { LocationId = "L7M3VT2K9P4NA", NewStatus = "opened" }
            ],
            FactionStateChanges =
            [
                new FactionStateChange { FactionId = "G7M3VT2K9P4NA", NewStatus = "alert" }
            ],
            TimeProgression = new TimeProgressionChange { TimePeriod = "第一日" },
            CharacterMovements =
            [
                new CharacterMovementChange { CharacterId = "C7M3VT2K9P4NA", ToLocation = "L7M3VT2K9P4NA" }
            ],
            ItemTransfers =
            [
                new ItemTransferChange { ItemId = "I7M3VT2K9P4NA", ToHolder = "C7M3VT2K9P4NA" }
            ]
        });

        Assert.Equal(
            [
                "character:vol1_ch5:C7M3VT2K9P4NA",
                "conflict:vol1_ch5:K7M3VT2K9P4NA",
                "plot:vol1_ch5:林衡入山",
                "foreshadow-setup:vol1_ch5:F7M3VT2K9P4NA",
                "foreshadow-payoff:vol1_ch5:F7M3VT2K9P4NB",
                "location:vol1_ch5:L7M3VT2K9P4NA",
                "faction:vol1_ch5:G7M3VT2K9P4NA",
                "time:vol1_ch5:第一日",
                "movement:vol1_ch5:1",
                "item:vol1_ch5:I7M3VT2K9P4NA",
                "foreshadow-refresh:vol1_ch5"
            ],
            sink.Events);
    }

    [Fact]
    public async Task RemoveChapterDataAsync_forwards_cleanup_to_all_tracking_categories()
    {
        var sink = new RecordingTrackingSink();
        var dispatcher = new ChapterTrackingDispatcher(sink);

        await dispatcher.RemoveChapterDataAsync("vol1_ch5");

        Assert.Equal(
            [
                "remove-character:vol1_ch5",
                "remove-conflict:vol1_ch5",
                "remove-plot:vol1_ch5",
                "remove-foreshadow:vol1_ch5",
                "remove-location:vol1_ch5",
                "remove-faction:vol1_ch5",
                "remove-timeline:vol1_ch5",
                "remove-item:vol1_ch5"
            ],
            sink.Events);
    }

    private sealed class RecordingTrackingSink : IChapterTrackingSink
    {
        public List<string> Events { get; } = new();

        public Task UpdateCharacterStateAsync(string chapterId, CharacterStateChange change)
        {
            Events.Add($"character:{chapterId}:{change.CharacterId}");
            return Task.CompletedTask;
        }

        public Task UpdateConflictProgressAsync(string chapterId, ConflictProgressChange change)
        {
            Events.Add($"conflict:{chapterId}:{change.ConflictId}");
            return Task.CompletedTask;
        }

        public Task AddPlotPointAsync(string chapterId, PlotPointChange change)
        {
            Events.Add($"plot:{chapterId}:{change.Context}");
            return Task.CompletedTask;
        }

        public Task MarkForeshadowingAsSetupAsync(string foreshadowId, string chapterId)
        {
            Events.Add($"foreshadow-setup:{chapterId}:{foreshadowId}");
            return Task.CompletedTask;
        }

        public Task MarkForeshadowingAsResolvedAsync(string foreshadowId, string chapterId)
        {
            Events.Add($"foreshadow-payoff:{chapterId}:{foreshadowId}");
            return Task.CompletedTask;
        }

        public Task RefreshForeshadowingOverdueStatusAsync(string chapterId)
        {
            Events.Add($"foreshadow-refresh:{chapterId}");
            return Task.CompletedTask;
        }

        public Task UpdateLocationStateAsync(string chapterId, LocationStateChange change)
        {
            Events.Add($"location:{chapterId}:{change.LocationId}");
            return Task.CompletedTask;
        }

        public Task UpdateFactionStateAsync(string chapterId, FactionStateChange change)
        {
            Events.Add($"faction:{chapterId}:{change.FactionId}");
            return Task.CompletedTask;
        }

        public Task UpdateTimeProgressionAsync(string chapterId, TimeProgressionChange change)
        {
            Events.Add($"time:{chapterId}:{change.TimePeriod}");
            return Task.CompletedTask;
        }

        public Task UpdateCharacterMovementsAsync(string chapterId, List<CharacterMovementChange> movements)
        {
            Events.Add($"movement:{chapterId}:{movements.Count}");
            return Task.CompletedTask;
        }

        public Task UpdateItemStateAsync(string chapterId, ItemTransferChange change)
        {
            Events.Add($"item:{chapterId}:{change.ItemId}");
            return Task.CompletedTask;
        }

        public Task RemoveCharacterStateAsync(string chapterId)
        {
            Events.Add($"remove-character:{chapterId}");
            return Task.CompletedTask;
        }

        public Task RemoveConflictProgressAsync(string chapterId)
        {
            Events.Add($"remove-conflict:{chapterId}");
            return Task.CompletedTask;
        }

        public Task RemovePlotPointsAsync(string chapterId)
        {
            Events.Add($"remove-plot:{chapterId}");
            return Task.CompletedTask;
        }

        public Task RemoveForeshadowingStatusAsync(string chapterId)
        {
            Events.Add($"remove-foreshadow:{chapterId}");
            return Task.CompletedTask;
        }

        public Task RemoveLocationStateAsync(string chapterId)
        {
            Events.Add($"remove-location:{chapterId}");
            return Task.CompletedTask;
        }

        public Task RemoveFactionStateAsync(string chapterId)
        {
            Events.Add($"remove-faction:{chapterId}");
            return Task.CompletedTask;
        }

        public Task RemoveTimelineAsync(string chapterId)
        {
            Events.Add($"remove-timeline:{chapterId}");
            return Task.CompletedTask;
        }

        public Task RemoveItemStateAsync(string chapterId)
        {
            Events.Add($"remove-item:{chapterId}");
            return Task.CompletedTask;
        }
    }
}
