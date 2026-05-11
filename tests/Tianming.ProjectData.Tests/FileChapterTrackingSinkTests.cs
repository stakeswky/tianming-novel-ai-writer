using System.Text.Json;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Guides;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class FileChapterTrackingSinkTests
{
    [Fact]
    public async Task DispatchAsync_writes_tracking_guides_to_json_files()
    {
        using var workspace = new TempDirectory();
        var sink = new FileChapterTrackingSink(workspace.Path);
        var dispatcher = new ChapterTrackingDispatcher(sink);

        await dispatcher.DispatchAsync("vol1_ch2", new ChapterChanges
        {
            CharacterStateChanges =
            [
                new CharacterStateChange
                {
                    CharacterId = "C7M3VT2K9P4NA",
                    NewLevel = "A",
                    NewAbilities = ["御风"],
                    RelationshipChanges =
                    {
                        ["C7M3VT2K9P4NB"] = new RelationshipChange { Relation = "盟友", TrustDelta = 12 }
                    },
                    NewMentalState = "镇定",
                    KeyEvent = "通过试炼",
                    Importance = "important"
                }
            ],
            ConflictProgress =
            [
                new ConflictProgressChange { ConflictId = "K7M3VT2K9P4NA", NewStatus = "active", Event = "冲突升级" }
            ],
            NewPlotPoints =
            [
                new PlotPointChange { Keywords = ["试炼"], Context = "林衡完成试炼", InvolvedCharacters = ["C7M3VT2K9P4NA"] }
            ],
            ForeshadowingActions =
            [
                new ForeshadowingAction { ForeshadowId = "F7M3VT2K9P4NA", Action = "setup" }
            ],
            LocationStateChanges =
            [
                new LocationStateChange { LocationId = "L7M3VT2K9P4NA", NewStatus = "开启", Event = "山门打开" }
            ],
            FactionStateChanges =
            [
                new FactionStateChange { FactionId = "G7M3VT2K9P4NA", NewStatus = "戒备", Event = "弟子入山" }
            ],
            TimeProgression = new TimeProgressionChange { TimePeriod = "第一日", ElapsedTime = "半日", KeyTimeEvent = "入山" },
            CharacterMovements =
            [
                new CharacterMovementChange { CharacterId = "C7M3VT2K9P4NA", FromLocation = "山脚", ToLocation = "山门" }
            ],
            ItemTransfers =
            [
                new ItemTransferChange { ItemId = "I7M3VT2K9P4NA", ItemName = "玉佩", FromHolder = "C7M3VT2K9P4NB", ToHolder = "C7M3VT2K9P4NA", Event = "交付" }
            ]
        });

        var characterGuide = await ReadJsonAsync<CharacterStateGuide>(workspace.Path, "character_state_guide_vol1.json");
        var conflictGuide = await ReadJsonAsync<ConflictProgressGuide>(workspace.Path, "conflict_progress_guide_vol1.json");
        var plotIndex = await ReadJsonAsync<PlotPointsIndex>(workspace.Path, "plot_points_vol1.json");
        var foreshadowGuide = await ReadJsonAsync<ForeshadowingStatusGuide>(workspace.Path, "foreshadowing_status_guide.json");
        var timelineGuide = await ReadJsonAsync<TimelineGuide>(workspace.Path, "timeline_guide_vol1.json");
        var itemGuide = await ReadJsonAsync<ItemStateGuide>(workspace.Path, "item_state_guide_vol1.json");

        Assert.Equal("A", characterGuide.Characters["C7M3VT2K9P4NA"].StateHistory.Single().Level);
        Assert.Equal(12, characterGuide.Characters["C7M3VT2K9P4NA"].StateHistory.Single().Relationships["C7M3VT2K9P4NB"].Trust);
        Assert.Equal("active", conflictGuide.Conflicts["K7M3VT2K9P4NA"].Status);
        Assert.Equal("林衡完成试炼", plotIndex.PlotPoints.Single().Context);
        Assert.True(foreshadowGuide.Foreshadowings["F7M3VT2K9P4NA"].IsSetup);
        Assert.Equal("山门", timelineGuide.CharacterLocations["C7M3VT2K9P4NA"].CurrentLocation);
        Assert.Equal("C7M3VT2K9P4NA", itemGuide.Items["I7M3VT2K9P4NA"].CurrentHolder);
    }

    [Fact]
    public async Task RemoveChapterDataAsync_removes_chapter_records_and_recalculates_current_state()
    {
        using var workspace = new TempDirectory();
        var sink = new FileChapterTrackingSink(workspace.Path);
        var dispatcher = new ChapterTrackingDispatcher(sink);

        await dispatcher.DispatchAsync("vol1_ch1", new ChapterChanges
        {
            CharacterStateChanges = [new CharacterStateChange { CharacterId = "C7M3VT2K9P4NA", NewLevel = "B" }],
            LocationStateChanges = [new LocationStateChange { LocationId = "L7M3VT2K9P4NA", NewStatus = "关闭" }]
        });
        await dispatcher.DispatchAsync("vol1_ch2", new ChapterChanges
        {
            CharacterStateChanges = [new CharacterStateChange { CharacterId = "C7M3VT2K9P4NA", NewLevel = "A" }],
            LocationStateChanges = [new LocationStateChange { LocationId = "L7M3VT2K9P4NA", NewStatus = "开启" }]
        });

        await dispatcher.RemoveChapterDataAsync("vol1_ch2");

        var characterGuide = await ReadJsonAsync<CharacterStateGuide>(workspace.Path, "character_state_guide_vol1.json");
        var locationGuide = await ReadJsonAsync<LocationStateGuide>(workspace.Path, "location_state_guide_vol1.json");

        Assert.Equal("B", characterGuide.Characters["C7M3VT2K9P4NA"].StateHistory.Single().Level);
        Assert.Equal("关闭", locationGuide.Locations["L7M3VT2K9P4NA"].CurrentStatus);
        Assert.DoesNotContain(locationGuide.Locations["L7M3VT2K9P4NA"].StateHistory, point => point.Chapter == "vol1_ch2");
    }

    private static async Task<T> ReadJsonAsync<T>(string root, string relativePath)
    {
        var json = await File.ReadAllTextAsync(System.IO.Path.Combine(root, relativePath));
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-tracking-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
