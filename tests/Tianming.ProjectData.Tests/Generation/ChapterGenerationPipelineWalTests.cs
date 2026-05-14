using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Generation.Wal;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Generation;

public class ChapterGenerationPipelineWalTests
{
    [Fact]
    public async Task Successful_save_appends_steps_and_clears_journal()
    {
        using var workspace = new TempDirectory();
        var recorder = new GenerationStatisticsRecorder();
        var journal = new RecordingJournal();
        var sink = new FileChapterTrackingSink(workspace.Path);
        var pipeline = CreatePipeline(workspace.Path, recorder, journal, new ChapterTrackingDispatcher(sink));

        var result = await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch20",
            """
            林衡束起黑发。
            ---CHANGES---
            {
              "CharacterStateChanges": [
                { "CharacterId": "C7M3VT2K9P4NA", "NewLevel": "A", "NewAbilities": [], "LostAbilities": [], "RelationshipChanges": {}, "NewMentalState": "镇定", "KeyEvent": "继续前进", "Importance": "normal" }
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
            ValidFactSnapshot(),
            packagedTitle: "日志成功");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(
            [GenerationStep.PrepareStart, GenerationStep.PrepareDone, GenerationStep.GateDone, GenerationStep.ContentSaved, GenerationStep.TrackingDone, GenerationStep.Done],
            journal.StepsByChapter["vol1_ch20"]);
        Assert.Equal(["vol1_ch20"], journal.ClearedChapterIds);
    }

    [Fact]
    public async Task Failed_gate_leaves_pending_journal()
    {
        using var workspace = new TempDirectory();
        var recorder = new GenerationStatisticsRecorder();
        var journal = new RecordingJournal();
        var pipeline = CreatePipeline(workspace.Path, recorder, journal);

        var result = await pipeline.SaveGeneratedChapterStrictAsync(
            "vol1_ch21",
            "林衡束起金发。\n---CHANGES---\n" + EmptyChangesJson(),
            ValidFactSnapshot(),
            packagedTitle: "日志失败");

        Assert.False(result.Success);
        Assert.Equal(
            [GenerationStep.PrepareStart, GenerationStep.PrepareDone],
            journal.StepsByChapter["vol1_ch21"]);
        Assert.Empty(journal.ClearedChapterIds);
    }

    private static ChapterGenerationPipeline CreatePipeline(
        string path,
        GenerationStatisticsRecorder recorder,
        IGenerationJournal journal,
        ChapterTrackingDispatcher? trackingDispatcher = null)
    {
        var gate = new GenerationGate(new LedgerConsistencyChecker(), new LedgerRuleSetProvider());
        return new ChapterGenerationPipeline(
            new ContentGenerationPreparer(gate),
            new ChapterContentStore(path),
            recorder,
            trackingDispatcher,
            journal: journal);
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

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-pipeline-wal-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            System.IO.Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (System.IO.Directory.Exists(Path))
                System.IO.Directory.Delete(Path, recursive: true);
        }
    }

    private sealed class RecordingJournal : IGenerationJournal
    {
        public Dictionary<string, List<GenerationStep>> StepsByChapter { get; } = new(StringComparer.Ordinal);
        public List<string> ClearedChapterIds { get; } = [];

        public Task AppendAsync(GenerationJournalEntry entry, CancellationToken ct = default)
        {
            if (!StepsByChapter.TryGetValue(entry.ChapterId, out var steps))
            {
                steps = [];
                StepsByChapter[entry.ChapterId] = steps;
            }

            steps.Add(entry.Step);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<GenerationJournalEntry>> ReadAllAsync(string chapterId, CancellationToken ct = default)
        {
            if (!StepsByChapter.TryGetValue(chapterId, out var steps))
                return Task.FromResult<IReadOnlyList<GenerationJournalEntry>>([]);

            var entries = new List<GenerationJournalEntry>(steps.Count);
            foreach (var step in steps)
                entries.Add(new GenerationJournalEntry { ChapterId = chapterId, Step = step });
            return Task.FromResult<IReadOnlyList<GenerationJournalEntry>>(entries);
        }

        public Task<IReadOnlyList<string>> ListPendingAsync(CancellationToken ct = default)
        {
            var pending = new List<string>();
            foreach (var pair in StepsByChapter)
            {
                if (!pair.Value.Contains(GenerationStep.Done))
                    pending.Add(pair.Key);
            }

            return Task.FromResult<IReadOnlyList<string>>(pending);
        }

        public Task ClearAsync(string chapterId, CancellationToken ct = default)
        {
            ClearedChapterIds.Add(chapterId);
            return Task.CompletedTask;
        }
    }
}
