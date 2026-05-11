using System.Text.Json;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Models.Design.Location;
using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Models.Guides;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class FileFactSnapshotGuideSourceTests
{
    [Fact]
    public async Task ExtractSnapshotAsync_loads_guides_and_design_rules_from_original_file_layout()
    {
        using var workspace = new TempDirectory();
        var trackingDir = Path.Combine(workspace.Path, "Tracking");
        Directory.CreateDirectory(trackingDir);
        WriteJson(Path.Combine(trackingDir, "character_state_guide_vol1.json"), new CharacterStateGuide
        {
            Characters =
            {
                ["char-a"] = new CharacterStateEntry
                {
                    Name = "林衡",
                    StateHistory =
                    [
                        new CharacterState { Chapter = "vol1_ch1", Level = "炼气" },
                        new CharacterState { Chapter = "vol1_ch3", Level = "筑基", Abilities = ["御风"] }
                    ]
                }
            }
        });
        WriteJson(Path.Combine(trackingDir, "conflict_progress_guide_vol1.json"), new ConflictProgressGuide
        {
            Conflicts =
            {
                ["conflict-a"] = new ConflictProgressEntry
                {
                    Name = "山门之争",
                    Status = "active",
                    ProgressPoints =
                    [
                        new ConflictProgressPoint { Chapter = "vol1_ch2", Event = "冲突显现", Status = "active" }
                    ]
                }
            }
        });
        WriteJson(Path.Combine(trackingDir, "foreshadowing_status_guide.json"), new ForeshadowingStatusGuide
        {
            Foreshadowings =
            {
                ["foreshadow-a"] = new ForeshadowingStatusEntry
                {
                    Name = "玉佩伏笔",
                    IsSetup = true,
                    ActualSetupChapter = "vol1_ch2"
                }
            }
        });
        WriteJson(Path.Combine(trackingDir, "location_state_guide_vol1.json"), new LocationStateGuide
        {
            Locations =
            {
                ["loc-a"] = new LocationStateEntry
                {
                    Name = "青岚山门",
                    StateHistory = [new LocationStatePoint { Chapter = "vol1_ch2", Status = "开启" }]
                }
            }
        });
        WriteJson(Path.Combine(trackingDir, "faction_state_guide_vol1.json"), new FactionStateGuide
        {
            Factions =
            {
                ["fac-a"] = new FactionStateEntry
                {
                    Name = "青岚宗",
                    StateHistory = [new FactionStatePoint { Chapter = "vol1_ch2", Status = "戒备" }]
                }
            }
        });
        WriteJson(Path.Combine(trackingDir, "timeline_guide_vol1.json"), new TimelineGuide
        {
            ChapterTimeline =
            [
                new ChapterTimeEntry { ChapterId = "vol1_ch2", TimePeriod = "第二日", KeyTimeEvent = "试炼开启" }
            ],
            CharacterLocations =
            {
                ["char-a"] = new CharacterLocationEntry
                {
                    CharacterName = "林衡",
                    CurrentLocation = "青岚山门",
                    LastUpdatedChapter = "vol1_ch2"
                }
            }
        });
        WriteJson(Path.Combine(trackingDir, "item_state_guide_vol1.json"), new ItemStateGuide
        {
            Items =
            {
                ["item-a"] = new ItemStateEntry
                {
                    Name = "玉佩",
                    CurrentHolder = "char-a",
                    StateHistory = [new ItemStatePoint { Chapter = "vol1_ch2", Holder = "char-a", Status = "active" }]
                }
            }
        });
        WriteJson(Path.Combine(trackingDir, "plot_points_vol1.json"), new PlotPointsIndex
        {
            PlotPoints =
            [
                new PlotPointEntry
                {
                    Id = "plot-a",
                    Chapter = "vol1_ch2",
                    Context = "林衡敲响试炼钟",
                    InvolvedCharacters = ["char-a"],
                    Keywords = ["conflict-a"],
                    Importance = "critical",
                    Storyline = "main"
                }
            ]
        });

        WriteJson(Path.Combine(workspace.Path, "Modules", "Design", "Elements", "CharacterRules", "character_rules.json"),
        new List<CharacterRulesData>
        {
            new CharacterRulesData
            {
                Id = "char-a",
                Name = "林衡",
                Appearance = "黑发少年",
                Identity = "青岚宗外门弟子",
                Want = "通过试炼"
            }
        });
        WriteJson(Path.Combine(workspace.Path, "Modules", "Design", "Elements", "LocationRules", "location_rules.json"),
        new List<LocationRulesData>
        {
            new LocationRulesData
            {
                Id = "loc-a",
                Name = "青岚山门",
                Description = "云雾缭绕的宗门入口",
                Terrain = "山门石阶",
                Landmarks = ["试炼钟"]
            }
        });
        WriteJson(Path.Combine(workspace.Path, "Modules", "Design", "GlobalSettings", "WorldRules", "world_rules.json"),
        new List<WorldRulesData>
        {
            new WorldRulesData
            {
                Id = "world-a",
                Name = "灵脉规则",
                HardRules = "凡人不可直接吸收灵脉"
            }
        });
        var source = new FileFactSnapshotGuideSource(trackingDir, workspace.Path);
        var extractor = new PortableFactSnapshotExtractor(source);

        var snapshot = await extractor.ExtractSnapshotAsync(
            chapterId: "vol1_ch4",
            characterIds: ["char-a"],
            locationIds: ["loc-a"],
            conflictIds: ["conflict-a"],
            foreshadowingSetupIds: ["foreshadow-a"],
            foreshadowingPayoffIds: [],
            worldRuleIds: ["world-a"],
            factionIds: ["fac-a"]);

        Assert.Equal("筑基", Assert.Single(snapshot.CharacterStates).Stage);
        Assert.Equal("active", Assert.Single(snapshot.ConflictProgress).Status);
        Assert.True(Assert.Single(snapshot.ForeshadowingStatus).IsSetup);
        Assert.Equal("开启", Assert.Single(snapshot.LocationStates).Status);
        Assert.Equal("戒备", Assert.Single(snapshot.FactionStates).Status);
        Assert.Equal("第二日", Assert.Single(snapshot.Timeline).TimePeriod);
        Assert.Equal("青岚山门", Assert.Single(snapshot.CharacterLocations).CurrentLocation);
        Assert.Equal("玉佩", Assert.Single(snapshot.ItemStates).Name);
        Assert.Equal("plot-a", Assert.Single(snapshot.PlotPoints).Id);
        Assert.Equal("黑发", snapshot.CharacterDescriptions["char-a"].HairColor);
        Assert.Contains("试炼钟", snapshot.LocationDescriptions["loc-a"].Features);
        Assert.Equal("凡人不可直接吸收灵脉", Assert.Single(snapshot.WorldRuleConstraints).Constraint);
    }

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"tianming-file-fact-source-{Guid.NewGuid():N}");

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
