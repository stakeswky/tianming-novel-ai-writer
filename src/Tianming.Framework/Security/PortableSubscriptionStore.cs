using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Security;

public sealed class PortableSubscriptionSnapshot
{
    [JsonPropertyName("SubscriptionId")] public int? SubscriptionId { get; set; }
    [JsonPropertyName("UserId")] public string UserId { get; set; } = string.Empty;
    [JsonPropertyName("PlanType")] public string? PlanType { get; set; } = "free";
    [JsonPropertyName("StartTime")] public DateTime? StartTime { get; set; }
    [JsonPropertyName("EndTime")] public DateTime? EndTime { get; set; }
    [JsonPropertyName("IsActive")] public bool IsActive { get; set; }
    [JsonPropertyName("Source")] public string? Source { get; set; } = string.Empty;
}

public sealed class PortableSubscriptionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _filePath;
    private readonly Func<DateTime> _now;
    private readonly object _lock = new();
    private PortableSubscriptionSnapshot? _cachedData;

    public PortableSubscriptionStore(string filePath, Func<DateTime> now)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Subscription cache path cannot be empty.", nameof(filePath))
            : filePath;
        _now = now ?? throw new ArgumentNullException(nameof(now));
        _cachedData = Load();
    }

    public bool IsActive
    {
        get
        {
            lock (_lock)
            {
                return _cachedData?.IsActive ?? false;
            }
        }
    }

    public string PlanType
    {
        get
        {
            lock (_lock)
            {
                return string.IsNullOrWhiteSpace(_cachedData?.PlanType) ? "free" : _cachedData.PlanType!;
            }
        }
    }

    public int RemainingDays
    {
        get
        {
            lock (_lock)
            {
                if (_cachedData?.EndTime == null || _cachedData.EndTime <= _now())
                {
                    return 0;
                }

                return (int)Math.Ceiling((_cachedData.EndTime.Value - _now()).TotalDays);
            }
        }
    }

    public DateTime? EndTime
    {
        get
        {
            lock (_lock)
            {
                return _cachedData?.EndTime;
            }
        }
    }

    public PortableSubscriptionSnapshot? GetSubscriptionInfo()
    {
        lock (_lock)
        {
            return Clone(_cachedData);
        }
    }

    public void SaveFromServer(PortableSubscriptionInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        lock (_lock)
        {
            _cachedData = new PortableSubscriptionSnapshot
            {
                SubscriptionId = info.SubscriptionId,
                UserId = info.UserId,
                PlanType = info.PlanType,
                StartTime = info.StartTime,
                EndTime = info.EndTime,
                IsActive = info.IsActive,
                Source = info.Source
            };
            Save();
        }
    }

    public void SaveActivationResult(PortableActivationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        lock (_lock)
        {
            _cachedData = new PortableSubscriptionSnapshot
            {
                SubscriptionId = result.Subscription.SubscriptionId,
                UserId = result.Subscription.UserId,
                PlanType = result.Subscription.PlanType,
                StartTime = result.Subscription.StartTime,
                EndTime = result.NewExpireTime,
                IsActive = result.Subscription.IsActive,
                Source = "card_key"
            };
            Save();
        }
    }

    public void ClearCache()
    {
        lock (_lock)
        {
            _cachedData = null;
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
    }

    private PortableSubscriptionSnapshot? Load()
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<PortableSubscriptionSnapshot>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(_cachedData, JsonOptions);
        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, json);
        File.Move(tmp, _filePath, overwrite: true);
    }

    private static PortableSubscriptionSnapshot? Clone(PortableSubscriptionSnapshot? data)
    {
        if (data == null)
        {
            return null;
        }

        return new PortableSubscriptionSnapshot
        {
            SubscriptionId = data.SubscriptionId,
            UserId = data.UserId,
            PlanType = data.PlanType,
            StartTime = data.StartTime,
            EndTime = data.EndTime,
            IsActive = data.IsActive,
            Source = data.Source
        };
    }
}
