using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Appearance;

public sealed class PortableGenerationHistoryRecord
{
    [JsonPropertyName("Id")] public string Id { get; set; } = string.Empty;

    [JsonPropertyName("Type")] public string Type { get; set; } = string.Empty;

    [JsonPropertyName("Name")] public string Name { get; set; } = string.Empty;

    [JsonPropertyName("Timestamp")] public DateTime Timestamp { get; set; }

    [JsonPropertyName("PrimaryColor")] public string PrimaryColor { get; set; } = string.Empty;

    [JsonPropertyName("SecondaryColor")] public string SecondaryColor { get; set; } = string.Empty;

    [JsonPropertyName("AccentColor")] public string AccentColor { get; set; } = string.Empty;

    [JsonPropertyName("BackgroundColor")] public string BackgroundColor { get; set; } = string.Empty;

    [JsonPropertyName("TextColor")] public string TextColor { get; set; } = string.Empty;

    [JsonPropertyName("Harmony")] public string Harmony { get; set; } = string.Empty;

    [JsonPropertyName("Keywords")] public string Keywords { get; set; } = string.Empty;

    [JsonPropertyName("IsFavorite")] public bool IsFavorite { get; set; }
}

public sealed class PortableGenerationHistoryStatistics
{
    public int TotalCount { get; set; }

    public int FavoriteCount { get; set; }

    public int ImagePickerCount { get; set; }

    public int AIGeneratedCount { get; set; }

    public int TodayCount { get; set; }

    public int ThisWeekCount { get; set; }

    public int ThisMonthCount { get; set; }
}

public sealed class FileGenerationHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly Func<string> _idFactory;
    private readonly Func<DateTime> _clock;

    public FileGenerationHistoryStore(
        string filePath,
        Func<string>? idFactory = null,
        Func<DateTime>? clock = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Generation history file path is required.", nameof(filePath))
            : filePath;
        _idFactory = idFactory ?? (() => "D" + Guid.NewGuid().ToString("N")[..12]);
        _clock = clock ?? (() => DateTime.Now);
    }

    public async Task<IReadOnlyList<PortableGenerationHistoryRecord>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return OrderNewestFirst(data.Records);
    }

    public async Task AddOrUpdateAsync(
        PortableGenerationHistoryRecord record,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(record);
        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(record.Id))
        {
            record.Id = _idFactory();
        }

        var existingIndex = data.Records.FindIndex(candidate => candidate.Id == record.Id);
        if (existingIndex >= 0)
        {
            data.Records[existingIndex] = record;
        }
        else
        {
            data.Records.Add(record);
        }

        await SaveAsync(data, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteRecordAsync(string recordId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recordId))
        {
            throw new ArgumentException("Record id is required.", nameof(recordId));
        }

        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        data.Records.RemoveAll(record => record.Id == recordId);
        await SaveAsync(data, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateFavoriteAsync(
        string recordId,
        bool isFavorite,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(recordId))
        {
            throw new ArgumentException("Record id is required.", nameof(recordId));
        }

        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var record = data.Records.FirstOrDefault(candidate => candidate.Id == recordId);
        if (record is not null)
        {
            record.IsFavorite = isFavorite;
            await SaveAsync(data, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        await SaveAsync(new PortableGenerationHistoryData(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PortableGenerationHistoryRecord>> GetFavoriteRecordsAsync(
        CancellationToken cancellationToken = default)
    {
        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return OrderNewestFirst(data.Records.Where(record => record.IsFavorite));
    }

    public async Task<IReadOnlyList<PortableGenerationHistoryRecord>> GetRecordsByTypeAsync(
        string type,
        CancellationToken cancellationToken = default)
    {
        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return OrderNewestFirst(data.Records.Where(record => record.Type == type));
    }

    public async Task<IReadOnlyList<PortableGenerationHistoryRecord>> GetRecordsByDateRangeAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken = default)
    {
        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return OrderNewestFirst(data.Records.Where(record =>
            record.Timestamp >= startDate && record.Timestamp <= endDate));
    }

    public async Task<IReadOnlyList<PortableGenerationHistoryRecord>> SearchRecordsAsync(
        string keyword,
        CancellationToken cancellationToken = default)
    {
        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return OrderNewestFirst(data.Records);
        }

        return OrderNewestFirst(data.Records.Where(record =>
            record.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            record.Keywords.Contains(keyword, StringComparison.OrdinalIgnoreCase)));
    }

    public async Task<PortableGenerationHistoryStatistics> GetStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var now = _clock();
        var weekStart = now.AddDays(-(int)now.DayOfWeek);
        return new PortableGenerationHistoryStatistics
        {
            TotalCount = data.Records.Count,
            FavoriteCount = data.Records.Count(record => record.IsFavorite),
            ImagePickerCount = data.Records.Count(record => record.Type == "图片取色"),
            AIGeneratedCount = data.Records.Count(record => record.Type == "AI配色"),
            TodayCount = data.Records.Count(record => record.Timestamp.Date == now.Date),
            ThisWeekCount = data.Records.Count(record => record.Timestamp >= weekStart),
            ThisMonthCount = data.Records.Count(record =>
                record.Timestamp.Month == now.Month && record.Timestamp.Year == now.Year)
        };
    }

    private static List<PortableGenerationHistoryRecord> OrderNewestFirst(
        IEnumerable<PortableGenerationHistoryRecord> records)
    {
        return records.OrderByDescending(record => record.Timestamp).ToList();
    }

    private async Task<PortableGenerationHistoryData> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new PortableGenerationHistoryData();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<PortableGenerationHistoryData>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false) ?? new PortableGenerationHistoryData();
        }
        catch (JsonException)
        {
            return new PortableGenerationHistoryData();
        }
        catch (IOException)
        {
            return new PortableGenerationHistoryData();
        }
    }

    private async Task SaveAsync(
        PortableGenerationHistoryData data,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, data, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }

    private sealed class PortableGenerationHistoryData
    {
        [JsonPropertyName("Records")] public List<PortableGenerationHistoryRecord> Records { get; set; } = new();
    }
}
