using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Security;

public interface IPortableAuthTokenProtector
{
    string Protect(byte[] data);
    byte[] Unprotect(string payload);
}

public sealed class PortableAuthTokenData
{
    [JsonPropertyName("AccessToken")] public string? AccessToken { get; set; }
    [JsonPropertyName("RefreshToken")] public string? RefreshToken { get; set; }
    [JsonPropertyName("SessionKey")] public string? SessionKey { get; set; }
    [JsonPropertyName("ExpiresAt")] public DateTime ExpiresAt { get; set; }
    [JsonPropertyName("UserId")] public string? UserId { get; set; }
    [JsonPropertyName("Username")] public string? Username { get; set; }
    [JsonPropertyName("ClientId")] public string? ClientId { get; set; }
    [JsonPropertyName("LastLoginTime")] public DateTime LastLoginTime { get; set; }
}

public sealed class PortableRefreshTokenResult
{
    [JsonPropertyName("accessToken")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("refreshToken")] public string RefreshToken { get; set; } = string.Empty;
    [JsonPropertyName("sessionKey")] public string SessionKey { get; set; } = string.Empty;
    [JsonPropertyName("expiresAt")] public DateTime ExpiresAt { get; set; }
}

public sealed class PortableSignatureHeaders
{
    [JsonPropertyName("ClientId")] public string ClientId { get; set; } = string.Empty;
    [JsonPropertyName("Timestamp")] public string Timestamp { get; set; } = string.Empty;
    [JsonPropertyName("Nonce")] public string Nonce { get; set; } = string.Empty;
    [JsonPropertyName("Signature")] public string Signature { get; set; } = string.Empty;
}

public sealed class PortableAuthTokenStore
{
    private readonly string _filePath;
    private readonly IPortableAuthTokenProtector _protector;
    private readonly Func<DateTime> _utcNow;
    private readonly Func<string> _clientIdFactory;
    private readonly Func<string> _nonceFactory;
    private readonly object _lock = new();
    private PortableAuthTokenData _data;

    public PortableAuthTokenStore(
        string filePath,
        IPortableAuthTokenProtector protector,
        Func<DateTime> utcNow,
        Func<string> clientIdFactory,
        Func<string> nonceFactory)
    {
        _filePath = filePath;
        _protector = protector;
        _utcNow = utcNow;
        _clientIdFactory = clientIdFactory;
        _nonceFactory = nonceFactory;
        _data = LoadOrCreate();
    }

    public bool IsLoggedIn
    {
        get
        {
            lock (_lock)
            {
                return !string.IsNullOrWhiteSpace(_data.AccessToken) && _data.ExpiresAt > _utcNow();
            }
        }
    }

    public bool IsAccessTokenExpired
    {
        get
        {
            lock (_lock)
            {
                return string.IsNullOrWhiteSpace(_data.AccessToken) || _data.ExpiresAt <= _utcNow();
            }
        }
    }

    public bool HasRefreshToken
    {
        get
        {
            lock (_lock)
            {
                return !string.IsNullOrWhiteSpace(_data.RefreshToken);
            }
        }
    }

    public string? AccessToken
    {
        get
        {
            lock (_lock)
            {
                return _data.AccessToken;
            }
        }
    }

    public string? RefreshToken
    {
        get
        {
            lock (_lock)
            {
                return _data.RefreshToken;
            }
        }
    }

    public string? SessionKey
    {
        get
        {
            lock (_lock)
            {
                return _data.SessionKey;
            }
        }
    }

    public string? UserId
    {
        get
        {
            lock (_lock)
            {
                return _data.UserId;
            }
        }
    }

    public string? Username
    {
        get
        {
            lock (_lock)
            {
                return _data.Username;
            }
        }
    }

    public string ClientId
    {
        get
        {
            lock (_lock)
            {
                EnsureClientId();
                return _data.ClientId!;
            }
        }
    }

    public void SaveTokens(PortableLoginResult loginResult)
    {
        lock (_lock)
        {
            EnsureClientId();
            _data.AccessToken = loginResult.AccessToken;
            _data.RefreshToken = loginResult.RefreshToken;
            _data.SessionKey = loginResult.SessionKey;
            _data.ExpiresAt = loginResult.ExpiresAt;
            _data.UserId = loginResult.User.UserId;
            _data.Username = loginResult.User.Username;
            _data.LastLoginTime = _utcNow().ToLocalTime();
            Save();
        }
    }

    public void SaveTokens(PortableRegisterResult registerResult)
    {
        lock (_lock)
        {
            EnsureClientId();
            _data.AccessToken = registerResult.AccessToken;
            _data.RefreshToken = registerResult.RefreshToken;
            _data.SessionKey = registerResult.SessionKey;
            _data.ExpiresAt = registerResult.ExpiresAt;
            _data.UserId = registerResult.UserId;
            _data.Username = registerResult.Username;
            _data.LastLoginTime = _utcNow().ToLocalTime();
            Save();
        }
    }

    public void UpdateTokens(PortableRefreshTokenResult refreshResult)
    {
        lock (_lock)
        {
            EnsureClientId();
            _data.AccessToken = refreshResult.AccessToken;
            _data.RefreshToken = refreshResult.RefreshToken;
            _data.SessionKey = refreshResult.SessionKey;
            _data.ExpiresAt = refreshResult.ExpiresAt;
            Save();
        }
    }

    public void ClearTokens()
    {
        lock (_lock)
        {
            var clientId = ClientId;
            _data = new PortableAuthTokenData { ClientId = clientId };
            Save();
        }
    }

    public PortableSignatureHeaders GenerateSignatureHeaders(string method, string path, string? body = null)
    {
        lock (_lock)
        {
            var timestamp = new DateTimeOffset(_utcNow()).ToUnixTimeSeconds().ToString();
            var nonce = _nonceFactory();
            var signContent = $"{method.ToUpperInvariant()}{path}{timestamp}{nonce}{body ?? string.Empty}";
            return new PortableSignatureHeaders
            {
                ClientId = ClientId,
                Timestamp = timestamp,
                Nonce = nonce,
                Signature = ComputeHmacSha256(signContent, _data.SessionKey ?? string.Empty)
            };
        }
    }

    private PortableAuthTokenData LoadOrCreate()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var fileContent = File.ReadAllText(_filePath);
                var loaded = ReadPayload(fileContent);
                if (loaded != null)
                {
                    if (string.IsNullOrWhiteSpace(loaded.ClientId))
                    {
                        loaded.ClientId = _clientIdFactory();
                    }

                    SaveData(loaded);
                    return loaded;
                }
            }
        }
        catch
        {
        }

        var created = new PortableAuthTokenData { ClientId = _clientIdFactory() };
        SaveData(created);
        return created;
    }

    private PortableAuthTokenData? ReadPayload(string fileContent)
    {
        if (fileContent.TrimStart().StartsWith("{", StringComparison.Ordinal))
        {
            return JsonSerializer.Deserialize<PortableAuthTokenData>(fileContent);
        }

        try
        {
            var json = Encoding.UTF8.GetString(_protector.Unprotect(fileContent));
            return JsonSerializer.Deserialize<PortableAuthTokenData>(json);
        }
        catch
        {
            return null;
        }
    }

    private void EnsureClientId()
    {
        if (string.IsNullOrWhiteSpace(_data.ClientId))
        {
            _data.ClientId = _clientIdFactory();
            Save();
        }
    }

    private void Save()
    {
        SaveData(_data);
    }

    private void SaveData(PortableAuthTokenData data)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.SerializeToUtf8Bytes(data);
        var payload = _protector.Protect(json);
        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, payload);
        File.Move(tmp, _filePath, overwrite: true);
    }

    private static string ComputeHmacSha256(string content, string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(content)));
    }
}
