using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Security;

public sealed class PortableAccountLockoutData
{
    [JsonPropertyName("FailedAttempts")]
    public int FailedAttempts { get; set; }

    [JsonPropertyName("LockedUntil")]
    public DateTime? LockedUntil { get; set; }

    [JsonPropertyName("IsPermanentlyLocked")]
    public bool IsPermanentlyLocked { get; set; }

    [JsonPropertyName("AttemptHistory")]
    public List<PortableLoginAttemptRecord> AttemptHistory { get; set; } = [];

    public static PortableAccountLockoutData CreateDefault()
    {
        return new PortableAccountLockoutData
        {
            FailedAttempts = 0,
            LockedUntil = null,
            IsPermanentlyLocked = false,
            AttemptHistory = []
        };
    }
}

public sealed class PortableLoginAttemptRecord
{
    [JsonPropertyName("Timestamp")]
    public DateTime Timestamp { get; set; }

    [JsonPropertyName("IsSuccess")]
    public bool IsSuccess { get; set; }

    [JsonPropertyName("AttemptNumber")]
    public int AttemptNumber { get; set; }
}

public sealed class PortableAccountLockoutStatus
{
    [JsonPropertyName("isLocked")]
    public bool IsLocked { get; set; }

    [JsonPropertyName("failedAttempts")]
    public int FailedAttempts { get; set; }

    [JsonPropertyName("lockedUntil")]
    public DateTime? LockedUntil { get; set; }

    [JsonPropertyName("isPermanentlyLocked")]
    public bool IsPermanentlyLocked { get; set; }
}

public interface IPortableAccountLockoutApi
{
    Task<PortableApiResponse<PortableAccountLockoutStatus>> GetLockoutStatusAsync(
        CancellationToken cancellationToken = default);

    Task<PortableApiResponse<object>> UnlockAccountAsync(CancellationToken cancellationToken = default);
}

public sealed class FileAccountLockoutStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;

    public FileAccountLockoutStore(string path)
    {
        _path = path;
    }

    public async Task<PortableAccountLockoutData> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_path))
                return PortableAccountLockoutData.CreateDefault();

            await using var stream = File.OpenRead(_path);
            var data = await JsonSerializer.DeserializeAsync<PortableAccountLockoutData>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return data ?? PortableAccountLockoutData.CreateDefault();
        }
        catch (JsonException)
        {
            return PortableAccountLockoutData.CreateDefault();
        }
        catch (IOException)
        {
            return PortableAccountLockoutData.CreateDefault();
        }
        catch (UnauthorizedAccessException)
        {
            return PortableAccountLockoutData.CreateDefault();
        }
    }

    public async Task SaveAsync(PortableAccountLockoutData data, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = _path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, data, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, _path, overwrite: true);
    }
}

public sealed class PortableAccountLockoutController
{
    private readonly PortableAccountLockoutData _data;
    private readonly Func<DateTime> _clock;

    public PortableAccountLockoutController(PortableAccountLockoutData data, Func<DateTime>? clock = null)
    {
        _data = data;
        _clock = clock ?? (() => DateTime.Now);
    }

    public bool IsAccountLocked()
    {
        if (_data.IsPermanentlyLocked)
            return true;

        if (_data.LockedUntil == null)
            return false;

        if (_clock() < _data.LockedUntil.Value)
            return true;

        _data.LockedUntil = null;
        return false;
    }

    public string GetLockoutTimeRemaining()
    {
        if (_data.IsPermanentlyLocked)
            return "永久锁定";

        if (_data.LockedUntil.HasValue && _clock() < _data.LockedUntil.Value)
        {
            var remaining = _data.LockedUntil.Value - _clock();
            return remaining.TotalHours >= 1
                ? $"{remaining.Hours}小时{remaining.Minutes}分钟"
                : $"{remaining.Minutes}分钟{remaining.Seconds}秒";
        }

        return "未锁定";
    }

    public void RecordFailedAttempt()
    {
        _data.FailedAttempts++;
        _data.AttemptHistory.Add(new PortableLoginAttemptRecord
        {
            Timestamp = _clock(),
            IsSuccess = false,
            AttemptNumber = _data.FailedAttempts
        });

        ApplyLockoutPolicy();
        TrimHistory();
    }

    public void ResetFailedAttempts()
    {
        _data.FailedAttempts = 0;
        _data.LockedUntil = null;
        _data.IsPermanentlyLocked = false;
        _data.AttemptHistory.Add(new PortableLoginAttemptRecord
        {
            Timestamp = _clock(),
            IsSuccess = true,
            AttemptNumber = 0
        });
        TrimHistory();
    }

    public IReadOnlyList<PortableLoginAttemptRecord> GetRecentAttempts(int count = 5)
    {
        return _data.AttemptHistory
            .OrderByDescending(item => item.Timestamp)
            .Take(count)
            .ToList();
    }

    public PortableAccountLockoutStatistics BuildStatistics(int days)
    {
        var since = _clock().AddDays(-days);
        var attempts = _data.AttemptHistory.Where(item => item.Timestamp >= since).ToList();
        var failed = attempts.Count(item => !item.IsSuccess);
        var successful = attempts.Count(item => item.IsSuccess);
        return new PortableAccountLockoutStatistics(attempts.Count, failed, successful);
    }

    private void ApplyLockoutPolicy()
    {
        if (_data.FailedAttempts >= 15)
        {
            _data.IsPermanentlyLocked = true;
            _data.LockedUntil = DateTime.MaxValue;
        }
        else if (_data.FailedAttempts >= 10)
        {
            _data.LockedUntil = _clock().AddHours(1);
        }
        else if (_data.FailedAttempts >= 5)
        {
            _data.LockedUntil = _clock().AddMinutes(15);
        }
    }

    private void TrimHistory()
    {
        if (_data.AttemptHistory.Count > 20)
            _data.AttemptHistory = _data.AttemptHistory.Skip(_data.AttemptHistory.Count - 20).ToList();
    }
}

public sealed class PortableAccountLockoutServerController
{
    private readonly FileAccountLockoutStore _store;
    private readonly IPortableAccountLockoutApi _api;

    public PortableAccountLockoutServerController(
        FileAccountLockoutStore store,
        IPortableAccountLockoutApi api)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _api = api ?? throw new ArgumentNullException(nameof(api));
    }

    public async Task<bool> SyncLockoutStatusFromServerAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _api.GetLockoutStatusAsync(cancellationToken).ConfigureAwait(false);
            if (!response.Success || response.Data == null)
                return false;

            var data = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            data.FailedAttempts = response.Data.FailedAttempts;
            data.LockedUntil = response.Data.LockedUntil;
            data.IsPermanentlyLocked = response.Data.IsPermanentlyLocked;
            await _store.SaveAsync(data, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public async Task<bool> UnlockAccountAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _api.UnlockAccountAsync(cancellationToken).ConfigureAwait(false);
            var data = await _store.LoadAsync(cancellationToken).ConfigureAwait(false);
            data.FailedAttempts = 0;
            data.LockedUntil = null;
            data.IsPermanentlyLocked = false;
            await _store.SaveAsync(data, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}

public sealed record PortableAccountLockoutStatistics(
    int TotalAttempts,
    int FailedAttempts,
    int SuccessfulAttempts);
