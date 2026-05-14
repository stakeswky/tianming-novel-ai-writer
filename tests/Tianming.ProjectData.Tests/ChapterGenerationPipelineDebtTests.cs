using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Debts;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Models.Design.Location;
using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Models.Guides;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ChapterGenerationPipelineDebtTests
{
    [Fact]
    public async Task SaveGeneratedChapter_with_overdue_foreshadow_records_deadline_debt()
    {
        using var workspace = new TempDirectory();
        var sink = new InMemoryTrackingSink();
        var pipeline = new ChapterGenerationPipeline(
            new ContentGenerationPreparer(new GenerationGate(new LedgerConsistencyChecker(), new LedgerRuleSetProvider())),
            new ChapterContentStore(workspace.Path),
            new GenerationStatisticsRecorder(),
            new ChapterTrackingDispatcher(sink),
            factSnapshotGuideSource: new InMemoryFactSnapshotGuideSource(),
            debtRegistry: new TrackingDebtRegistry(new ITrackingDebtDetector[] { new DeadlineDetector() }));

        var result = await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch9",
            "林衡束起黑发。\n---CHANGES---\n" + EmptyChangesJson(),
            new FactSnapshot
            {
                CharacterDescriptions =
                {
                    ["C7M3VT2K9P4NA"] = new CharacterCoreDescription { Name = "林衡", HairColor = "黑发" }
                }
            },
            packagedTitle: "债务章节");

        Assert.True(result.Success, result.ErrorMessage);
        var debt = Assert.Single(sink.RecordedDebts);
        Assert.Equal(TrackingDebtCategory.Deadline, debt.Category);
    }

    [Fact]
    public async Task SaveGeneratedChapter_with_missing_pledge_guide_records_no_pledge_debt()
    {
        using var workspace = new TempDirectory();
        var sink = new InMemoryTrackingSink();
        var pipeline = CreatePipeline(workspace.Path, sink, new PledgeDetector());

        var result = await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch9",
            "林衡束起黑发。\n---CHANGES---\n" + EmptyChangesJson(),
            new FactSnapshot(),
            packagedTitle: "无承诺指南");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Empty(sink.RecordedDebts);
    }

    [Fact]
    public async Task SaveGeneratedChapter_loads_pledge_guide_from_volume_guides()
    {
        using var workspace = new TempDirectory();
        await WriteJsonAsync(
            System.IO.Path.Combine(workspace.Path, "vol1", "guides", "PledgeGuide.json"),
            new PledgeGuide
            {
                Pledges =
                {
                    ["pledge-1"] = new PledgeEntry
                    {
                        Name = "护送九璃",
                        PromisedAtChapter = "vol1_ch2",
                        DeadlineChapter = "vol1_ch4",
                        IsFulfilled = false,
                    },
                },
            });
        var sink = new InMemoryTrackingSink();
        var pipeline = CreatePipeline(workspace.Path, sink, new PledgeDetector());

        var result = await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch9",
            "林衡束起黑发。\n---CHANGES---\n" + EmptyChangesJson(),
            new FactSnapshot(),
            packagedTitle: "承诺过期");

        Assert.True(result.Success, result.ErrorMessage);
        var debt = Assert.Single(sink.RecordedDebts);
        Assert.Equal(TrackingDebtCategory.Pledge, debt.Category);
        Assert.Equal("pledge-1", debt.EntityId);
    }

    [Fact]
    public async Task SaveGeneratedChapter_with_missing_secret_guide_records_no_secret_debt()
    {
        using var workspace = new TempDirectory();
        var sink = new InMemoryTrackingSink();
        var pipeline = CreatePipeline(workspace.Path, sink, new SecretRevealDetector());

        var result = await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch9",
            "林衡束起黑发。\n---CHANGES---\n" + EmptyChangesJson(),
            new FactSnapshot(),
            packagedTitle: "无秘密指南");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Empty(sink.RecordedDebts);
    }

    [Fact]
    public async Task SaveGeneratedChapter_loads_secret_guide_from_volume_guides()
    {
        using var workspace = new TempDirectory();
        await WriteJsonAsync(
            System.IO.Path.Combine(workspace.Path, "vol1", "guides", "SecretGuide.json"),
            new SecretGuide
            {
                Secrets =
                {
                    ["secret-1"] = new SecretEntry
                    {
                        Name = "命火血脉",
                        IsRevealed = true,
                        ActualRevealChapter = "vol1_ch3",
                        ExpectedRevealChapter = "vol1_ch8",
                    },
                },
            });
        var sink = new InMemoryTrackingSink();
        var pipeline = CreatePipeline(workspace.Path, sink, new SecretRevealDetector());

        var result = await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch9",
            "林衡束起黑发。\n---CHANGES---\n" + EmptyChangesJson(),
            new FactSnapshot(),
            packagedTitle: "秘密提前揭露");

        Assert.True(result.Success, result.ErrorMessage);
        var debt = Assert.Single(sink.RecordedDebts);
        Assert.Equal(TrackingDebtCategory.SecretReveal, debt.Category);
        Assert.Equal("secret-1", debt.EntityId);
    }

    [Fact]
    public async Task SaveGeneratedChapter_persists_pledge_debt_to_tracking_debt_file()
    {
        using var workspace = new TempDirectory();
        await WriteJsonAsync(
            System.IO.Path.Combine(workspace.Path, "vol1", "guides", "PledgeGuide.json"),
            new PledgeGuide
            {
                Pledges =
                {
                    ["pledge-1"] = new PledgeEntry
                    {
                        Name = "归还玉牌",
                        PromisedAtChapter = "vol1_ch1",
                        DeadlineChapter = "vol1_ch2",
                        IsFulfilled = false,
                    },
                },
            });
        var pipeline = new ChapterGenerationPipeline(
            new ContentGenerationPreparer(new GenerationGate(new LedgerConsistencyChecker(), new LedgerRuleSetProvider())),
            new ChapterContentStore(workspace.Path),
            new GenerationStatisticsRecorder(),
            new ChapterTrackingDispatcher(new FileChapterTrackingSink(workspace.Path)),
            debtRegistry: new TrackingDebtRegistry(new ITrackingDebtDetector[] { new PledgeDetector() }));

        var result = await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch5",
            "林衡束起黑发。\n---CHANGES---\n" + EmptyChangesJson(),
            new FactSnapshot(),
            packagedTitle: "承诺持久化");

        Assert.True(result.Success, result.ErrorMessage);
        var persisted = await ReadJsonAsync<List<TrackingDebt>>(
            System.IO.Path.Combine(workspace.Path, "tracking_debts_vol1.json"));
        var debt = Assert.Single(persisted);
        Assert.Equal(TrackingDebtCategory.Pledge, debt.Category);
        Assert.Equal("pledge-1", debt.EntityId);
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

    private static ChapterGenerationPipeline CreatePipeline(
        string workspacePath,
        IChapterTrackingSink sink,
        params ITrackingDebtDetector[] detectors)
    {
        return new ChapterGenerationPipeline(
            new ContentGenerationPreparer(new GenerationGate(new LedgerConsistencyChecker(), new LedgerRuleSetProvider())),
            new ChapterContentStore(workspacePath),
            new GenerationStatisticsRecorder(),
            new ChapterTrackingDispatcher(sink),
            debtRegistry: new TrackingDebtRegistry(detectors));
    }

    private static async Task WriteJsonAsync<T>(string path, T value)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path)!);
        var json = System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true,
        });
        await File.WriteAllTextAsync(path, json);
    }

    private static async Task<T> ReadJsonAsync<T>(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        return System.Text.Json.JsonSerializer.Deserialize<T>(json, new System.Text.Json.JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        })!;
    }

    private sealed class InMemoryTrackingSink : IChapterTrackingSink
    {
        public List<TrackingDebt> RecordedDebts { get; } = new();

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
        public Task RecordTrackingDebtsAsync(string chapterId, IReadOnlyList<TrackingDebt> debts)
        {
            RecordedDebts.Clear();
            RecordedDebts.AddRange(debts);
            return Task.CompletedTask;
        }

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

    private sealed class InMemoryFactSnapshotGuideSource : IFactSnapshotGuideSource
    {
        public Task<CharacterStateGuide> GetCharacterStateGuideAsync(bool allVolumes, CancellationToken cancellationToken = default) => Task.FromResult(new CharacterStateGuide());
        public Task<ConflictProgressGuide> GetConflictProgressGuideAsync(bool allVolumes, CancellationToken cancellationToken = default) => Task.FromResult(new ConflictProgressGuide());
        public Task<ForeshadowingStatusGuide> GetForeshadowingStatusGuideAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new ForeshadowingStatusGuide
            {
                Foreshadowings = new()
                {
                    ["fs-1"] = new ForeshadowingStatusEntry
                    {
                        Name = "金鳞匕首",
                        IsOverdue = true,
                        IsResolved = false,
                        ExpectedPayoffChapter = "vol1_ch8",
                    },
                },
            });
        }

        public Task<LocationStateGuide> GetLocationStateGuideAsync(bool allVolumes, CancellationToken cancellationToken = default) => Task.FromResult(new LocationStateGuide());
        public Task<FactionStateGuide> GetFactionStateGuideAsync(bool allVolumes, CancellationToken cancellationToken = default) => Task.FromResult(new FactionStateGuide());
        public Task<TimelineGuide> GetTimelineGuideAsync(bool allVolumes, CancellationToken cancellationToken = default) => Task.FromResult(new TimelineGuide());
        public Task<ItemStateGuide> GetItemStateGuideAsync(bool allVolumes, CancellationToken cancellationToken = default) => Task.FromResult(new ItemStateGuide());
        public Task<IReadOnlyList<PlotPointEntry>> GetPlotPointsAsync(string currentChapterId, IReadOnlyCollection<string> characterIds, IReadOnlyCollection<string> otherEntityIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PlotPointEntry>>(Array.Empty<PlotPointEntry>());
        public Task<IReadOnlyList<CharacterRulesData>> GetCharactersAsync(IReadOnlyCollection<string> characterIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CharacterRulesData>>(Array.Empty<CharacterRulesData>());
        public Task<IReadOnlyList<LocationRulesData>> GetLocationsAsync(IReadOnlyCollection<string> locationIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<LocationRulesData>>(Array.Empty<LocationRulesData>());
        public Task<IReadOnlyList<WorldRulesData>> GetWorldRulesAsync(IReadOnlyCollection<string> worldRuleIds, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<WorldRulesData>>(Array.Empty<WorldRulesData>());
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-pipeline-debt-{Guid.NewGuid():N}");

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
