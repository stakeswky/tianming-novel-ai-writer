using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.StagedChanges;

public sealed class FileStagedChangeStore : IStagedChangeStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _stagedDirectory;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public FileStagedChangeStore(string projectRoot)
    {
        _stagedDirectory = Path.Combine(projectRoot, ".staged");
        Directory.CreateDirectory(_stagedDirectory);
    }

    public async Task<string> StageAsync(StagedChange change, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (string.IsNullOrWhiteSpace(change.Id))
            {
                change.Id = $"stg-{Guid.NewGuid():N}";
            }

            var path = GetPath(change.Id);
            var tempPath = path + ".tmp";
            var json = JsonSerializer.Serialize(change, JsonOptions);

            await File.WriteAllTextAsync(tempPath, json, ct).ConfigureAwait(false);
            File.Move(tempPath, path, overwrite: true);
            return change.Id;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<StagedChange?> GetAsync(string id, CancellationToken ct = default)
    {
        var path = GetPath(id);
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<StagedChange>(json, JsonOptions);
    }

    public async Task<IReadOnlyList<StagedChange>> ListPendingAsync(CancellationToken ct = default)
    {
        var pending = new List<StagedChange>();
        foreach (var file in Directory.GetFiles(_stagedDirectory, "*.json"))
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
                var change = JsonSerializer.Deserialize<StagedChange>(json, JsonOptions);
                if (change != null)
                {
                    pending.Add(change);
                }
            }
            catch (JsonException)
            {
                // Skip corrupt staged payloads so one bad file does not block the queue.
            }
        }

        return pending;
    }

    public Task RemoveAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var path = GetPath(id);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    private string GetPath(string id) => Path.Combine(_stagedDirectory, $"{id}.json");
}
