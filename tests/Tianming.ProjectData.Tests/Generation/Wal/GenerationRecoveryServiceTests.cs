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

    [Fact]
    public async Task ReplayAsync_resolves_journal_from_factory_each_call()
    {
        var dir1 = Path.Combine(Path.GetTempPath(), $"tm-rec-{Guid.NewGuid():N}");
        var dir2 = Path.Combine(Path.GetTempPath(), $"tm-rec-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir1);
        Directory.CreateDirectory(dir2);

        var journal1 = new FileGenerationJournal(dir1);
        var journal2 = new FileGenerationJournal(dir2);
        await journal1.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-001", Step = GenerationStep.GateDone });
        await journal2.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-002", Step = GenerationStep.ContentSaved });

        var calls = new List<string>();
        IGenerationJournal current = journal1;
        var service = new GenerationRecoveryService(() => current, async (chapterId, fromStep, ct) =>
        {
            calls.Add($"{chapterId}:{fromStep}");
            await Task.CompletedTask;
        });

        await service.ReplayAsync();
        current = journal2;
        await service.ReplayAsync();

        Assert.Equal(["ch-001:GateDone", "ch-002:ContentSaved"], calls);
    }
}
