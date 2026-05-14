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

        var dataPath = Path.Combine(_projectRoot, subDir, "data.json");
        if (!File.Exists(dataPath))
            return Array.Empty<DesignReference>();

        return await ParseModuleDataAsync(dataPath, category, ct).ConfigureAwait(false);
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

    private static async Task<IReadOnlyList<DesignReference>> ParseModuleDataAsync(string file, string category, CancellationToken ct)
    {
        try
        {
            var json = await File.ReadAllTextAsync(file, ct).ConfigureAwait(false);
            var root = JsonNode.Parse(json);
            if (root is JsonArray array)
            {
                var results = new List<DesignReference>();
                foreach (var item in array.OfType<JsonObject>())
                {
                    var reference = ParseItem(item, category);
                    if (reference is not null)
                        results.Add(reference);
                }

                return results;
            }

            if (root is JsonObject obj)
            {
                var reference = ParseItem(obj, category);
                return reference is null ? Array.Empty<DesignReference>() : [reference];
            }

            return Array.Empty<DesignReference>();
        }
        catch (IOException)
        {
            return Array.Empty<DesignReference>();
        }
        catch (JsonException)
        {
            return Array.Empty<DesignReference>();
        }
    }

    private static DesignReference? ParseItem(JsonObject node, string category)
    {
        var id = node["Id"]?.ToString();
        var name = node["Name"]?.ToString();
        if (string.IsNullOrWhiteSpace(id) && string.IsNullOrWhiteSpace(name))
            return null;

        return new DesignReference
        {
            Id = id ?? string.Empty,
            Name = name ?? string.Empty,
            Category = category,
            Summary = FirstNonEmpty(
                node["Summary"]?.ToString(),
                node["Description"]?.ToString(),
                node["OneLineSummary"]?.ToString(),
                node["OverallIdea"]?.ToString(),
                node["HardRules"]?.ToString(),
                node["Goal"]?.ToString(),
                node["Conflict"]?.ToString()),
            RawJson = node.ToJsonString(),
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
