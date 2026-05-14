using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.BookPipeline;

public sealed class FileBookGenerationJournal : IBookGenerationJournal
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _filePath;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public FileBookGenerationJournal(string projectRoot)
    {
        var dir = Path.Combine(projectRoot, ".book");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "pipeline.json");
    }

    public async Task<bool> IsCompletedAsync(string stepName, CancellationToken ct = default)
    {
        var map = await LoadAsync(ct).ConfigureAwait(false);
        return map.TryGetValue(stepName, out var status) && status == "Completed";
    }

    public async Task<bool> IsSkippedAsync(string stepName, CancellationToken ct = default)
    {
        var map = await LoadAsync(ct).ConfigureAwait(false);
        return map.TryGetValue(stepName, out var status) && status == "Skipped";
    }

    public async Task RecordCompletedAsync(string stepName, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var map = await LoadAsync(ct).ConfigureAwait(false);
            map[stepName] = "Completed";
            await SaveAsync(map, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task MarkSkippedAsync(string stepName, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var map = await LoadAsync(ct).ConfigureAwait(false);
            map[stepName] = "Skipped";
            await SaveAsync(map, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task ResetAsync(string stepName, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var map = await LoadAsync(ct).ConfigureAwait(false);
            map.Remove(stepName);
            await SaveAsync(map, ct).ConfigureAwait(false);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<Dictionary<string, string>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_filePath))
            return new Dictionary<string, string>();

        var json = await File.ReadAllTextAsync(_filePath, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? new Dictionary<string, string>();
    }

    private async Task SaveAsync(Dictionary<string, string> map, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(map, JsonOptions);
        var tempPath = _filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);
        File.Move(tempPath, _filePath, true);
    }
}
