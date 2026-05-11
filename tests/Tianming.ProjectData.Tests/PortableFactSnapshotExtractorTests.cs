using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Models.Design.Location;
using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Models.Guides;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class PortableFactSnapshotExtractorTests
{
    [Fact]
    public async Task ExtractSnapshotAsync_uses_previous_chapter_state_and_selected_context_without_future_leakage()
    {
        var source = new InMemoryFactSnapshotGuideSource
        {
            CharacterStateGuide = new CharacterStateGuide
            {
                Characters =
                {
                    ["char-a"] = new CharacterStateEntry
                    {
                        Name = "林衡",
                        StateHistory =
                        [
                            new CharacterState { Chapter = "vol1_ch1", Level = "炼气", Abilities = ["御风"] },
                            new CharacterState { Chapter = "vol1_ch3", Level = "筑基", Abilities = ["御风", "剑意"] },
                            new CharacterState { Chapter = "vol1_ch5", Level = "金丹", Abilities = ["未来术"] }
                        ]
                    },
                    ["char-b"] = new CharacterStateEntry
                    {
                        Name = "路人",
                        StateHistory =
                        [
                            new CharacterState { Chapter = "vol1_ch2", Level = "凡人" }
                        ]
                    }
                }
            },
            ConflictProgressGuide = new ConflictProgressGuide
            {
                Conflicts =
                {
                    ["conflict-a"] = new ConflictProgressEntry
                    {
                        Name = "山门之争",
                        Status = "future",
                        ProgressPoints =
                        [
                            new ConflictProgressPoint { Chapter = "vol1_ch2", Event = "冲突显现", Status = "active" },
                            new ConflictProgressPoint { Chapter = "vol1_ch5", Event = "未来解决", Status = "resolved" }
                        ]
                    }
                }
            },
            ForeshadowingStatusGuide = new ForeshadowingStatusGuide
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
            },
            LocationStateGuide = new LocationStateGuide
            {
                Locations =
                {
                    ["loc-a"] = new LocationStateEntry
                    {
                        Name = "青岚山门",
                        CurrentStatus = "future",
                        StateHistory =
                        [
                            new LocationStatePoint { Chapter = "vol1_ch2", Status = "开启" },
                            new LocationStatePoint { Chapter = "vol1_ch5", Status = "崩塌" }
                        ]
                    }
                }
            },
            FactionStateGuide = new FactionStateGuide
            {
                Factions =
                {
                    ["fac-a"] = new FactionStateEntry
                    {
                        Name = "青岚宗",
                        CurrentStatus = "future",
                        StateHistory =
                        [
                            new FactionStatePoint { Chapter = "vol1_ch1", Status = "戒备" },
                            new FactionStatePoint { Chapter = "vol1_ch5", Status = "覆灭" }
                        ]
                    }
                }
            },
            TimelineGuide = new TimelineGuide
            {
                ChapterTimeline =
                [
                    new ChapterTimeEntry { ChapterId = "vol1_ch1", TimePeriod = "第一日", ElapsedTime = "一刻", KeyTimeEvent = "入山" },
                    new ChapterTimeEntry { ChapterId = "vol1_ch5", TimePeriod = "第五日", ElapsedTime = "五日", KeyTimeEvent = "未来" }
                ],
                CharacterLocations =
                {
                    ["char-a"] = new CharacterLocationEntry
                    {
                        CharacterName = "林衡",
                        CurrentLocation = "青岚山门",
                        LastUpdatedChapter = "vol1_ch3"
                    },
                    ["char-future"] = new CharacterLocationEntry
                    {
                        CharacterName = "未来人",
                        CurrentLocation = "未来",
                        LastUpdatedChapter = "vol1_ch5"
                    }
                }
            },
            ItemStateGuide = new ItemStateGuide
            {
                Items =
                {
                    ["item-a"] = new ItemStateEntry
                    {
                        Name = "玉佩",
                        CurrentHolder = "char-a",
                        CurrentStatus = "future",
                        StateHistory =
                        [
                            new ItemStatePoint { Chapter = "vol1_ch2", Holder = "char-a", Status = "active" },
                            new ItemStatePoint { Chapter = "vol1_ch5", Holder = "char-b", Status = "lost" }
                        ]
                    }
                }
            },
            Characters =
            {
                ["char-a"] = new CharacterRulesData
                {
                    Id = "char-a",
                    Name = "林衡",
                    Appearance = "黑发少年，眉心有青色印记",
                    Identity = "青岚宗外门弟子",
                    Want = "通过试炼",
                    FlawBelief = "不敢相信同伴"
                }
            },
            Locations =
            {
                ["loc-a"] = new LocationRulesData
                {
                    Id = "loc-a",
                    Name = "青岚山门",
                    Description = "云雾缭绕的宗门入口",
                    Terrain = "山门石阶",
                    Landmarks = ["试炼钟"]
                }
            },
            WorldRules =
            {
                ["world-a"] = new WorldRulesData
                {
                    Id = "world-a",
                    Name = "灵脉规则",
                    HardRules = "凡人不可直接吸收灵脉"
                }
            },
            PlotPoints =
            [
                new PlotPointEntry
                {
                    Id = "plot-important",
                    Chapter = "vol1_ch2",
                    Context = "林衡第一次敲响试炼钟",
                    InvolvedCharacters = ["char-a"],
                    Keywords = ["foreshadow-a"],
                    Importance = "important",
                    Storyline = "main"
                },
                new PlotPointEntry
                {
                    Id = "plot-critical",
                    Chapter = "vol1_ch1",
                    Context = "山门之争埋下主线冲突",
                    InvolvedCharacters = ["char-a"],
                    Keywords = ["conflict-a"],
                    Importance = "critical",
                    Storyline = "main"
                },
                new PlotPointEntry
                {
                    Id = "plot-future",
                    Chapter = "vol1_ch5",
                    Context = "未来剧情不得泄漏",
                    InvolvedCharacters = ["char-a"],
                    Importance = "critical",
                    Storyline = "main"
                }
            ]
        };
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

        var character = Assert.Single(snapshot.CharacterStates, state => state.Id == "char-a");
        Assert.Equal("筑基", character.Stage);
        Assert.Equal("御风、剑意", character.Abilities);
        Assert.Equal("vol1_ch3", character.ChapterId);
        Assert.Contains(snapshot.CharacterStates, state => state.Id == "char-b" && state.Stage == "凡人");
        var conflict = Assert.Single(snapshot.ConflictProgress);
        Assert.Equal("active", conflict.Status);
        Assert.Equal(["vol1_ch2: 冲突显现"], conflict.RecentProgress);
        Assert.Equal("开启", Assert.Single(snapshot.LocationStates).Status);
        Assert.Equal("戒备", Assert.Single(snapshot.FactionStates).Status);
        Assert.Equal("vol1_ch1", Assert.Single(snapshot.Timeline).ChapterId);
        Assert.Equal("青岚山门", Assert.Single(snapshot.CharacterLocations).CurrentLocation);
        var item = Assert.Single(snapshot.ItemStates);
        Assert.Equal("char-a", item.CurrentHolder);
        Assert.Equal("active", item.Status);
        var characterDescription = snapshot.CharacterDescriptions["char-a"];
        Assert.Equal("林衡", characterDescription.Name);
        Assert.Equal("黑发", characterDescription.HairColor);
        Assert.Contains("青岚宗外门弟子", characterDescription.PersonalityTags);
        Assert.Equal("云雾缭绕的宗门入口", snapshot.LocationDescriptions["loc-a"].Description);
        Assert.Contains("试炼钟", snapshot.LocationDescriptions["loc-a"].Features);
        var rule = Assert.Single(snapshot.WorldRuleConstraints);
        Assert.Equal("world-a", rule.RuleId);
        Assert.Equal("凡人不可直接吸收灵脉", rule.Constraint);
        Assert.Equal(["plot-critical", "plot-important"], snapshot.PlotPoints.Select(point => point.Id).ToList());
        Assert.Equal("山门之争埋下主线冲突", snapshot.PlotPoints[0].Summary);
        Assert.DoesNotContain(snapshot.PlotPoints, point => point.Id == "plot-future");
        Assert.DoesNotContain(snapshot.CharacterStates, state => state.Stage == "金丹");
    }

    [Fact]
    public async Task ExtractVolumeEndSnapshotAsync_includes_latest_values_from_all_guides()
    {
        var source = new InMemoryFactSnapshotGuideSource
        {
            CharacterStateGuide = new CharacterStateGuide
            {
                Characters =
                {
                    ["char-a"] = new CharacterStateEntry
                    {
                        Name = "林衡",
                        StateHistory =
                        [
                            new CharacterState { Chapter = "vol1_ch1", Level = "炼气" },
                            new CharacterState { Chapter = "vol1_ch3", Level = "筑基" }
                        ]
                    }
                }
            },
            ConflictProgressGuide = new ConflictProgressGuide
            {
                Conflicts =
                {
                    ["conflict-a"] = new ConflictProgressEntry
                    {
                        Name = "山门之争",
                        Status = "resolved",
                        ProgressPoints =
                        [
                            new ConflictProgressPoint { Chapter = "vol1_ch1", Event = "爆发", Status = "active" },
                            new ConflictProgressPoint { Chapter = "vol1_ch3", Event = "平息", Status = "resolved" }
                        ]
                    }
                }
            },
            ForeshadowingStatusGuide = new ForeshadowingStatusGuide
            {
                Foreshadowings =
                {
                    ["foreshadow-a"] = new ForeshadowingStatusEntry
                    {
                        Name = "玉佩伏笔",
                        IsSetup = true,
                        IsResolved = true,
                        ActualSetupChapter = "vol1_ch1",
                        ActualPayoffChapter = "vol1_ch3"
                    }
                }
            }
        };
        var extractor = new PortableFactSnapshotExtractor(source);

        var snapshot = await extractor.ExtractVolumeEndSnapshotAsync("vol1_ch3");

        Assert.Equal("筑基", Assert.Single(snapshot.CharacterStates).Stage);
        Assert.Equal("resolved", Assert.Single(snapshot.ConflictProgress).Status);
        Assert.True(Assert.Single(snapshot.ForeshadowingStatus).IsResolved);
    }

    [Fact]
    public async Task ExtractSnapshotAsync_respects_original_context_window_and_list_caps()
    {
        var source = new InMemoryFactSnapshotGuideSource
        {
            CharacterStateGuide = new CharacterStateGuide
            {
                Characters =
                {
                    ["char-a"] = new CharacterStateEntry
                    {
                        Name = "林衡",
                        StateHistory = [new CharacterState { Chapter = "vol1_ch2", Level = "炼气" }]
                    },
                    ["char-b"] = new CharacterStateEntry
                    {
                        Name = "沈晚",
                        StateHistory = [new CharacterState { Chapter = "vol1_ch5", Level = "筑基" }]
                    },
                    ["char-c"] = new CharacterStateEntry
                    {
                        Name = "远处旧友",
                        StateHistory = [new CharacterState { Chapter = "vol1_ch2", Level = "炼气" }]
                    }
                }
            },
            LocationStateGuide = new LocationStateGuide
            {
                Locations =
                {
                    ["loc-a"] = new LocationStateEntry
                    {
                        Name = "青岚山门",
                        StateHistory = [new LocationStatePoint { Chapter = "vol1_ch1", Status = "平静" }]
                    },
                    ["loc-b"] = new LocationStateEntry
                    {
                        Name = "戒律堂",
                        StateHistory = [new LocationStatePoint { Chapter = "vol1_ch5", Status = "戒严" }]
                    },
                    ["loc-c"] = new LocationStateEntry
                    {
                        Name = "旧矿洞",
                        StateHistory = [new LocationStatePoint { Chapter = "vol1_ch3", Status = "关闭" }]
                    }
                }
            },
            FactionStateGuide = new FactionStateGuide
            {
                Factions =
                {
                    ["fac-old"] = new FactionStateEntry
                    {
                        Name = "青岚宗",
                        StateHistory = [new FactionStatePoint { Chapter = "vol1_ch1", Status = "守势" }]
                    },
                    ["fac-active"] = new FactionStateEntry
                    {
                        Name = "戒律堂",
                        StateHistory = [new FactionStatePoint { Chapter = "vol1_ch5", Status = "强势" }]
                    },
                    ["fac-other"] = new FactionStateEntry
                    {
                        Name = "旧盟",
                        StateHistory = [new FactionStatePoint { Chapter = "vol1_ch2", Status = "沉寂" }]
                    }
                }
            },
            TimelineGuide = new TimelineGuide
            {
                ChapterTimeline =
                [
                    new ChapterTimeEntry { ChapterId = "vol1_ch1", KeyTimeEvent = "入门" },
                    new ChapterTimeEntry { ChapterId = "vol1_ch2", KeyTimeEvent = "试炼" },
                    new ChapterTimeEntry { ChapterId = "vol1_ch4", KeyTimeEvent = "夜谈" },
                    new ChapterTimeEntry { ChapterId = "vol1_ch5", KeyTimeEvent = "戒严" },
                    new ChapterTimeEntry { ChapterId = "vol1_ch7", KeyTimeEvent = "未来" }
                ],
                CharacterLocations =
                {
                    ["char-a"] = new CharacterLocationEntry
                    {
                        CharacterName = "林衡",
                        CurrentLocation = "青岚山门",
                        LastUpdatedChapter = "vol1_ch1"
                    },
                    ["char-b"] = new CharacterLocationEntry
                    {
                        CharacterName = "沈晚",
                        CurrentLocation = "戒律堂",
                        LastUpdatedChapter = "vol1_ch5"
                    },
                    ["char-c"] = new CharacterLocationEntry
                    {
                        CharacterName = "远处旧友",
                        CurrentLocation = "旧矿洞",
                        LastUpdatedChapter = "vol1_ch2"
                    }
                }
            },
            ItemStateGuide = new ItemStateGuide
            {
                Items =
                {
                    ["item-a"] = new ItemStateEntry
                    {
                        Name = "玉佩",
                        CurrentHolder = "char-a",
                        StateHistory = [new ItemStatePoint { Chapter = "vol1_ch1", Holder = "char-a", Status = "active" }]
                    },
                    ["item-b"] = new ItemStateEntry
                    {
                        Name = "戒尺",
                        CurrentHolder = "char-b",
                        StateHistory = [new ItemStatePoint { Chapter = "vol1_ch5", Holder = "char-b", Status = "active" }]
                    },
                    ["item-c"] = new ItemStateEntry
                    {
                        Name = "旧钥匙",
                        CurrentHolder = "char-c",
                        StateHistory = [new ItemStatePoint { Chapter = "vol1_ch4", Holder = "char-c", Status = "sealed" }]
                    }
                }
            },
            PlotPoints =
            [
                new PlotPointEntry { Id = "plot-1", Chapter = "vol1_ch4", Context = "林衡发现戒律堂异常", InvolvedCharacters = ["char-a"], Importance = "critical", Storyline = "main" },
                new PlotPointEntry { Id = "plot-2", Chapter = "vol1_ch5", Context = "沈晚封锁山门", InvolvedCharacters = ["char-a"], Importance = "important", Storyline = "main" },
                new PlotPointEntry { Id = "plot-future", Chapter = "vol1_ch7", Context = "未来剧情", InvolvedCharacters = ["char-a"], Importance = "critical", Storyline = "main" }
            ]
        };
        var extractor = new PortableFactSnapshotExtractor(
            source,
            new PortableFactSnapshotExtractorOptions
            {
                ActiveEntityWindowChapters = 2,
                ActiveEntityWindowMaxCount = 1,
                ChaptersPerVolume = 20,
                MaxFactionStates = 2,
                MaxItemStates = 2,
                MaxTimelineEntries = 2,
                MaxPlotPoints = 1
            });

        var snapshot = await extractor.ExtractSnapshotAsync(
            chapterId: "vol1_ch6",
            characterIds: ["char-a"],
            locationIds: ["loc-a"],
            conflictIds: [],
            foreshadowingSetupIds: [],
            foreshadowingPayoffIds: [],
            worldRuleIds: [],
            factionIds: ["fac-old"]);

        Assert.Equal(["char-a", "char-b"], snapshot.CharacterStates.Select(state => state.Id).ToList());
        Assert.Equal(["loc-a", "loc-b"], snapshot.LocationStates.Select(state => state.Id).ToList());
        Assert.Equal(["fac-old", "fac-active"], snapshot.FactionStates.Select(state => state.Id).ToList());
        Assert.Equal(["vol1_ch4", "vol1_ch5"], snapshot.Timeline.Select(entry => entry.ChapterId).ToList());
        Assert.Equal(["char-a", "char-b"], snapshot.CharacterLocations.Select(entry => entry.CharacterId).ToList());
        Assert.Equal(["item-a", "item-b"], snapshot.ItemStates.Select(item => item.Id).ToList());
        Assert.Equal(["plot-1"], snapshot.PlotPoints.Select(point => point.Id).ToList());
    }

    private sealed class InMemoryFactSnapshotGuideSource : IFactSnapshotGuideSource
    {
        public CharacterStateGuide CharacterStateGuide { get; init; } = new();
        public ConflictProgressGuide ConflictProgressGuide { get; init; } = new();
        public ForeshadowingStatusGuide ForeshadowingStatusGuide { get; init; } = new();
        public LocationStateGuide LocationStateGuide { get; init; } = new();
        public FactionStateGuide FactionStateGuide { get; init; } = new();
        public TimelineGuide TimelineGuide { get; init; } = new();
        public ItemStateGuide ItemStateGuide { get; init; } = new();
        public Dictionary<string, CharacterRulesData> Characters { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, LocationRulesData> Locations { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, WorldRulesData> WorldRules { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<PlotPointEntry> PlotPoints { get; init; } = [];

        public Task<CharacterStateGuide> GetCharacterStateGuideAsync(bool allVolumes, CancellationToken cancellationToken = default)
            => Task.FromResult(CharacterStateGuide);

        public Task<ConflictProgressGuide> GetConflictProgressGuideAsync(bool allVolumes, CancellationToken cancellationToken = default)
            => Task.FromResult(ConflictProgressGuide);

        public Task<ForeshadowingStatusGuide> GetForeshadowingStatusGuideAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(ForeshadowingStatusGuide);

        public Task<LocationStateGuide> GetLocationStateGuideAsync(bool allVolumes, CancellationToken cancellationToken = default)
            => Task.FromResult(LocationStateGuide);

        public Task<FactionStateGuide> GetFactionStateGuideAsync(bool allVolumes, CancellationToken cancellationToken = default)
            => Task.FromResult(FactionStateGuide);

        public Task<TimelineGuide> GetTimelineGuideAsync(bool allVolumes, CancellationToken cancellationToken = default)
            => Task.FromResult(TimelineGuide);

        public Task<ItemStateGuide> GetItemStateGuideAsync(bool allVolumes, CancellationToken cancellationToken = default)
            => Task.FromResult(ItemStateGuide);

        public Task<IReadOnlyList<PlotPointEntry>> GetPlotPointsAsync(
            string currentChapterId,
            IReadOnlyCollection<string> characterIds,
            IReadOnlyCollection<string> otherEntityIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PlotPointEntry>>(PlotPoints);
        }

        public Task<IReadOnlyList<CharacterRulesData>> GetCharactersAsync(
            IReadOnlyCollection<string> characterIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<CharacterRulesData>>(characterIds
                .Where(Characters.ContainsKey)
                .Select(id => Characters[id])
                .ToList());
        }

        public Task<IReadOnlyList<LocationRulesData>> GetLocationsAsync(
            IReadOnlyCollection<string> locationIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<LocationRulesData>>(locationIds
                .Where(Locations.ContainsKey)
                .Select(id => Locations[id])
                .ToList());
        }

        public Task<IReadOnlyList<WorldRulesData>> GetWorldRulesAsync(
            IReadOnlyCollection<string> worldRuleIds,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WorldRulesData>>(worldRuleIds
                .Where(WorldRules.ContainsKey)
                .Select(id => WorldRules[id])
                .ToList());
        }
    }
}
