using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Security;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PortableBindingPlatform
{
    WeChat,
    QQ,
    GitHub,
    Google,
    Microsoft,
    Baidu,
    Weibo,
    Twitter
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PortableBindingSyncStatus
{
    None,
    Syncing,
    Synced,
    Failed,
    Outdated
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PortableBindingAction
{
    Bind,
    Unbind,
    Update,
    Sync,
    PermissionChange
}

public sealed class PortableBindingsData
{
    [JsonPropertyName("Bindings")] public List<PortableThirdPartyBinding> Bindings { get; set; } = new();
    [JsonPropertyName("History")] public List<PortableBindingHistoryRecord> History { get; set; } = new();
}

public sealed class PortableThirdPartyBinding
{
    [JsonPropertyName("Platform")] public PortableBindingPlatform Platform { get; set; }
    [JsonPropertyName("AccountId")] public string AccountId { get; set; } = string.Empty;
    [JsonPropertyName("Nickname")] public string Nickname { get; set; } = string.Empty;
    [JsonPropertyName("AvatarUrl")] public string AvatarUrl { get; set; } = string.Empty;
    [JsonPropertyName("Email")] public string Email { get; set; } = string.Empty;
    [JsonPropertyName("BindTime")] public DateTime BindTime { get; set; }
    [JsonPropertyName("LastSyncTime")] public DateTime? LastSyncTime { get; set; }
    [JsonPropertyName("LastUseTime")] public DateTime? LastUseTime { get; set; }
    [JsonPropertyName("IsActive")] public bool IsActive { get; set; }
    [JsonPropertyName("SyncStatus")] public PortableBindingSyncStatus SyncStatus { get; set; } = PortableBindingSyncStatus.None;
    [JsonPropertyName("Permissions")] public List<string> Permissions { get; set; } = new();
    [JsonPropertyName("ExtendedInfo")] public Dictionary<string, string> ExtendedInfo { get; set; } = new();
}

public sealed class PortableBindingHistoryRecord
{
    [JsonPropertyName("Timestamp")] public DateTime Timestamp { get; set; }
    [JsonPropertyName("Platform")] public PortableBindingPlatform Platform { get; set; }
    [JsonPropertyName("Action")] public PortableBindingAction Action { get; set; }
    [JsonPropertyName("AccountId")] public string AccountId { get; set; } = string.Empty;
    [JsonPropertyName("Nickname")] public string Nickname { get; set; } = string.Empty;
    [JsonPropertyName("Details")] public string Details { get; set; } = string.Empty;
}

public sealed class PortableAccountBindingStore
{
    private const int MaxHistoryRecords = 100;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _filePath;
    private readonly Func<DateTime> _now;
    private PortableBindingsData _data;

    public PortableAccountBindingStore(string filePath, Func<DateTime> now)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Bindings file path cannot be empty.", nameof(filePath))
            : filePath;
        _now = now ?? throw new ArgumentNullException(nameof(now));
        _data = Load();
    }

    public List<PortableThirdPartyBinding> GetAllBindings()
    {
        return _data.Bindings.Select(CloneBinding).ToList();
    }

    public PortableThirdPartyBinding? GetBinding(PortableBindingPlatform platform)
    {
        var binding = _data.Bindings.FirstOrDefault(item => item.Platform == platform && item.IsActive);
        return binding == null ? null : CloneBinding(binding);
    }

    public bool IsBound(PortableBindingPlatform platform)
    {
        return _data.Bindings.Any(item => item.Platform == platform && item.IsActive);
    }

    public void SaveFromServer(PortableBindingsResult result)
    {
        _data.Bindings = result.Bindings
            .Select(binding => new PortableThirdPartyBinding
            {
                Platform = ParsePlatform(binding.Platform),
                AccountId = binding.PlatformUserId,
                Nickname = binding.DisplayName ?? string.Empty,
                BindTime = binding.BoundTime,
                IsActive = true,
                SyncStatus = PortableBindingSyncStatus.Synced
            })
            .ToList();
        Save();
    }

    public bool BindAccount(
        PortableBindingPlatform platform,
        string accountId,
        string nickname,
        string email = "",
        string avatarUrl = "",
        IEnumerable<string>? permissions = null)
    {
        var bindingTimestamp = _now();
        var existing = _data.Bindings.FirstOrDefault(binding => binding.Platform == platform);
        var isUpdate = existing != null;
        var permissionList = permissions?.ToList() ?? new List<string> { "basic_info", "profile" };

        if (existing == null)
        {
            _data.Bindings.Add(new PortableThirdPartyBinding
            {
                Platform = platform,
                AccountId = accountId,
                Nickname = nickname,
                Email = email,
                AvatarUrl = avatarUrl,
                BindTime = bindingTimestamp,
                LastUseTime = bindingTimestamp,
                IsActive = true,
                SyncStatus = PortableBindingSyncStatus.Synced,
                Permissions = permissionList
            });
        }
        else
        {
            existing.AccountId = accountId;
            existing.Nickname = nickname;
            existing.Email = email;
            existing.AvatarUrl = avatarUrl;
            existing.BindTime = bindingTimestamp;
            existing.LastUseTime = bindingTimestamp;
            existing.IsActive = true;
            existing.Permissions = permissionList;
        }

        AddHistoryRecord(
            _now(),
            platform,
            isUpdate ? PortableBindingAction.Update : PortableBindingAction.Bind,
            accountId,
            nickname,
            isUpdate ? "更新账号信息" : "首次绑定账号");
        Save();
        return true;
    }

    public bool UnbindAccount(PortableBindingPlatform platform)
    {
        var binding = _data.Bindings.FirstOrDefault(item => item.Platform == platform);
        if (binding == null)
        {
            return false;
        }

        AddHistoryRecord(_now(), platform, PortableBindingAction.Unbind, binding.AccountId, binding.Nickname, "用户主动解绑");
        binding.IsActive = false;
        Save();
        return true;
    }

    public bool UpdateSyncStatus(PortableBindingPlatform platform, PortableBindingSyncStatus status)
    {
        var binding = _data.Bindings.FirstOrDefault(item => item.Platform == platform && item.IsActive);
        if (binding == null)
        {
            return false;
        }

        binding.SyncStatus = status;
        binding.LastSyncTime = _now();
        Save();
        return true;
    }

    public bool UpdatePermissions(PortableBindingPlatform platform, IEnumerable<string> permissions)
    {
        var binding = _data.Bindings.FirstOrDefault(item => item.Platform == platform && item.IsActive);
        if (binding == null)
        {
            return false;
        }

        var permissionList = permissions.ToList();
        binding.Permissions = permissionList;
        AddHistoryRecord(
            _now(),
            platform,
            PortableBindingAction.PermissionChange,
            binding.AccountId,
            binding.Nickname,
            $"权限更新: {string.Join(", ", permissionList)}");
        Save();
        return true;
    }

    public List<PortableBindingHistoryRecord> GetHistory(PortableBindingPlatform? platform = null, int limit = 50)
    {
        var query = _data.History.AsEnumerable();
        if (platform.HasValue)
        {
            query = query.Where(record => record.Platform == platform.Value);
        }

        return query
            .Select((record, index) => new { Record = record, Index = index })
            .OrderByDescending(item => item.Record.Timestamp)
            .ThenByDescending(item => item.Index)
            .Take(Math.Max(0, limit))
            .Select(item => CloneHistoryRecord(item.Record))
            .ToList();
    }

    public void RecordUsage(PortableBindingPlatform platform)
    {
        var binding = _data.Bindings.FirstOrDefault(item => item.Platform == platform && item.IsActive);
        if (binding == null)
        {
            return;
        }

        binding.LastUseTime = _now();
        Save();
    }

    private void AddHistoryRecord(
        DateTime timestamp,
        PortableBindingPlatform platform,
        PortableBindingAction action,
        string accountId,
        string nickname,
        string details)
    {
        _data.History.Add(new PortableBindingHistoryRecord
        {
            Timestamp = timestamp,
            Platform = platform,
            Action = action,
            AccountId = accountId,
            Nickname = nickname,
            Details = details
        });

        if (_data.History.Count > MaxHistoryRecords)
        {
            _data.History = _data.History
                .Select((record, index) => new { Record = record, Index = index })
                .OrderByDescending(item => item.Record.Timestamp)
                .ThenByDescending(item => item.Index)
                .Take(MaxHistoryRecords)
                .Select(item => item.Record)
                .ToList();
        }
    }

    private PortableBindingsData Load()
    {
        if (!File.Exists(_filePath))
        {
            return new PortableBindingsData();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<PortableBindingsData>(json, JsonOptions) ?? new PortableBindingsData();
        }
        catch (JsonException)
        {
            return new PortableBindingsData();
        }
        catch (IOException)
        {
            return new PortableBindingsData();
        }
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(_data, JsonOptions));
        File.Move(tempPath, _filePath, overwrite: true);
    }

    private static PortableBindingPlatform ParsePlatform(string platform)
    {
        return platform.Trim().ToLowerInvariant() switch
        {
            "wechat" => PortableBindingPlatform.WeChat,
            "qq" => PortableBindingPlatform.QQ,
            "github" => PortableBindingPlatform.GitHub,
            "google" => PortableBindingPlatform.Google,
            "microsoft" => PortableBindingPlatform.Microsoft,
            "baidu" => PortableBindingPlatform.Baidu,
            "weibo" => PortableBindingPlatform.Weibo,
            "twitter" => PortableBindingPlatform.Twitter,
            _ => PortableBindingPlatform.WeChat
        };
    }

    private static PortableThirdPartyBinding CloneBinding(PortableThirdPartyBinding binding)
    {
        return new PortableThirdPartyBinding
        {
            Platform = binding.Platform,
            AccountId = binding.AccountId,
            Nickname = binding.Nickname,
            AvatarUrl = binding.AvatarUrl,
            Email = binding.Email,
            BindTime = binding.BindTime,
            LastSyncTime = binding.LastSyncTime,
            LastUseTime = binding.LastUseTime,
            IsActive = binding.IsActive,
            SyncStatus = binding.SyncStatus,
            Permissions = binding.Permissions.ToList(),
            ExtendedInfo = new Dictionary<string, string>(binding.ExtendedInfo)
        };
    }

    private static PortableBindingHistoryRecord CloneHistoryRecord(PortableBindingHistoryRecord record)
    {
        return new PortableBindingHistoryRecord
        {
            Timestamp = record.Timestamp,
            Platform = record.Platform,
            Action = record.Action,
            AccountId = record.AccountId,
            Nickname = record.Nickname,
            Details = record.Details
        };
    }
}
