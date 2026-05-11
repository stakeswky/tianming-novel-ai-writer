using System.Text.Json;

namespace TM.Framework.Cleanup;

public enum PortableCleanupRiskLevel
{
    Low,
    Medium,
    High
}

public enum PortableCleanupMethod
{
    ClearContent,
    DeleteFile,
    ClearDirectory,
    DeleteNonBuiltIn,
    KeepModelCategoryLevel1,
    ClearProjectVolumesAndChapters,
    ClearProjectHistory
}

public sealed class PortableCleanupItem
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string RelativePath { get; init; } = string.Empty;

    public bool IsDirectory { get; init; }

    public PortableCleanupRiskLevel RiskLevel { get; init; } = PortableCleanupRiskLevel.Low;

    public string WarningMessage { get; init; } = string.Empty;

    public PortableCleanupMethod Method { get; init; } = PortableCleanupMethod.ClearContent;
}

public sealed class PortableCleanupFailure
{
    public required string ItemId { get; init; }

    public required string Path { get; init; }

    public required string Error { get; init; }
}

public sealed class PortableCleanupResult
{
    public int SucceededItems { get; internal set; }

    public int MissingItems { get; internal set; }

    public int ClearedFiles { get; internal set; }

    public int DeletedFiles { get; internal set; }

    public int DeletedDirectories { get; internal set; }

    public int UpdatedFiles { get; internal set; }

    public List<PortableCleanupFailure> Failures { get; } = [];
}

public sealed class PortableCleanupScanEntry
{
    public required string ItemId { get; init; }

    public required string Path { get; init; }

    public bool Exists { get; init; }

    public int FileCount { get; init; }

    public long TotalBytes { get; init; }
}

public sealed class PortableDataCleanupService
{
    private static readonly HashSet<string> ProtectedFileNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "built_in_categories.json",
        "standard_form_options.json",
        "provider-logos.json",
        "model-capabilities.json",
        "ip2region_v4.xdb",
        "model.onnx",
        "vocab.txt",
        "doudi.png"
    };

    private readonly string _storageRoot;
    private readonly string _storageRootWithSeparator;

    public PortableDataCleanupService(string storageRoot)
    {
        if (string.IsNullOrWhiteSpace(storageRoot))
        {
            throw new ArgumentException("Storage root is required.", nameof(storageRoot));
        }

        _storageRoot = Path.GetFullPath(storageRoot);
        _storageRootWithSeparator = EnsureTrailingSeparator(_storageRoot);
    }

    public IReadOnlyList<PortableCleanupScanEntry> Scan(IEnumerable<PortableCleanupItem> items)
    {
        var entries = new List<PortableCleanupScanEntry>();

        foreach (var item in items)
        {
            var fullPath = ResolveInsideRoot(item.RelativePath);
            if (fullPath is null)
            {
                entries.Add(new PortableCleanupScanEntry
                {
                    ItemId = item.Id,
                    Path = item.RelativePath,
                    Exists = false
                });
                continue;
            }

            if (Directory.Exists(fullPath))
            {
                var files = Directory.EnumerateFiles(fullPath, "*", SearchOption.AllDirectories)
                    .Where(file => !IsProtectedBuiltInFile(file))
                    .Select(file => new FileInfo(file))
                    .ToList();
                entries.Add(new PortableCleanupScanEntry
                {
                    ItemId = item.Id,
                    Path = fullPath,
                    Exists = true,
                    FileCount = files.Count,
                    TotalBytes = files.Sum(file => file.Length)
                });
            }
            else if (File.Exists(fullPath))
            {
                var file = new FileInfo(fullPath);
                entries.Add(new PortableCleanupScanEntry
                {
                    ItemId = item.Id,
                    Path = fullPath,
                    Exists = true,
                    FileCount = 1,
                    TotalBytes = file.Length
                });
            }
            else
            {
                entries.Add(new PortableCleanupScanEntry
                {
                    ItemId = item.Id,
                    Path = fullPath,
                    Exists = false
                });
            }
        }

        return entries;
    }

    public PortableCleanupResult Cleanup(IEnumerable<PortableCleanupItem> items)
    {
        var result = new PortableCleanupResult();

        foreach (var item in items)
        {
            var fullPath = ResolveInsideRoot(item.RelativePath);
            if (fullPath is null)
            {
                result.Failures.Add(new PortableCleanupFailure
                {
                    ItemId = item.Id,
                    Path = item.RelativePath,
                    Error = "Path is outside the configured storage root."
                });
                continue;
            }

            try
            {
                switch (item.Method)
                {
                    case PortableCleanupMethod.ClearContent:
                        ClearFileContent(fullPath, result);
                        break;
                    case PortableCleanupMethod.DeleteFile:
                        DeleteFileIfExists(fullPath, result);
                        break;
                    case PortableCleanupMethod.ClearDirectory:
                        ClearDirectoryFiles(fullPath, result);
                        break;
                    case PortableCleanupMethod.DeleteNonBuiltIn:
                        DeleteNonBuiltInTemplates(fullPath, result);
                        break;
                    case PortableCleanupMethod.KeepModelCategoryLevel1:
                        KeepModelCategoryLevel1(fullPath, result);
                        break;
                    case PortableCleanupMethod.ClearProjectVolumesAndChapters:
                        ClearProjectVolumesAndChapters(fullPath, result);
                        break;
                    case PortableCleanupMethod.ClearProjectHistory:
                        ClearProjectHistory(fullPath, result);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported cleanup method: {item.Method}.");
                }

                result.SucceededItems++;
            }
            catch (Exception ex)
            {
                result.Failures.Add(new PortableCleanupFailure
                {
                    ItemId = item.Id,
                    Path = fullPath,
                    Error = ex.Message
                });
            }
        }

        return result;
    }

    private void ClearFileContent(string filePath, PortableCleanupResult result)
    {
        if (!File.Exists(filePath))
        {
            result.MissingItems++;
            return;
        }

        var content = File.ReadAllText(filePath);
        File.WriteAllText(filePath, content.TrimStart().StartsWith("[", StringComparison.Ordinal) ? "[]" : "{}");
        result.ClearedFiles++;
    }

    private static void DeleteFileIfExists(string filePath, PortableCleanupResult result)
    {
        if (!File.Exists(filePath))
        {
            result.MissingItems++;
            return;
        }

        File.Delete(filePath);
        result.DeletedFiles++;
    }

    private static void ClearDirectoryFiles(string directory, PortableCleanupResult result)
    {
        if (!Directory.Exists(directory))
        {
            result.MissingItems++;
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
        {
            if (IsProtectedBuiltInFile(file))
            {
                continue;
            }

            File.Delete(file);
            result.DeletedFiles++;
        }
    }

    private static void DeleteNonBuiltInTemplates(string directory, PortableCleanupResult result)
    {
        if (!Directory.Exists(directory))
        {
            result.MissingItems++;
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories))
        {
            if (IsProtectedBuiltInFile(file))
            {
                continue;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(file));
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var builtInTemplates = document.RootElement.EnumerateArray()
                .Where(element =>
                    element.ValueKind == JsonValueKind.Object &&
                    element.TryGetProperty("IsBuiltIn", out var isBuiltIn) &&
                    isBuiltIn.ValueKind == JsonValueKind.True)
                .Select(element => element.Clone())
                .ToList();

            WriteJsonAtomically(file, builtInTemplates);
            result.UpdatedFiles++;
        }
    }

    private static void KeepModelCategoryLevel1(string filePath, PortableCleanupResult result)
    {
        if (!File.Exists(filePath))
        {
            result.MissingItems++;
            return;
        }

        using var document = JsonDocument.Parse(File.ReadAllText(filePath));
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var levelOneCategories = document.RootElement.EnumerateArray()
            .Where(element =>
                element.ValueKind == JsonValueKind.Object &&
                element.TryGetProperty("Level", out var level) &&
                level.TryGetInt32(out var value) &&
                value == 1)
            .Select(element => element.Clone())
            .ToList();

        WriteJsonAtomically(filePath, levelOneCategories);
        result.UpdatedFiles++;
    }

    private static void ClearProjectVolumesAndChapters(string projectsDirectory, PortableCleanupResult result)
    {
        if (!Directory.Exists(projectsDirectory))
        {
            result.MissingItems++;
            return;
        }

        foreach (var projectDirectory in Directory.EnumerateDirectories(projectsDirectory))
        {
            ClearProjectGeneratedData(projectDirectory, result);
            ClearProjectTrackingGuides(projectDirectory, result);
            ClearProjectVectorIndex(projectDirectory, result);
        }
    }

    private static void ClearProjectGeneratedData(string projectDirectory, PortableCleanupResult result)
    {
        var categoriesFile = Path.Combine(projectDirectory, "Generated", "categories.json");
        if (File.Exists(categoriesFile))
        {
            File.WriteAllText(categoriesFile, "[]");
            result.ClearedFiles++;
        }

        var chaptersDirectory = Path.Combine(projectDirectory, "Generated", "chapters");
        if (!Directory.Exists(chaptersDirectory))
        {
            return;
        }

        foreach (var pattern in new[] { "*.md", "*.bak", "*.staging" })
        {
            DeleteFiles(chaptersDirectory, pattern, SearchOption.AllDirectories, result);
        }
    }

    private static void ClearProjectTrackingGuides(string projectDirectory, PortableCleanupResult result)
    {
        var guidesDirectory = Path.Combine(projectDirectory, "Config", "guides");
        if (!Directory.Exists(guidesDirectory))
        {
            return;
        }

        var trackingPrefixes = new[]
        {
            "character_state_guide",
            "location_state_guide",
            "faction_state_guide",
            "item_state_guide",
            "timeline_guide",
            "conflict_progress_guide",
            "foreshadowing_status_guide",
            "chapter_summary"
        };

        foreach (var file in Directory.EnumerateFiles(guidesDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            var fileName = Path.GetFileName(file);
            if (trackingPrefixes.Any(prefix => fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            {
                File.Delete(file);
                result.DeletedFiles++;
            }
        }

        DeleteFiles(Path.Combine(guidesDirectory, "fact_archives"), "*.json", SearchOption.TopDirectoryOnly, result);
        DeleteFiles(Path.Combine(guidesDirectory, "milestones"), "*.*", SearchOption.TopDirectoryOnly, result);
        DeleteFiles(Path.Combine(guidesDirectory, "plot_points"), "*.json", SearchOption.TopDirectoryOnly, result);
        DeleteFiles(Path.Combine(guidesDirectory, "summaries"), "vol*.json", SearchOption.TopDirectoryOnly, result);

        var keywordIndexFile = Path.Combine(guidesDirectory, "keyword_index.json");
        if (File.Exists(keywordIndexFile))
        {
            File.Delete(keywordIndexFile);
            result.DeletedFiles++;
        }
    }

    private static void ClearProjectVectorIndex(string projectDirectory, PortableCleanupResult result)
    {
        DeleteFiles(Path.Combine(projectDirectory, "VectorIndex"), "*.json", SearchOption.TopDirectoryOnly, result);
    }

    private static void ClearProjectHistory(string projectsDirectory, PortableCleanupResult result)
    {
        if (!Directory.Exists(projectsDirectory))
        {
            result.MissingItems++;
            return;
        }

        foreach (var projectDirectory in Directory.EnumerateDirectories(projectsDirectory))
        {
            var historyDirectory = Path.Combine(projectDirectory, "History");
            if (!Directory.Exists(historyDirectory))
            {
                continue;
            }

            foreach (var versionDirectory in Directory.EnumerateDirectories(historyDirectory))
            {
                Directory.Delete(versionDirectory, recursive: true);
                result.DeletedDirectories++;
            }
        }
    }

    private static void DeleteFiles(
        string directory,
        string pattern,
        SearchOption searchOption,
        PortableCleanupResult result)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.EnumerateFiles(directory, pattern, searchOption))
        {
            File.Delete(file);
            result.DeletedFiles++;
        }
    }

    private string? ResolveInsideRoot(string relativePath)
    {
        if (Path.IsPathFullyQualified(relativePath))
        {
            return null;
        }

        var fullPath = Path.GetFullPath(Path.Combine(_storageRoot, relativePath));
        if (!string.Equals(fullPath, _storageRoot, StringComparison.Ordinal) &&
            !fullPath.StartsWith(_storageRootWithSeparator, StringComparison.Ordinal))
        {
            return null;
        }

        return fullPath;
    }

    private static bool IsProtectedBuiltInFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (ProtectedFileNames.Contains(fileName))
        {
            return true;
        }

        var normalizedPath = filePath.Replace('\\', '/');
        return normalizedPath.Contains("/built_in_templates/", StringComparison.OrdinalIgnoreCase);
    }

    private static void WriteJsonAtomically<T>(string filePath, T value)
    {
        var tempPath = filePath + ".tmp";
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(tempPath, JsonSerializer.Serialize(value, options));
        File.Move(tempPath, filePath, overwrite: true);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
        {
            return path;
        }

        return path + Path.DirectorySeparatorChar;
    }
}
