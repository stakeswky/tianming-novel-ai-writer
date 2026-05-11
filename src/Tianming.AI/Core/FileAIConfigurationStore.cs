using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using TM.Framework.Common.Helpers.Id;

namespace TM.Services.Framework.AI.Core;

public sealed class FileAIConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _libraryPath;
    private readonly string _configurationsPath;
    private readonly IApiKeySecretStore? _secretStore;
    private readonly object _configurationsLock = new();
    private List<AICategory> _categories = new();
    private List<AIProvider> _providers = new();
    private List<AIModel> _models = new();
    private List<UserConfiguration> _userConfigurations = new();

    public FileAIConfigurationStore(string libraryPath, string configurationsPath, IApiKeySecretStore? secretStore = null)
    {
        if (string.IsNullOrWhiteSpace(libraryPath))
            throw new ArgumentException("模型库目录不能为空", nameof(libraryPath));
        if (string.IsNullOrWhiteSpace(configurationsPath))
            throw new ArgumentException("配置目录不能为空", nameof(configurationsPath));

        _libraryPath = libraryPath;
        _configurationsPath = configurationsPath;
        _secretStore = secretStore;
        LoadLibrary();
        LoadUserConfigurations();
    }

    public event EventHandler? ConfigurationsChanged;

    public IReadOnlyList<AICategory> GetAllCategories() => _categories.OrderBy(category => category.Order).ToList();

    public IReadOnlyList<AIProvider> GetAllProviders() => _providers.OrderBy(provider => provider.Order).ToList();

    public IReadOnlyList<AIProvider> GetProvidersByCategory(string categoryId)
    {
        return _providers
            .Where(provider => provider.Category == categoryId)
            .OrderBy(provider => provider.Order)
            .ToList();
    }

    public IReadOnlyList<AIModel> GetAllModels() => _models.OrderBy(model => model.Order).ToList();

    public IReadOnlyList<AIModel> GetModelsByProvider(string providerId)
    {
        return _models
            .Where(model => model.ProviderId == providerId)
            .OrderBy(model => model.Order)
            .ToList();
    }

    public AIProvider? GetProviderById(string providerId)
    {
        return _providers.FirstOrDefault(provider => provider.Id == providerId);
    }

    public AIModel? GetModelById(string modelId)
    {
        return _models.FirstOrDefault(model => model.Id == modelId)
            ?? _models.FirstOrDefault(model => model.Name == modelId);
    }

    public IReadOnlyList<UserConfiguration> GetAllConfigurations()
    {
        lock (_configurationsLock)
        {
            return _userConfigurations.Select(CloneConfiguration).ToList();
        }
    }

    public UserConfiguration? GetActiveConfiguration()
    {
        lock (_configurationsLock)
        {
            var active = _userConfigurations.FirstOrDefault(config => config.IsActive);
            return active == null ? null : CloneConfiguration(active);
        }
    }

    public void AddConfiguration(UserConfiguration config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        UserConfiguration? existing;
        lock (_configurationsLock)
        {
            existing = _userConfigurations.FirstOrDefault(item =>
                string.Equals(item.ProviderId, config.ProviderId, StringComparison.OrdinalIgnoreCase)
                && string.Equals(item.ModelId, config.ModelId, StringComparison.OrdinalIgnoreCase));
        }

        if (existing != null)
        {
            config.Id = existing.Id;
            config.CreatedAt = existing.CreatedAt;
            UpdateConfiguration(config);
            return;
        }

        var toAdd = CloneConfiguration(config);
        toAdd.Id = ShortIdGenerator.New("D");
        toAdd.CreatedAt = DateTime.Now;
        toAdd.UpdatedAt = DateTime.Now;

        lock (_configurationsLock)
        {
            if (toAdd.IsActive)
            {
                foreach (var item in _userConfigurations)
                    item.IsActive = false;
            }

            _userConfigurations.Add(toAdd);
        }

        SaveApiKeySecret(toAdd);
        SaveUserConfigurations();
        ConfigurationsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateConfiguration(UserConfiguration config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        var updated = false;
        lock (_configurationsLock)
        {
            var index = _userConfigurations.FindIndex(item => item.Id == config.Id);
            if (index >= 0)
            {
                var next = CloneConfiguration(config);
                next.UpdatedAt = DateTime.Now;
                if (string.IsNullOrWhiteSpace(next.ApiKey) && _secretStore != null)
                    next.ApiKey = _userConfigurations[index].ApiKey;

                if (next.IsActive)
                {
                    foreach (var item in _userConfigurations)
                        item.IsActive = false;
                }

                _userConfigurations[index] = next;
                SaveApiKeySecret(next);
                updated = true;
            }
        }

        if (!updated)
            return;

        SaveUserConfigurations();
        ConfigurationsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteConfiguration(string configId)
    {
        var removed = false;
        lock (_configurationsLock)
        {
            var config = _userConfigurations.FirstOrDefault(item => item.Id == configId);
            if (config != null)
                removed = _userConfigurations.Remove(config);
        }

        if (!removed)
            return;

        _secretStore?.DeleteSecret(configId);
        SaveUserConfigurations();
        ConfigurationsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetActiveConfiguration(string configId)
    {
        lock (_configurationsLock)
        {
            foreach (var config in _userConfigurations)
                config.IsActive = config.Id == configId;
        }

        SaveUserConfigurations();
        ConfigurationsChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ReloadLibrary()
    {
        LoadLibrary();
    }

    private void LoadLibrary()
    {
        _categories = LoadWrappedOrArray<CategoryWrapper, AICategory>(
            Path.Combine(_libraryPath, "categories.json"),
            wrapper => wrapper.Categories);

        _providers = LoadWrappedOrArray<ProviderWrapper, AIProvider>(
            Path.Combine(_libraryPath, "providers.json"),
            wrapper => wrapper.Providers);

        var modelsFile = Path.Combine(_libraryPath, "models.json");
        if (File.Exists(modelsFile))
        {
            _models = LoadWrappedOrArray<ModelWrapper, AIModel>(modelsFile, wrapper => wrapper.Models);
            return;
        }

        _models = LoadProviderModels();
    }

    private List<AIModel> LoadProviderModels()
    {
        var providerModelsPath = Path.Combine(_libraryPath, "ProviderModels");
        if (!Directory.Exists(providerModelsPath))
            return new List<AIModel>();

        var result = new List<AIModel>();
        foreach (var file in Directory.GetFiles(providerModelsPath, "*.models.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var models = JsonSerializer.Deserialize<List<AIModel>>(json, JsonOptions);
                if (models != null)
                    result.AddRange(models);
            }
            catch (JsonException)
            {
            }
            catch (IOException)
            {
            }
        }

        return result;
    }

    private static List<TItem> LoadWrappedOrArray<TWrapper, TItem>(
        string path,
        Func<TWrapper, List<TItem>> unwrap)
        where TWrapper : class
    {
        if (!File.Exists(path))
            return new List<TItem>();

        try
        {
            var json = File.ReadAllText(path);
            var trimmed = json.TrimStart();
            if (trimmed.StartsWith("[", StringComparison.Ordinal))
                return JsonSerializer.Deserialize<List<TItem>>(json, JsonOptions) ?? new List<TItem>();

            var wrapper = JsonSerializer.Deserialize<TWrapper>(json, JsonOptions);
            return wrapper == null ? new List<TItem>() : unwrap(wrapper);
        }
        catch (JsonException)
        {
            return new List<TItem>();
        }
        catch (IOException)
        {
            return new List<TItem>();
        }
    }

    private void LoadUserConfigurations()
    {
        var configFile = GetConfigurationsFile();
        if (!File.Exists(configFile))
            return;

        try
        {
            var json = File.ReadAllText(configFile);
            var wrapper = JsonSerializer.Deserialize<ConfigurationWrapper>(json, JsonOptions);
            lock (_configurationsLock)
            {
                _userConfigurations = wrapper?.Configurations ?? new List<UserConfiguration>();
                HydrateApiKeysFromSecretStore(_userConfigurations);
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }
    }

    private void SaveUserConfigurations()
    {
        Directory.CreateDirectory(_configurationsPath);
        var configFile = GetConfigurationsFile();
        List<UserConfiguration> snapshot;
        lock (_configurationsLock)
        {
            snapshot = _userConfigurations
                .Select(CloneForStorage)
                .ToList();
        }

        var wrapper = new ConfigurationWrapper
        {
            Configurations = snapshot
        };
        var tempPath = $"{configFile}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(wrapper, JsonOptions));
        File.Move(tempPath, configFile, overwrite: true);
    }

    private string GetConfigurationsFile()
    {
        return Path.Combine(_configurationsPath, "user_configurations.json");
    }

    private void SaveApiKeySecret(UserConfiguration config)
    {
        if (_secretStore == null || string.IsNullOrWhiteSpace(config.Id) || string.IsNullOrWhiteSpace(config.ApiKey))
            return;

        _secretStore.SaveSecret(config.Id, config.ApiKey);
    }

    private void HydrateApiKeysFromSecretStore(List<UserConfiguration> configurations)
    {
        if (_secretStore == null)
            return;

        foreach (var config in configurations)
        {
            if (string.IsNullOrWhiteSpace(config.Id))
                continue;

            var secret = _secretStore.GetSecret(config.Id);
            if (!string.IsNullOrWhiteSpace(secret))
                config.ApiKey = secret;
        }
    }

    private static UserConfiguration CloneConfiguration(UserConfiguration config)
    {
        return new UserConfiguration
        {
            Id = config.Id,
            Name = config.Name,
            ProviderId = config.ProviderId,
            ModelId = config.ModelId,
            ApiKey = config.ApiKey,
            CustomEndpoint = config.CustomEndpoint,
            Temperature = config.Temperature,
            MaxTokens = config.MaxTokens,
            ContextWindow = config.ContextWindow,
            IsActive = config.IsActive,
            IsEnabled = config.IsEnabled,
            CreatedAt = config.CreatedAt,
            UpdatedAt = config.UpdatedAt,
            DeveloperMessage = config.DeveloperMessage
        };
    }

    private static UserConfiguration CloneForStorage(UserConfiguration config)
    {
        var clone = CloneConfiguration(config);
        clone.ApiKey = string.Empty;
        return clone;
    }

    private sealed class CategoryWrapper
    {
        [JsonPropertyName("Categories")]
        public List<AICategory> Categories { get; set; } = new();
    }

    private sealed class ProviderWrapper
    {
        [JsonPropertyName("Providers")]
        public List<AIProvider> Providers { get; set; } = new();
    }

    private sealed class ModelWrapper
    {
        [JsonPropertyName("Models")]
        public List<AIModel> Models { get; set; } = new();
    }

    private sealed class ConfigurationWrapper
    {
        [JsonPropertyName("Configurations")]
        public List<UserConfiguration> Configurations { get; set; } = new();
    }
}
