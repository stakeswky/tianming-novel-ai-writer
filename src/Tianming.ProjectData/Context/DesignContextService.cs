using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Context;

public sealed class DesignContextService : IDesignContextService
{
    private static readonly IReadOnlyDictionary<string, string> CategorySubDirs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Characters"] = "Design/Elements/Characters",
        ["WorldRules"] = "Design/GlobalSettings/WorldRules",
        ["Factions"] = "Design/Elements/Factions",
        ["Locations"] = "Design/Elements/Locations",
        ["Plot"] = "Design/Elements/Plot",
        ["CreativeMaterials"] = "Design/Templates/CreativeMaterials",
    };

    private readonly string _projectRoot;

    public DesignContextService(string projectRoot)
    {
        _projectRoot = projectRoot;
    }

    public async Task<IReadOnlyList<DesignReference>> ListByCategoryAsync(string category, CancellationToken ct = default)
    {
        if (!CategorySubDirs.TryGetValue(category, out var subDir))
            return Array.Empty<DesignReference>();

        var directory = Path.Combine(_projectRoot, subDir);
        if (!Directory.Exists(directory))
            return Array.Empty<DesignReference>();

        var results = new List<DesignReference>();
        foreach (var file in Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var reference = await ParseAsync(file, category, ct).ConfigureAwait(false);
            if (reference is not null)
                results.Add(reference);
        }

        return results;
    }

    public async Task<IReadOnlyList<DesignReference>> SearchAsync(string query, CancellationToken ct = default)
    {
        var results = new List<DesignReference>();
        foreach (var category in CategorySubDirs.Keys)
        {
            var items = await ListByCategoryAsync(category, ct).ConfigureAwait(false);
            results.AddRange(items.Where(item =>
                item.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                item.RawJson.Contains(query, StringComparison.OrdinalIgnoreCase)));
        }

        return results;
    }

    public async Task<DesignReference?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        foreach (var category in CategorySubDirs.Keys)
        {
            var items = await ListByCategoryAsync(category, ct).ConfigureAwait(false);
            var match = items.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
            if (match is not null)
                return match;
        }

        return null;
    }

    private static async Task<DesignReference?> ParseAsync(string file, string category, CancellationToken ct)
    {
        try
        {
            var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            var node = JsonNode.Parse(json) as JsonObject;
            if (node is null)
                return null;

            return new DesignReference
            {
                Id = node["Id"]?.ToString() ?? Path.GetFileNameWithoutExtension(file),
                Name = node["Name"]?.ToString() ?? string.Empty,
                Category = category,
                Summary = node["Summary"]?.ToString() ?? node["Description"]?.ToString() ?? string.Empty,
                RawJson = json,
            };
        }
        catch (IOException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
