using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Generation.Wal;

public sealed class FileGenerationJournal : IGenerationJournal
{
    private const string EncodedFilePrefix = "cid-";
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
        var path = ResolvePath(chapterId);
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

            if (!TryGetChapterId(path, out var chapterId))
                continue;

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

        var encodedPath = PathFor(chapterId);
        if (File.Exists(encodedPath))
            File.Delete(encodedPath);

        var legacyPath = LegacyPathFor(chapterId);
        if (legacyPath != null && !string.Equals(encodedPath, legacyPath, StringComparison.Ordinal) && File.Exists(legacyPath))
            File.Delete(legacyPath);

        return Task.CompletedTask;
    }

    private string PathFor(string chapterId) => Path.Combine(_walDirectory, $"{EncodeChapterId(chapterId)}.journal.jsonl");

    private string ResolvePath(string chapterId)
    {
        var encodedPath = PathFor(chapterId);
        if (File.Exists(encodedPath))
            return encodedPath;

        var legacyPath = LegacyPathFor(chapterId);
        return legacyPath != null && File.Exists(legacyPath) ? legacyPath : encodedPath;
    }

    private static string EncodeChapterId(string chapterId)
    {
        if (string.IsNullOrWhiteSpace(chapterId))
            throw new ArgumentException("Chapter id cannot be empty.", nameof(chapterId));

        var bytes = Encoding.UTF8.GetBytes(chapterId);
        return EncodedFilePrefix + Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string? DecodeChapterId(string encodedChapterId)
    {
        if (!encodedChapterId.StartsWith(EncodedFilePrefix, StringComparison.Ordinal))
            return null;

        var payload = encodedChapterId[EncodedFilePrefix.Length..]
            .Replace('-', '+')
            .Replace('_', '/');
        var padding = payload.Length % 4;
        if (padding != 0)
            payload = payload.PadRight(payload.Length + (4 - padding), '=');

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool TryGetChapterId(string path, out string chapterId)
    {
        var fileName = Path.GetFileName(path);
        if (!fileName.EndsWith(".journal.jsonl", StringComparison.Ordinal))
        {
            chapterId = string.Empty;
            return false;
        }

        var stem = fileName[..^".journal.jsonl".Length];
        var decoded = DecodeChapterId(stem);
        if (!string.IsNullOrEmpty(decoded))
        {
            chapterId = decoded;
            return true;
        }

        chapterId = stem;
        return !string.IsNullOrWhiteSpace(chapterId);
    }

    private string? LegacyPathFor(string chapterId)
    {
        if (string.IsNullOrWhiteSpace(chapterId))
            return null;

        if (chapterId.Contains('/') || chapterId.Contains('\\') || chapterId.Contains("..", StringComparison.Ordinal))
            return null;

        return Path.Combine(_walDirectory, $"{chapterId}.journal.jsonl");
    }
}
