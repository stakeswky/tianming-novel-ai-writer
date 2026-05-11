using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Security;

public sealed class PortableAppLockConfig
{
    [JsonPropertyName("ConfigVersion")]
    public int ConfigVersion { get; set; } = 1;

    [JsonPropertyName("EnablePasswordLock")]
    public bool EnablePasswordLock { get; set; }

    [JsonPropertyName("LockOnStartup")]
    public bool LockOnStartup { get; set; }

    [JsonPropertyName("LockOnSwitch")]
    public bool LockOnSwitch { get; set; }

    [JsonPropertyName("EnableAutoLock")]
    public bool EnableAutoLock { get; set; }

    [JsonPropertyName("AutoLockMinutes")]
    public int AutoLockMinutes { get; set; } = 5;

    [JsonPropertyName("LastActivityTime")]
    public DateTime? LastActivityTime { get; set; }

    [JsonPropertyName("FailedAttempts")]
    public int FailedAttempts { get; set; }

    [JsonPropertyName("LockoutUntil")]
    public DateTime? LockoutUntil { get; set; }

    [JsonPropertyName("EmergencyCode")]
    public string? EmergencyCode { get; set; }

    [JsonPropertyName("EmergencyCodeHash")]
    public string? EmergencyCodeHash { get; set; }

    public static PortableAppLockConfig CreateDefault()
    {
        return new PortableAppLockConfig
        {
            ConfigVersion = 1,
            EnablePasswordLock = false,
            LockOnStartup = false,
            LockOnSwitch = false,
            EnableAutoLock = false,
            AutoLockMinutes = 5,
            LastActivityTime = null,
            FailedAttempts = 0,
            LockoutUntil = null,
            EmergencyCode = null,
            EmergencyCodeHash = null
        };
    }
}

public sealed class FileAppLockConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;

    public FileAppLockConfigStore(string path)
    {
        _path = path;
    }

    public async Task<PortableAppLockConfig> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_path))
                return PortableAppLockConfig.CreateDefault();

            await using var stream = File.OpenRead(_path);
            var config = await JsonSerializer.DeserializeAsync<PortableAppLockConfig>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return config ?? PortableAppLockConfig.CreateDefault();
        }
        catch (JsonException)
        {
            return PortableAppLockConfig.CreateDefault();
        }
        catch (IOException)
        {
            return PortableAppLockConfig.CreateDefault();
        }
        catch (UnauthorizedAccessException)
        {
            return PortableAppLockConfig.CreateDefault();
        }
    }

    public async Task SaveAsync(PortableAppLockConfig config, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        var tempPath = _path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, config, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, _path, overwrite: true);
    }
}

public sealed class PortableAppLockPolicy
{
    private readonly Func<DateTime> _clock;
    private readonly Func<bool> _hasPassword;

    public PortableAppLockPolicy(Func<DateTime>? clock = null, Func<bool>? hasPassword = null)
    {
        _clock = clock ?? (() => DateTime.Now);
        _hasPassword = hasPassword ?? (() => false);
    }

    public bool ShouldLockOnStartup(PortableAppLockConfig config)
    {
        return config.EnablePasswordLock && config.LockOnStartup && _hasPassword();
    }

    public bool ShouldLockOnSwitch(PortableAppLockConfig config)
    {
        return config.EnablePasswordLock && config.LockOnSwitch && _hasPassword();
    }

    public bool ShouldAutoLock(PortableAppLockConfig config)
    {
        if (!config.EnableAutoLock || !config.EnablePasswordLock || !_hasPassword())
            return false;

        if (config.LastActivityTime == null)
            return false;

        return _clock() - config.LastActivityTime.Value >= TimeSpan.FromMinutes(config.AutoLockMinutes);
    }

    public TimeSpan GetTimeUntilAutoLock(PortableAppLockConfig config)
    {
        if (config.LastActivityTime == null)
            return TimeSpan.FromMinutes(config.AutoLockMinutes);

        var remaining = TimeSpan.FromMinutes(config.AutoLockMinutes) - (_clock() - config.LastActivityTime.Value);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }
}

public sealed class PortableAppLockController
{
    private readonly PortableAppLockConfig _config;
    private readonly Func<DateTime> _clock;

    public PortableAppLockController(PortableAppLockConfig config, Func<DateTime>? clock = null)
    {
        _config = config;
        _clock = clock ?? (() => DateTime.Now);
    }

    public bool IsLocked { get; private set; }

    public void LockApp(string? reason = null)
    {
        IsLocked = true;
    }

    public void UnlockApp()
    {
        IsLocked = false;
        UpdateLastActivity();
        ResetFailedAttempts();
    }

    public void UpdateLastActivity()
    {
        _config.LastActivityTime = _clock();
    }

    public void IncrementFailedAttempts()
    {
        _config.FailedAttempts++;
        var lockout = GetLockoutDuration(_config.FailedAttempts);
        if (lockout > TimeSpan.Zero)
            _config.LockoutUntil = _clock().Add(lockout);
    }

    public void ResetFailedAttempts()
    {
        _config.FailedAttempts = 0;
        _config.LockoutUntil = null;
    }

    public bool IsLockedOut()
    {
        if (_config.LockoutUntil == null)
            return false;

        if (_clock() >= _config.LockoutUntil.Value)
        {
            _config.LockoutUntil = null;
            return false;
        }

        return true;
    }

    public TimeSpan GetLockoutRemaining()
    {
        if (_config.LockoutUntil == null || _clock() >= _config.LockoutUntil.Value)
            return TimeSpan.Zero;

        return _config.LockoutUntil.Value - _clock();
    }

    public void SetEmergencyCode(string code)
    {
        _config.EmergencyCode = null;
        _config.EmergencyCodeHash = HashCode(code);
    }

    public bool VerifyEmergencyCode(string code)
    {
        if (string.IsNullOrEmpty(_config.EmergencyCodeHash))
            return false;

        if (!FixedTimeEquals(HashCode(code), _config.EmergencyCodeHash))
            return false;

        _config.EmergencyCodeHash = null;
        return true;
    }

    public bool HasEmergencyCode()
    {
        return !string.IsNullOrEmpty(_config.EmergencyCodeHash);
    }

    private static TimeSpan GetLockoutDuration(int failedAttempts)
    {
        if (failedAttempts >= 10)
            return TimeSpan.FromMinutes(30);

        if (failedAttempts >= 5)
            return TimeSpan.FromMinutes(5);

        if (failedAttempts >= 3)
            return TimeSpan.FromSeconds(30);

        return TimeSpan.Zero;
    }

    private static string HashCode(string code)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(code));
        return Convert.ToBase64String(hash);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }
}
