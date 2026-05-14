using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Generation.Wal;
using Xunit;

namespace Tianming.ProjectData.Tests.Generation.Wal;

public class GenerationRecoveryServiceTests
{
    [Fact]
    public async Task ReplayAsync_recovers_pending_chapter()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-rec-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var journal = new FileGenerationJournal(dir);
        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-001", Step = GenerationStep.ContentSaved });

        var calls = new List<string>();
        var service = new GenerationRecoveryService(journal, async (chapterId, fromStep, ct) =>
        {
            calls.Add($"{chapterId}:{fromStep}");
            await Task.CompletedTask;
        });

        var replayed = await service.ReplayAsync();

        Assert.Equal(1, replayed);
        Assert.Single(calls);
        Assert.Equal("ch-001:ContentSaved", calls[0]);
    }

    [Fact]
    public async Task ReplayAsync_skips_done_chapters()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-rec-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        var journal = new FileGenerationJournal(dir);
        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-001", Step = GenerationStep.Done });

        var calls = new List<string>();
        var service = new GenerationRecoveryService(journal, async (chapterId, fromStep, ct) =>
        {
            calls.Add($"{chapterId}:{fromStep}");
            await Task.CompletedTask;
        });

        var replayed = await service.ReplayAsync();

        Assert.Equal(0, replayed);
        Assert.Empty(calls);
    }
}
