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
