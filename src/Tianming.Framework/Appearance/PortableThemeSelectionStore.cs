using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Appearance;

public sealed class PortableThemeSelectionData
{
    [JsonPropertyName("FavoriteIds")] public HashSet<string> FavoriteIds { get; set; } = new();

    [JsonPropertyName("RecentThemes")] public List<PortableRecentThemeRecord> RecentThemes { get; set; } = new();

    [JsonPropertyName("SearchHistory")] public List<string> SearchHistory { get; set; } = new();

    [JsonPropertyName("Preferences")] public PortableThemeSelectionPreferences Preferences { get; set; } = new();
}

public sealed class PortableRecentThemeRecord
{
    [JsonPropertyName("ThemeId")] public string ThemeId { get; set; } = string.Empty;

    [JsonPropertyName("ThemeName")] public string ThemeName { get; set; } = string.Empty;

    [JsonPropertyName("LastUsedTime")] public DateTime LastUsedTime { get; set; }
}

public sealed class PortableThemeSelectionPreferences
{
    [JsonPropertyName("LastSearchText")] public string LastSearchText { get; set; } = string.Empty;

    [JsonPropertyName("LastSelectedCategory")] public string LastSelectedCategory { get; set; } = "全部";

    [JsonPropertyName("LastSortMode")] public string LastSortMode { get; set; } = "默认排序";

    [JsonPropertyName("ShowOnlyFavorites")] public bool ShowOnlyFavorites { get; set; }
}

public sealed class FileThemeSelectionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly Func<DateTime> _clock;

    public FileThemeSelectionStore(string filePath, Func<DateTime>? clock = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Theme selection file path is required.", nameof(filePath))
            : filePath;
        _clock = clock ?? (() => DateTime.Now);
    }

    public async Task<PortableThemeSelectionData> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new PortableThemeSelectionData();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var data = await JsonSerializer.DeserializeAsync<PortableThemeSelectionData>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);

            return Normalize(data);
        }
        catch (JsonException)
        {
            return new PortableThemeSelectionData();
        }
        catch (IOException)
        {
            return new PortableThemeSelectionData();
        }
    }

    public async Task<bool> ToggleFavoriteAsync(string themeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(themeId))
        {
            throw new ArgumentNullException(nameof(themeId));
        }

        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var added = data.FavoriteIds.Add(themeId);
        if (!added)
        {
            data.FavoriteIds.Remove(themeId);
        }

        await SaveAsync(data, cancellationToken).ConfigureAwait(false);
        return added;
    }

    public async Task AddFavoriteAsync(string themeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(themeId))
        {
            throw new ArgumentNullException(nameof(themeId));
        }

        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        if (data.FavoriteIds.Add(themeId))
        {
            await SaveAsync(data, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RemoveFavoriteAsync(string themeId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(themeId))
        {
            throw new ArgumentNullException(nameof(themeId));
        }

        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        if (data.FavoriteIds.Remove(themeId))
        {
            await SaveAsync(data, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RecordRecentThemeAsync(
        string themeId,
        string themeName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(themeId))
        {
            throw new ArgumentNullException(nameof(themeId));
        }

        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        data.RecentThemes.RemoveAll(record => record.ThemeId == themeId);
        data.RecentThemes.Insert(
            0,
            new PortableRecentThemeRecord
            {
                ThemeId = themeId,
                ThemeName = themeName,
                LastUsedTime = _clock()
            });
        data.RecentThemes = data.RecentThemes.Take(20).ToList();

        await SaveAsync(data, cancellationToken).ConfigureAwait(false);
    }

    public async Task AddSearchHistoryAsync(string searchText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return;
        }

        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        data.SearchHistory.Remove(searchText);
        data.SearchHistory.Insert(0, searchText);
        data.SearchHistory = data.SearchHistory.Take(50).ToList();

        await SaveAsync(data, cancellationToken).ConfigureAwait(false);
    }

    public async Task ClearSearchHistoryAsync(CancellationToken cancellationToken = default)
    {
        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        data.SearchHistory.Clear();
        await SaveAsync(data, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdatePreferencesAsync(
        PortableThemeSelectionPreferences preferences,
        CancellationToken cancellationToken = default)
    {
        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        data.Preferences = preferences ?? new PortableThemeSelectionPreferences();
        await SaveAsync(data, cancellationToken).ConfigureAwait(false);
    }

    private async Task SaveAsync(PortableThemeSelectionData data, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                Normalize(data),
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }

    private static PortableThemeSelectionData Normalize(PortableThemeSelectionData? data)
    {
        return new PortableThemeSelectionData
        {
            FavoriteIds = data?.FavoriteIds ?? new HashSet<string>(),
            RecentThemes = data?.RecentThemes?.Take(20).ToList() ?? new List<PortableRecentThemeRecord>(),
            SearchHistory = data?.SearchHistory?.Take(50).ToList() ?? new List<string>(),
            Preferences = data?.Preferences ?? new PortableThemeSelectionPreferences()
        };
    }
}
