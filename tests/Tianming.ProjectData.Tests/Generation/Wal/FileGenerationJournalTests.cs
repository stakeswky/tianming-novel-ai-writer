using System;
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Generation.Wal;
using Xunit;

namespace Tianming.ProjectData.Tests.Generation.Wal;

public class FileGenerationJournalTests
{
    [Fact]
    public async Task Append_then_ReadAll_returns_entries_in_order()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-wal-{Guid.NewGuid():N}");
        var journal = new FileGenerationJournal(dir);

        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-001", Step = GenerationStep.PrepareStart });
        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-001", Step = GenerationStep.GateDone });
        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-001", Step = GenerationStep.ContentSaved });

        var entries = await journal.ReadAllAsync("ch-001");

        Assert.Equal(3, entries.Count);
        Assert.Equal(GenerationStep.PrepareStart, entries[0].Step);
        Assert.Equal(GenerationStep.GateDone, entries[1].Step);
        Assert.Equal(GenerationStep.ContentSaved, entries[2].Step);
    }

    [Fact]
    public async Task ListPending_returns_chapters_without_Done_step()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-wal-{Guid.NewGuid():N}");
        var journal = new FileGenerationJournal(dir);

        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-001", Step = GenerationStep.GateDone });
        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-002", Step = GenerationStep.PrepareStart });
        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-002", Step = GenerationStep.Done });

        var pending = await journal.ListPendingAsync();

        Assert.Contains("ch-001", pending);
        Assert.DoesNotContain("ch-002", pending);
    }

    [Fact]
    public async Task ClearAsync_removes_journal_file()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-wal-{Guid.NewGuid():N}");
        var journal = new FileGenerationJournal(dir);

        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = "ch-001", Step = GenerationStep.PrepareStart });

        await journal.ClearAsync("ch-001");

        var entries = await journal.ReadAllAsync("ch-001");
        Assert.Empty(entries);
    }
}
