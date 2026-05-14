using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Generation.Wal;

public sealed class FileGenerationJournal : IGenerationJournal
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _walDirectory;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileGenerationJournal(string projectRoot)
    {
        _walDirectory = Path.Combine(projectRoot, ".wal");
        Directory.CreateDirectory(_walDirectory);
    }

    public async Task AppendAsync(GenerationJournalEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var line = JsonSerializer.Serialize(entry, JsonOptions);
            await File.AppendAllTextAsync(PathFor(entry.ChapterId), line + Environment.NewLine, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<IReadOnlyList<GenerationJournalEntry>> ReadAllAsync(string chapterId, CancellationToken ct = default)
    {
        var path = PathFor(chapterId);
        if (!File.Exists(path))
            return Array.Empty<GenerationJournalEntry>();

        var lines = await File.ReadAllLinesAsync(path, ct).ConfigureAwait(false);
        var entries = new List<GenerationJournalEntry>(lines.Length);
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            try
            {
                var entry = JsonSerializer.Deserialize<GenerationJournalEntry>(line, JsonOptions);
                if (entry != null)
                    entries.Add(entry);
            }
            catch (JsonException)
            {
                // Skip corrupt lines so later valid recovery markers are still readable.
            }
        }

        return entries;
    }

    public async Task<IReadOnlyList<string>> ListPendingAsync(CancellationToken ct = default)
    {
        if (!Directory.Exists(_walDirectory))
            return Array.Empty<string>();

        var pending = new List<string>();
        foreach (var path in Directory.GetFiles(_walDirectory, "*.journal.jsonl"))
        {
            ct.ThrowIfCancellationRequested();

            var chapterId = Path.GetFileName(path).Replace(".journal.jsonl", string.Empty, StringComparison.Ordinal);
            var entries = await ReadAllAsync(chapterId, ct).ConfigureAwait(false);
            if (entries.Count == 0)
                continue;

            if (entries.Any(entry => entry.Step == GenerationStep.Done))
                continue;

            pending.Add(chapterId);
        }

        return pending;
    }

    public Task ClearAsync(string chapterId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var path = PathFor(chapterId);
        if (File.Exists(path))
            File.Delete(path);

        return Task.CompletedTask;
    }

    private string PathFor(string chapterId) => Path.Combine(_walDirectory, $"{chapterId}.journal.jsonl");
}
