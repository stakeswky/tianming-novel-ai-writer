using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Security;

public sealed class PortableOAuthProviderConfigData
{
    [JsonPropertyName("Providers")]
    public Dictionary<string, PortableOAuthProviderConfig> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PortableOAuthProviderConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly object _lock = new();
    private Dictionary<string, PortableOAuthProviderConfig> _providers;

    public PortableOAuthProviderConfigStore(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("OAuth provider config path cannot be empty.", nameof(filePath))
            : filePath;
        _providers = Load();
    }

    public IReadOnlyDictionary<string, PortableOAuthProviderConfig> GetProviders()
    {
        lock (_lock)
        {
            return CloneProviders(_providers);
        }
    }

    public PortableOAuthProviderConfig? GetProvider(string platform)
    {
        lock (_lock)
        {
            return TryGetKnownProvider(platform, out var normalizedPlatform)
                ? CloneProvider(_providers[normalizedPlatform])
                : null;
        }
    }

    public bool ConfigurePlatform(string platform, string clientId)
    {
        lock (_lock)
        {
            if (!TryGetKnownProvider(platform, out var normalizedPlatform))
            {
                return false;
            }

            _providers[normalizedPlatform].ClientId = clientId ?? string.Empty;
            Save();
            return true;
        }
    }

    public bool UpdateProviderConfig(string platform, PortableOAuthProviderConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        lock (_lock)
        {
            if (!TryGetKnownProvider(platform, out var normalizedPlatform))
            {
                return false;
            }

            _providers[normalizedPlatform] = CloneProvider(config);
            Save();
            return true;
        }
    }

    public bool IsPlatformConfigured(string platform)
    {
        lock (_lock)
        {
            return TryGetKnownProvider(platform, out var normalizedPlatform)
                && !string.IsNullOrWhiteSpace(_providers[normalizedPlatform].ClientId);
        }
    }

    private Dictionary<string, PortableOAuthProviderConfig> Load()
    {
        var defaults = PortableOAuthAuthorizationCore.CreateDefaultConfigs();
        if (!File.Exists(_filePath))
        {
            return CloneProviders(defaults);
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var data = JsonSerializer.Deserialize<PortableOAuthProviderConfigData>(json, JsonOptions);
            if (data?.Providers == null)
            {
                return CloneProviders(defaults);
            }

            var merged = CloneProviders(defaults);
            foreach (var (platform, config) in data.Providers)
            {
                var normalizedPlatform = NormalizePlatform(platform);
                if (!defaults.ContainsKey(normalizedPlatform))
                {
                    continue;
                }

                merged[normalizedPlatform] = new PortableOAuthProviderConfig
                {
                    AuthUrl = string.IsNullOrWhiteSpace(config.AuthUrl) ? defaults[normalizedPlatform].AuthUrl : config.AuthUrl,
                    ClientId = config.ClientId ?? string.Empty,
                    Scope = string.IsNullOrWhiteSpace(config.Scope) ? defaults[normalizedPlatform].Scope : config.Scope
                };
            }

            return merged;
        }
        catch (JsonException)
        {
            return CloneProviders(defaults);
        }
        catch (IOException)
        {
            return CloneProviders(defaults);
        }
    }

    private void Save()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var data = new PortableOAuthProviderConfigData
        {
            Providers = CloneProviders(_providers)
        };
        var tempPath = _filePath + ".tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(data, JsonOptions));
        File.Move(tempPath, _filePath, overwrite: true);
    }

    private bool TryGetKnownProvider(string platform, out string normalizedPlatform)
    {
        normalizedPlatform = NormalizePlatform(platform);
        return !string.IsNullOrWhiteSpace(normalizedPlatform) && _providers.ContainsKey(normalizedPlatform);
    }

    private static string NormalizePlatform(string platform)
    {
        return string.IsNullOrWhiteSpace(platform) ? string.Empty : platform.Trim().ToLowerInvariant();
    }

    private static Dictionary<string, PortableOAuthProviderConfig> CloneProviders(
        IReadOnlyDictionary<string, PortableOAuthProviderConfig> providers)
    {
        return providers.ToDictionary(
            item => item.Key,
            item => CloneProvider(item.Value),
            StringComparer.OrdinalIgnoreCase);
    }

    private static PortableOAuthProviderConfig CloneProvider(PortableOAuthProviderConfig provider)
    {
        return new PortableOAuthProviderConfig
        {
            AuthUrl = provider.AuthUrl ?? string.Empty,
            ClientId = provider.ClientId ?? string.Empty,
            Scope = provider.Scope ?? string.Empty
        };
    }
}
