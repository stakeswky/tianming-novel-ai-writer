using System.Text.Json;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Guides;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ChapterGenerationPipelineTests
{
    [Fact]
    public async Task SaveGeneratedChapterStrictAsync_validates_prepares_saves_and_records_success()
    {
        using var workspace = new TempDirectory();
        var recorder = new GenerationStatisticsRecorder();
        var pipeline = CreatePipeline(workspace.Path, recorder);

        var result = await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch3",
            "林衡束起黑发。\n---CHANGES---\n" + EmptyChangesJson(),
            new FactSnapshot
            {
                CharacterDescriptions =
                {
                    ["C7M3VT2K9P4NA"] = new CharacterCoreDescription { Name = "林衡", HairColor = "黑发" }
                }
            },
            packagedTitle: "黑发少年");

        var saved = await new ChapterContentStore(workspace.Path).GetChapterAsync("vol1_ch3");
        var stats = recorder.GetStatistics();

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("# 第3章 黑发少年\n\n林衡束起黑发。", saved);
        Assert.Equal(1, stats.TotalGenerations);
        Assert.Equal(1, stats.FirstPassCount);
    }

    [Fact]
    public async Task SaveGeneratedChapterStrictAsync_records_failure_without_saving_invalid_content()
    {
        using var workspace = new TempDirectory();
        var recorder = new GenerationStatisticsRecorder();
        var pipeline = CreatePipeline(workspace.Path, recorder);

        var result = await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch4",
            "林衡束起金发。\n---CHANGES---\n" + EmptyChangesJson(),
            new FactSnapshot
            {
                CharacterDescriptions =
                {
                    ["C7M3VT2K9P4NA"] = new CharacterCoreDescription { Name = "林衡", HairColor = "黑发" }
                }
            },
            packagedTitle: "失败章节");

        var saved = await new ChapterContentStore(workspace.Path).GetChapterAsync("vol1_ch4");
        var stats = recorder.GetStatistics();

        Assert.False(result.Success);
        Assert.True(result.RequiresManualIntervention);
        Assert.Null(saved);
        Assert.Equal(1, stats.TotalGenerations);
        Assert.Equal(1, stats.FinalFailureCount);
        Assert.Equal(1, stats.ConsistencyFailureCount);
    }

    [Fact]
    public async Task SaveGeneratedChapterStrictAsync_dispatches_tracking_only_after_successful_save()
    {
        using var workspace = new TempDirectory();
        var sink = new RecordingTrackingSink();
        var recorder = new GenerationStatisticsRecorder();
        var pipeline = CreatePipeline(workspace.Path, recorder, new ChapterTrackingDispatcher(sink));

        var result = await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch5",
            """
            林衡束起黑发。
            ---CHANGES---
            {
              "CharacterStateChanges": [
                { "CharacterId": "C7M3VT2K9P4NA", "NewLevel": "A", "NewAbilities": [], "LostAbilities": [], "RelationshipChanges": {}, "NewMentalState": "镇定", "KeyEvent": "整理行装", "Importance": "normal" }
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
            """,
            new FactSnapshot
            {
                CharacterStates = [new CharacterStateSnapshot { Id = "C7M3VT2K9P4NA", Stage = "B" }],
                CharacterDescriptions =
                {
                    ["C7M3VT2K9P4NA"] = new CharacterCoreDescription { Name = "林衡", HairColor = "黑发" }
                }
            },
            packagedTitle: "追踪章节");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(["character:vol1_ch5:C7M3VT2K9P4NA", "time:vol1_ch5:第一日", "refresh:vol1_ch5"], sink.Events);
    }

    [Fact]
    public async Task SaveGeneratedChapterStrictAsync_can_persist_tracking_files_with_file_sink()
    {
        using var workspace = new TempDirectory();
        var recorder = new GenerationStatisticsRecorder();
        var trackingSink = new FileChapterTrackingSink(workspace.Path);
        var pipeline = CreatePipeline(workspace.Path, recorder, new ChapterTrackingDispatcher(trackingSink));

        var result = await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch6",
            """
            林衡束起黑发。
            ---CHANGES---
            {
              "CharacterStateChanges": [
                { "CharacterId": "C7M3VT2K9P4NA", "NewLevel": "A", "NewAbilities": [], "LostAbilities": [], "RelationshipChanges": {}, "NewMentalState": "镇定", "KeyEvent": "进入山门", "Importance": "normal" }
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
            """,
            new FactSnapshot
            {
                CharacterStates = [new CharacterStateSnapshot { Id = "C7M3VT2K9P4NA", Stage = "B" }],
                CharacterDescriptions =
                {
                    ["C7M3VT2K9P4NA"] = new CharacterCoreDescription { Name = "林衡", HairColor = "黑发" }
                }
            },
            packagedTitle: "文件追踪");

        Assert.True(result.Success, result.ErrorMessage);
        var characterGuide = await ReadJsonAsync<CharacterStateGuide>(workspace.Path, "character_state_guide_vol1.json");
        var timelineGuide = await ReadJsonAsync<TimelineGuide>(workspace.Path, "timeline_guide_vol1.json");

        Assert.Equal("# 第6章 文件追踪\n\n林衡束起黑发。", await new ChapterContentStore(workspace.Path).GetChapterAsync("vol1_ch6"));
        Assert.Equal("A", characterGuide.Characters["C7M3VT2K9P4NA"].StateHistory.Single().Level);
        Assert.Equal("进入山门", characterGuide.Characters["C7M3VT2K9P4NA"].StateHistory.Single().KeyEvent);
        Assert.Equal("第一日", timelineGuide.ChapterTimeline.Single().TimePeriod);
    }

    [Fact]
    public async Task SaveGeneratedChapterStrictAsync_does_not_persist_tracking_files_when_gate_fails()
    {
        using var workspace = new TempDirectory();
        var recorder = new GenerationStatisticsRecorder();
        var trackingSink = new FileChapterTrackingSink(workspace.Path);
        var pipeline = CreatePipeline(workspace.Path, recorder, new ChapterTrackingDispatcher(trackingSink));

        var result = await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch7",
            """
            林衡束起金发。
            ---CHANGES---
            {
              "CharacterStateChanges": [
                { "CharacterId": "C7M3VT2K9P4NA", "NewLevel": "A", "NewAbilities": [], "LostAbilities": [], "RelationshipChanges": {}, "NewMentalState": "镇定", "KeyEvent": "错误更新", "Importance": "normal" }
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
            """,
            new FactSnapshot
            {
                CharacterDescriptions =
                {
                    ["C7M3VT2K9P4NA"] = new CharacterCoreDescription { Name = "林衡", HairColor = "黑发" }
                }
            },
            packagedTitle: "失败追踪");

        Assert.False(result.Success);
        Assert.Null(await new ChapterContentStore(workspace.Path).GetChapterAsync("vol1_ch7"));
        Assert.False(File.Exists(System.IO.Path.Combine(workspace.Path, "character_state_guide_vol1.json")));
        Assert.False(File.Exists(System.IO.Path.Combine(workspace.Path, "timeline_guide_vol1.json")));
    }

    [Fact]
    public async Task DeleteChapterAsync_removes_chapter_file_and_recalculates_tracking_guides()
    {
        using var workspace = new TempDirectory();
        var recorder = new GenerationStatisticsRecorder();
        var trackingSink = new FileChapterTrackingSink(workspace.Path);
        var pipeline = CreatePipeline(workspace.Path, recorder, new ChapterTrackingDispatcher(trackingSink));

        await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch8",
            """
            林衡束起黑发。
            ---CHANGES---
            {
              "CharacterStateChanges": [
                { "CharacterId": "C7M3VT2K9P4NA", "NewLevel": "B", "NewAbilities": [], "LostAbilities": [], "RelationshipChanges": {}, "NewMentalState": "镇定", "KeyEvent": "入门", "Importance": "normal" }
              ],
              "ConflictProgress": [],
              "ForeshadowingActions": [],
              "NewPlotPoints": [],
              "LocationStateChanges": [
                { "LocationId": "L7M3VT2K9P4NA", "NewStatus": "关闭", "Event": "山门关闭", "Importance": "normal" }
              ],
              "FactionStateChanges": [],
              "TimeProgression": { "TimePeriod": "第一日", "ElapsedTime": "片刻", "KeyTimeEvent": "入门", "Importance": "normal" },
              "CharacterMovements": [],
              "ItemTransfers": []
            }
            """,
            ValidFactSnapshot(),
            packagedTitle: "入门");
        await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch9",
            """
            林衡束起黑发。
            ---CHANGES---
            {
              "CharacterStateChanges": [
                { "CharacterId": "C7M3VT2K9P4NA", "NewLevel": "A", "NewAbilities": [], "LostAbilities": [], "RelationshipChanges": {}, "NewMentalState": "坚定", "KeyEvent": "进阶", "Importance": "normal" }
              ],
              "ConflictProgress": [],
              "ForeshadowingActions": [],
              "NewPlotPoints": [],
              "LocationStateChanges": [
                { "LocationId": "L7M3VT2K9P4NA", "NewStatus": "开启", "Event": "山门开启", "Importance": "normal" }
              ],
              "FactionStateChanges": [],
              "TimeProgression": { "TimePeriod": "第二日", "ElapsedTime": "一日", "KeyTimeEvent": "进阶", "Importance": "normal" },
              "CharacterMovements": [],
              "ItemTransfers": []
            }
            """,
            ValidFactSnapshot(),
            packagedTitle: "进阶");

        var deleted = await pipeline.DeleteChapterAsync("vol1_ch9");

        var characterGuide = await ReadJsonAsync<CharacterStateGuide>(workspace.Path, "character_state_guide_vol1.json");
        var locationGuide = await ReadJsonAsync<LocationStateGuide>(workspace.Path, "location_state_guide_vol1.json");
        var timelineGuide = await ReadJsonAsync<TimelineGuide>(workspace.Path, "timeline_guide_vol1.json");

        Assert.True(deleted);
        Assert.Null(await new ChapterContentStore(workspace.Path).GetChapterAsync("vol1_ch9"));
        Assert.Equal("B", characterGuide.Characters["C7M3VT2K9P4NA"].StateHistory.Single().Level);
        Assert.Equal("关闭", locationGuide.Locations["L7M3VT2K9P4NA"].CurrentStatus);
        Assert.DoesNotContain(timelineGuide.ChapterTimeline, entry => entry.ChapterId == "vol1_ch9");
    }

    [Fact]
    public async Task SaveGeneratedChapterStrictAsync_replaces_tracking_data_when_overwriting_existing_chapter()
    {
        using var workspace = new TempDirectory();
        var recorder = new GenerationStatisticsRecorder();
        var trackingSink = new FileChapterTrackingSink(workspace.Path);
        var keywordIndex = new FileChapterKeywordIndex(workspace.Path);
        var pipeline = CreatePipeline(
            workspace.Path,
            recorder,
            new ChapterTrackingDispatcher(trackingSink),
            keywordIndex);

        await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch10",
            """
            林衡束起黑发。
            ---CHANGES---
            {
              "CharacterStateChanges": [
                { "CharacterId": "C7M3VT2K9P4NA", "NewLevel": "B", "NewAbilities": [], "LostAbilities": [], "RelationshipChanges": {}, "NewMentalState": "镇定", "KeyEvent": "初稿", "Importance": "normal" }
              ],
              "ConflictProgress": [],
              "ForeshadowingActions": [],
              "NewPlotPoints": [
                { "Keywords": ["初稿线索"], "Context": "旧线索", "InvolvedCharacters": ["C7M3VT2K9P4NA"], "Importance": "normal", "Storyline": "主线" }
              ],
              "LocationStateChanges": [],
              "FactionStateChanges": [],
              "TimeProgression": { "TimePeriod": "第一日", "ElapsedTime": "片刻", "KeyTimeEvent": "初稿", "Importance": "normal" },
              "CharacterMovements": [],
              "ItemTransfers": []
            }
            """,
            ValidFactSnapshot(),
            packagedTitle: "初稿");
        await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch10",
            """
            林衡束起黑发。
            ---CHANGES---
            {
              "CharacterStateChanges": [
                { "CharacterId": "C7M3VT2K9P4NA", "NewLevel": "A", "NewAbilities": [], "LostAbilities": [], "RelationshipChanges": {}, "NewMentalState": "坚定", "KeyEvent": "重写", "Importance": "normal" }
              ],
              "ConflictProgress": [],
              "ForeshadowingActions": [],
              "NewPlotPoints": [
                { "Keywords": ["重写线索"], "Context": "新线索", "InvolvedCharacters": ["C7M3VT2K9P4NA"], "Importance": "normal", "Storyline": "主线" }
              ],
              "LocationStateChanges": [],
              "FactionStateChanges": [],
              "TimeProgression": { "TimePeriod": "第二日", "ElapsedTime": "一日", "KeyTimeEvent": "重写", "Importance": "normal" },
              "CharacterMovements": [],
              "ItemTransfers": []
            }
            """,
            ValidFactSnapshot(),
            packagedTitle: "重写");

        var characterGuide = await ReadJsonAsync<CharacterStateGuide>(workspace.Path, "character_state_guide_vol1.json");
        var timelineGuide = await ReadJsonAsync<TimelineGuide>(workspace.Path, "timeline_guide_vol1.json");

        Assert.Equal("# 第10章 重写\n\n林衡束起黑发。", await new ChapterContentStore(workspace.Path).GetChapterAsync("vol1_ch10"));
        Assert.Equal("A", characterGuide.Characters["C7M3VT2K9P4NA"].StateHistory.Single().Level);
        Assert.Equal("重写", characterGuide.Characters["C7M3VT2K9P4NA"].StateHistory.Single().KeyEvent);
        Assert.Equal("第二日", timelineGuide.ChapterTimeline.Single(entry => entry.ChapterId == "vol1_ch10").TimePeriod);
        Assert.Empty(await keywordIndex.SearchAsync(["初稿线索"]));
        Assert.Equal(["vol1_ch10"], await keywordIndex.SearchAsync(["重写线索"]));
    }

    [Fact]
    public async Task DeleteChapterAsync_removes_keyword_index_even_when_tracking_cleanup_fails()
    {
        using var workspace = new TempDirectory();
        var recorder = new GenerationStatisticsRecorder();
        var keywordIndex = new FileChapterKeywordIndex(workspace.Path);
        var pipeline = CreatePipeline(
            workspace.Path,
            recorder,
            new ChapterTrackingDispatcher(new ThrowingRemoveTrackingSink()),
            keywordIndex);

        await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch11",
            """
            林衡束起黑发。
            ---CHANGES---
            {
              "CharacterStateChanges": [],
              "ConflictProgress": [],
              "ForeshadowingActions": [],
              "NewPlotPoints": [
                { "Keywords": ["清理线索"], "Context": "待清理", "InvolvedCharacters": [], "Importance": "normal", "Storyline": "主线" }
              ],
              "LocationStateChanges": [],
              "FactionStateChanges": [],
              "TimeProgression": { "TimePeriod": "第一日", "ElapsedTime": "片刻", "KeyTimeEvent": "入门", "Importance": "normal" },
              "CharacterMovements": [],
              "ItemTransfers": []
            }
            """,
            ValidFactSnapshot(),
            packagedTitle: "清理");

        var deleted = await pipeline.DeleteChapterAsync("vol1_ch11");

        Assert.True(deleted);
        Assert.Empty(await keywordIndex.SearchAsync(["清理线索"]));
    }

    [Fact]
    public async Task SaveGeneratedChapterStrictAsync_updates_derived_indexes_after_successful_save()
    {
        using var workspace = new TempDirectory();
        var recorder = new GenerationStatisticsRecorder();
        var derivedIndex = new RecordingDerivedIndex();
        var pipeline = CreatePipeline(
            workspace.Path,
            recorder,
            derivedIndexes: [derivedIndex]);

        var result = await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch12",
            """
            林衡束起黑发，踏入雨夜。
            ---CHANGES---
            {
              "CharacterStateChanges": [],
              "ConflictProgress": [],
              "ForeshadowingActions": [],
              "NewPlotPoints": [
                { "Keywords": ["雨夜线索"], "Context": "雨夜入山", "InvolvedCharacters": [], "Importance": "normal", "Storyline": "主线" }
              ],
              "LocationStateChanges": [],
              "FactionStateChanges": [],
              "TimeProgression": { "TimePeriod": "第一日", "ElapsedTime": "片刻", "KeyTimeEvent": "入山", "Importance": "normal" },
              "CharacterMovements": [],
              "ItemTransfers": []
            }
            """,
            ValidFactSnapshot(),
            packagedTitle: "雨夜入山");

        Assert.True(result.Success, result.ErrorMessage);
        var update = Assert.Single(derivedIndex.Updates);
        Assert.Equal("vol1_ch12", update.ChapterId);
        Assert.EndsWith("vol1_ch12.md", update.ChapterFilePath);
        Assert.Contains("雨夜入山", update.PersistedContent);
        Assert.Contains("雨夜线索", update.Changes.NewPlotPoints.Single().Keywords);
    }

    [Fact]
    public async Task SaveGeneratedChapterStrictAsync_treats_derived_index_failure_as_non_fatal()
    {
        using var workspace = new TempDirectory();
        var recorder = new GenerationStatisticsRecorder();
        var pipeline = CreatePipeline(
            workspace.Path,
            recorder,
            derivedIndexes: [new ThrowingDerivedIndex()]);

        var result = await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch13",
            "林衡束起黑发。\n---CHANGES---\n" + EmptyChangesJson(),
            ValidFactSnapshot(),
            packagedTitle: "索引失败不阻断");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("# 第13章 索引失败不阻断\n\n林衡束起黑发。", await new ChapterContentStore(workspace.Path).GetChapterAsync("vol1_ch13"));
    }

    [Fact]
    public async Task Rewriting_chapter_removes_derived_index_before_reindexing()
    {
        using var workspace = new TempDirectory();
        var recorder = new GenerationStatisticsRecorder();
        var derivedIndex = new RecordingDerivedIndex();
        var pipeline = CreatePipeline(
            workspace.Path,
            recorder,
            derivedIndexes: [derivedIndex]);

        await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch14",
            "林衡束起黑发。\n---CHANGES---\n" + EmptyChangesJson(),
            ValidFactSnapshot(),
            packagedTitle: "旧稿");
        await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch14",
            "林衡束起黑发，再入山门。\n---CHANGES---\n" + EmptyChangesJson(),
            ValidFactSnapshot(),
            packagedTitle: "新稿");

        Assert.Equal(["update:vol1_ch14", "remove:vol1_ch14", "update:vol1_ch14"], derivedIndex.Events);
    }

    private static ChapterGenerationPipeline CreatePipeline(
        string path,
        GenerationStatisticsRecorder recorder,
        ChapterTrackingDispatcher? trackingDispatcher = null,
        FileChapterKeywordIndex? keywordIndex = null,
        IReadOnlyList<IChapterDerivedIndex>? derivedIndexes = null)
    {
        var gate = new GenerationGate(new LedgerConsistencyChecker(), new LedgerRuleSetProvider());
        return new ChapterGenerationPipeline(
            new ContentGenerationPreparer(gate),
            new ChapterContentStore(path),
            recorder,
            trackingDispatcher,
            keywordIndex,
            derivedIndexes);
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

    private static FactSnapshot ValidFactSnapshot()
    {
        return new FactSnapshot
        {
            CharacterDescriptions =
            {
                ["C7M3VT2K9P4NA"] = new CharacterCoreDescription { Name = "林衡", HairColor = "黑发" }
            }
        };
    }

    private static async Task<T> ReadJsonAsync<T>(string root, string relativePath)
    {
        var json = await File.ReadAllTextAsync(System.IO.Path.Combine(root, relativePath));
        return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-pipeline-{Guid.NewGuid():N}");

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

    private sealed class RecordingTrackingSink : IChapterTrackingSink
    {
        public List<string> Events { get; } = new();

        public Task UpdateCharacterStateAsync(string chapterId, CharacterStateChange change)
        {
            Events.Add($"character:{chapterId}:{change.CharacterId}");
            return Task.CompletedTask;
        }

        public Task UpdateConflictProgressAsync(string chapterId, ConflictProgressChange change) => Task.CompletedTask;
        public Task AddPlotPointAsync(string chapterId, PlotPointChange change) => Task.CompletedTask;
        public Task MarkForeshadowingAsSetupAsync(string foreshadowId, string chapterId) => Task.CompletedTask;
        public Task MarkForeshadowingAsResolvedAsync(string foreshadowId, string chapterId) => Task.CompletedTask;

        public Task RefreshForeshadowingOverdueStatusAsync(string chapterId)
        {
            Events.Add($"refresh:{chapterId}");
            return Task.CompletedTask;
        }

        public Task UpdateLocationStateAsync(string chapterId, LocationStateChange change) => Task.CompletedTask;
        public Task UpdateFactionStateAsync(string chapterId, FactionStateChange change) => Task.CompletedTask;

        public Task UpdateTimeProgressionAsync(string chapterId, TimeProgressionChange change)
        {
            Events.Add($"time:{chapterId}:{change.TimePeriod}");
            return Task.CompletedTask;
        }

        public Task UpdateCharacterMovementsAsync(string chapterId, List<CharacterMovementChange> movements) => Task.CompletedTask;
        public Task UpdateItemStateAsync(string chapterId, ItemTransferChange change) => Task.CompletedTask;
        public Task RecordTrackingDebtsAsync(string chapterId, IReadOnlyList<TrackingDebt> debts) => Task.CompletedTask;
        public Task<IReadOnlyList<TrackingDebt>> LoadTrackingDebtsAsync(int volume) => Task.FromResult<IReadOnlyList<TrackingDebt>>(Array.Empty<TrackingDebt>());
        public Task RemoveCharacterStateAsync(string chapterId) => Task.CompletedTask;
        public Task RemoveConflictProgressAsync(string chapterId) => Task.CompletedTask;
        public Task RemovePlotPointsAsync(string chapterId) => Task.CompletedTask;
        public Task RemoveForeshadowingStatusAsync(string chapterId) => Task.CompletedTask;
        public Task RemoveLocationStateAsync(string chapterId) => Task.CompletedTask;
        public Task RemoveFactionStateAsync(string chapterId) => Task.CompletedTask;
        public Task RemoveTimelineAsync(string chapterId) => Task.CompletedTask;
        public Task RemoveItemStateAsync(string chapterId) => Task.CompletedTask;
    }

    private sealed class ThrowingRemoveTrackingSink : IChapterTrackingSink
    {
        public Task UpdateCharacterStateAsync(string chapterId, CharacterStateChange change) => Task.CompletedTask;
        public Task UpdateConflictProgressAsync(string chapterId, ConflictProgressChange change) => Task.CompletedTask;
        public Task AddPlotPointAsync(string chapterId, PlotPointChange change) => Task.CompletedTask;
        public Task MarkForeshadowingAsSetupAsync(string foreshadowId, string chapterId) => Task.CompletedTask;
        public Task MarkForeshadowingAsResolvedAsync(string foreshadowId, string chapterId) => Task.CompletedTask;
        public Task RefreshForeshadowingOverdueStatusAsync(string chapterId) => Task.CompletedTask;
        public Task UpdateLocationStateAsync(string chapterId, LocationStateChange change) => Task.CompletedTask;
        public Task UpdateFactionStateAsync(string chapterId, FactionStateChange change) => Task.CompletedTask;
        public Task UpdateTimeProgressionAsync(string chapterId, TimeProgressionChange change) => Task.CompletedTask;
        public Task UpdateCharacterMovementsAsync(string chapterId, List<CharacterMovementChange> movements) => Task.CompletedTask;
        public Task UpdateItemStateAsync(string chapterId, ItemTransferChange change) => Task.CompletedTask;
        public Task RecordTrackingDebtsAsync(string chapterId, IReadOnlyList<TrackingDebt> debts) => Task.CompletedTask;
        public Task<IReadOnlyList<TrackingDebt>> LoadTrackingDebtsAsync(int volume) => Task.FromResult<IReadOnlyList<TrackingDebt>>(Array.Empty<TrackingDebt>());
        public Task RemoveCharacterStateAsync(string chapterId) => throw new InvalidOperationException("tracking cleanup failed");
        public Task RemoveConflictProgressAsync(string chapterId) => Task.CompletedTask;
        public Task RemovePlotPointsAsync(string chapterId) => Task.CompletedTask;
        public Task RemoveForeshadowingStatusAsync(string chapterId) => Task.CompletedTask;
        public Task RemoveLocationStateAsync(string chapterId) => Task.CompletedTask;
        public Task RemoveFactionStateAsync(string chapterId) => Task.CompletedTask;
        public Task RemoveTimelineAsync(string chapterId) => Task.CompletedTask;
        public Task RemoveItemStateAsync(string chapterId) => Task.CompletedTask;
    }

    private sealed class RecordingDerivedIndex : IChapterDerivedIndex
    {
        public List<string> Events { get; } = new();
        public List<(string ChapterId, string ChapterFilePath, string PersistedContent, ChapterChanges Changes)> Updates { get; } = new();

        public Task IndexChapterAsync(string chapterId, string chapterFilePath, string persistedContent, ChapterChanges? changes)
        {
            Events.Add($"update:{chapterId}");
            Updates.Add((chapterId, chapterFilePath, persistedContent, changes!));
            return Task.CompletedTask;
        }

        public Task RemoveChapterAsync(string chapterId)
        {
            Events.Add($"remove:{chapterId}");
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingDerivedIndex : IChapterDerivedIndex
    {
        public Task IndexChapterAsync(string chapterId, string chapterFilePath, string persistedContent, ChapterChanges? changes)
        {
            throw new InvalidOperationException("index failed");
        }

        public Task RemoveChapterAsync(string chapterId)
        {
            throw new InvalidOperationException("remove failed");
        }
    }
}
