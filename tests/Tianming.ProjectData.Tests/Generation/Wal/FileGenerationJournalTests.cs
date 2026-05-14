using System;
using System.IO;
using System.Linq;
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

    [Fact]
    public async Task Append_with_path_like_chapter_id_stays_inside_wal_directory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-wal-{Guid.NewGuid():N}");
        var journal = new FileGenerationJournal(dir);
        const string chapterId = "../vol1\\ch/007";

        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = chapterId, Step = GenerationStep.PrepareStart });

        var walRoot = Path.Combine(dir, ".wal");
        var files = Directory.GetFiles(walRoot, "*.journal.jsonl", SearchOption.AllDirectories);
        var directories = Directory.GetDirectories(walRoot, "*", SearchOption.AllDirectories);

        Assert.Single(files);
        Assert.Empty(directories);
        Assert.StartsWith(walRoot, Path.GetDirectoryName(files[0])!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ListPending_decodes_chapter_ids_with_separators_and_traversal_tokens()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-wal-{Guid.NewGuid():N}");
        var journal = new FileGenerationJournal(dir);
        const string chapterId = "../vol1\\ch/008";

        await journal.AppendAsync(new GenerationJournalEntry { ChapterId = chapterId, Step = GenerationStep.ContentSaved });

        var pending = await journal.ListPendingAsync();
        var entries = await journal.ReadAllAsync(chapterId);

        Assert.Equal([chapterId], pending.ToArray());
        Assert.Single(entries);
        Assert.Equal(chapterId, entries[0].ChapterId);
    }
}
