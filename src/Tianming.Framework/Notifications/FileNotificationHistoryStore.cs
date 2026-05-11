using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Framework.Notifications;

public sealed class NotificationHistoryData
{
    [JsonPropertyName("Records")] public List<NotificationRecordData> Records { get; set; } = new();
    [JsonPropertyName("MaxRecords")] public int MaxRecords { get; set; } = 10;
}

public sealed class NotificationRecordData
{
    [JsonPropertyName("Id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("Title")] public string Title { get; set; } = string.Empty;
    [JsonPropertyName("Content")] public string Content { get; set; } = string.Empty;
    [JsonPropertyName("Time")] public DateTime Time { get; set; }
    [JsonPropertyName("Type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("IsRead")] public bool IsRead { get; set; }
    [JsonPropertyName("WasBlocked")] public bool WasBlocked { get; set; }
}

public sealed class FileNotificationHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly Func<DateTime> _clock;
    private readonly Func<string> _idFactory;

    public FileNotificationHistoryStore(
        string filePath,
        Func<DateTime>? clock = null,
        Func<string>? idFactory = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Notification history file path is required.", nameof(filePath))
            : filePath;
        _clock = clock ?? (() => DateTime.Now);
        _idFactory = idFactory ?? (() => "D" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant());
    }

    public async Task<NotificationRecordData> AddRecordAsync(
        string title,
        string content,
        string type,
        bool wasBlocked = false,
        CancellationToken cancellationToken = default)
    {
        var data = await LoadDataAsync(cancellationToken).ConfigureAwait(false);
        var maxRecords = NormalizeMaxRecords(data);
        var record = new NotificationRecordData
        {
            Id = _idFactory(),
            Title = title,
            Content = content,
            Time = _clock(),
            Type = type,
            IsRead = false,
            WasBlocked = wasBlocked
        };

        data.Records.Insert(0, record);
        data.Records = data.Records.Take(maxRecords).ToList();
        await SaveDataAsync(data, cancellationToken).ConfigureAwait(false);

        return record;
    }

    public async Task<IReadOnlyList<NotificationRecordData>> GetRecordsAsync(CancellationToken cancellationToken = default)
    {
        var data = await LoadDataAsync(cancellationToken).ConfigureAwait(false);
        var maxRecords = NormalizeMaxRecords(data);
        if (data.Records.Count > maxRecords)
        {
            data.Records = data.Records.Take(maxRecords).ToList();
            await SaveDataAsync(data, cancellationToken).ConfigureAwait(false);
        }

        return data.Records;
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        var data = await LoadDataAsync(cancellationToken).ConfigureAwait(false);
        data.Records.Clear();
        await SaveDataAsync(data, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeleteRecordAsync(string id, CancellationToken cancellationToken = default)
    {
        var data = await LoadDataAsync(cancellationToken).ConfigureAwait(false);
        var removed = data.Records.RemoveAll(r => r.Id == id) > 0;
        if (removed)
        {
            await SaveDataAsync(data, cancellationToken).ConfigureAwait(false);
        }

        return removed;
    }

    public async Task<bool> MarkAsReadAsync(string id, CancellationToken cancellationToken = default)
    {
        var data = await LoadDataAsync(cancellationToken).ConfigureAwait(false);
        var record = data.Records.FirstOrDefault(r => r.Id == id);
        if (record == null)
        {
            return false;
        }

        record.IsRead = true;
        await SaveDataAsync(data, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private async Task<NotificationHistoryData> LoadDataAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new NotificationHistoryData();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<NotificationHistoryData>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false) ?? new NotificationHistoryData();
        }
        catch (JsonException)
        {
            return new NotificationHistoryData();
        }
        catch (IOException)
        {
            return new NotificationHistoryData();
        }
    }

    private async Task SaveDataAsync(NotificationHistoryData data, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, data, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }

    private static int NormalizeMaxRecords(NotificationHistoryData data)
    {
        if (data.MaxRecords <= 0)
        {
            data.MaxRecords = 10;
        }

        return data.MaxRecords;
    }
}

public sealed class PortableNotificationHistoryRecordView
{
    public string Id { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Content { get; init; } = string.Empty;

    public DateTime Time { get; init; }

    public string Type { get; init; } = string.Empty;

    public bool IsRead { get; init; }

    public bool WasBlocked { get; init; }

    public string TimeDisplay => Time.ToString("yyyy-MM-dd HH:mm");
}

public sealed class PortableNotificationHistorySnapshot
{
    public IReadOnlyList<PortableNotificationHistoryRecordView> Records { get; init; } =
        Array.Empty<PortableNotificationHistoryRecordView>();

    public int TotalCount { get; init; }

    public int UnreadCount { get; init; }
}

public sealed record PortableNotificationHistoryCommandResult(bool Changed, string Message);

public sealed class PortableNotificationHistoryController
{
    private readonly FileNotificationHistoryStore _store;
    private PortableNotificationHistorySnapshot _snapshot = new();

    public PortableNotificationHistoryController(FileNotificationHistoryStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public Task<PortableNotificationHistorySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_snapshot);
    }

    public async Task<PortableNotificationHistorySnapshot> LoadAsync(CancellationToken cancellationToken = default)
    {
        var records = await _store.GetRecordsAsync(cancellationToken).ConfigureAwait(false);
        _snapshot = CreateSnapshot(records);
        return _snapshot;
    }

    public async Task<bool> MarkAsReadAsync(string id, CancellationToken cancellationToken = default)
    {
        var changed = await _store.MarkAsReadAsync(id, cancellationToken).ConfigureAwait(false);
        if (changed)
        {
            await LoadAsync(cancellationToken).ConfigureAwait(false);
        }

        return changed;
    }

    public async Task<PortableNotificationHistoryCommandResult> DeleteRecordAsync(
        string id,
        CancellationToken cancellationToken = default)
    {
        var changed = await _store.DeleteRecordAsync(id, cancellationToken).ConfigureAwait(false);
        if (changed)
        {
            await LoadAsync(cancellationToken).ConfigureAwait(false);
            return new PortableNotificationHistoryCommandResult(true, "已删除该条通知记录");
        }

        return new PortableNotificationHistoryCommandResult(false, string.Empty);
    }

    public async Task<PortableNotificationHistoryCommandResult> ClearAllAsync(
        bool confirm,
        CancellationToken cancellationToken = default)
    {
        if (!confirm)
        {
            return new PortableNotificationHistoryCommandResult(false, string.Empty);
        }

        await _store.ClearAllAsync(cancellationToken).ConfigureAwait(false);
        await LoadAsync(cancellationToken).ConfigureAwait(false);
        return new PortableNotificationHistoryCommandResult(true, "已清空所有通知历史");
    }

    private static PortableNotificationHistorySnapshot CreateSnapshot(
        IReadOnlyList<NotificationRecordData> records)
    {
        var views = records
            .Select(record => new PortableNotificationHistoryRecordView
            {
                Id = record.Id,
                Title = record.Title,
                Content = record.Content,
                Time = record.Time,
                Type = record.Type,
                IsRead = record.IsRead,
                WasBlocked = record.WasBlocked
            })
            .ToList();

        return new PortableNotificationHistorySnapshot
        {
            Records = views,
            TotalCount = views.Count,
            UnreadCount = views.Count(record => !record.IsRead)
        };
    }
}
