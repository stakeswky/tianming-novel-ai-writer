using System.Text.Json;
using TM.Services.Framework.AI.Core;
using Xunit;

namespace Tianming.AI.Tests;

public class FileAIConfigurationStoreTests
{
    [Fact]
    public void Loads_library_from_wrapped_json_and_orders_results()
    {
        using var workspace = new TempDirectory();
        var library = System.IO.Path.Combine(workspace.Path, "Library");
        Directory.CreateDirectory(library);
        File.WriteAllText(System.IO.Path.Combine(library, "categories.json"), """
        { "Categories": [
          { "Id": "local", "Name": "Local", "Order": 2 },
          { "Id": "cloud", "Name": "Cloud", "Order": 1 }
        ] }
        """);
        File.WriteAllText(System.IO.Path.Combine(library, "providers.json"), """
        { "Providers": [
          { "Id": "anthropic", "Name": "Anthropic", "Category": "cloud", "Order": 2 },
          { "Id": "openai", "Name": "OpenAI", "Category": "cloud", "Order": 1 }
        ] }
        """);
        File.WriteAllText(System.IO.Path.Combine(library, "models.json"), """
        { "Models": [
          { "Id": "m2", "ProviderId": "openai", "Name": "gpt-4.1-mini", "Order": 2 },
          { "Id": "m1", "ProviderId": "openai", "Name": "gpt-4.1", "Order": 1 }
        ] }
        """);

        var store = new FileAIConfigurationStore(library, System.IO.Path.Combine(workspace.Path, "Configurations"));

        Assert.Equal(["cloud", "local"], store.GetAllCategories().Select(category => category.Id).ToArray());
        Assert.Equal(["openai", "anthropic"], store.GetAllProviders().Select(provider => provider.Id).ToArray());
        Assert.Equal(["m1", "m2"], store.GetModelsByProvider("openai").Select(model => model.Id).ToArray());
        Assert.Equal("gpt-4.1", store.GetModelById("m1")?.Name);
        Assert.Equal("m2", store.GetModelById("gpt-4.1-mini")?.Id);
    }

    [Fact]
    public void Loads_models_from_provider_models_directory_when_models_file_is_missing()
    {
        using var workspace = new TempDirectory();
        var library = System.IO.Path.Combine(workspace.Path, "Library");
        var providerModels = System.IO.Path.Combine(library, "ProviderModels");
        Directory.CreateDirectory(providerModels);
        File.WriteAllText(System.IO.Path.Combine(providerModels, "openai.models.json"), """
        [
          { "Id": "gpt", "ProviderId": "openai", "Name": "gpt-4.1", "Order": 2 },
          { "Id": "mini", "ProviderId": "openai", "Name": "gpt-4.1-mini", "Order": 1 }
        ]
        """);

        var store = new FileAIConfigurationStore(library, System.IO.Path.Combine(workspace.Path, "Configurations"));

        Assert.Equal(["mini", "gpt"], store.GetAllModels().Select(model => model.Id).ToArray());
    }

    [Fact]
    public void Configuration_crud_persists_without_api_key_and_maintains_single_active_config()
    {
        using var workspace = new TempDirectory();
        var configs = System.IO.Path.Combine(workspace.Path, "Configurations");
        var store = new FileAIConfigurationStore(System.IO.Path.Combine(workspace.Path, "Library"), configs);
        var changes = 0;
        store.ConfigurationsChanged += (_, _) => changes++;

        store.AddConfiguration(new UserConfiguration
        {
            Name = "Main",
            ProviderId = "openai",
            ModelId = "gpt",
            ApiKey = "secret",
            IsActive = true,
            Purpose = "Writing"
        });
        store.AddConfiguration(new UserConfiguration
        {
            Name = "Backup",
            ProviderId = "anthropic",
            ModelId = "claude",
            ApiKey = "other-secret",
            Purpose = "Validation"
        });
        var backup = Assert.Single(store.GetAllConfigurations(), config => config.Name == "Backup");
        store.SetActiveConfiguration(backup.Id);

        var reloaded = new FileAIConfigurationStore(System.IO.Path.Combine(workspace.Path, "Library"), configs);
        var configFile = System.IO.Path.Combine(configs, "user_configurations.json");
        var json = File.ReadAllText(configFile);

        Assert.Equal(3, changes);
        Assert.Equal("Backup", reloaded.GetActiveConfiguration()?.Name);
        Assert.Equal("Validation", reloaded.GetActiveConfiguration()?.Purpose);
        Assert.All(reloaded.GetAllConfigurations(), config => Assert.False(config.ApiKey.Length > 0));
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ApiKey", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Configuration_crud_with_secret_store_persists_api_key_outside_json_and_rehydrates()
    {
        using var workspace = new TempDirectory();
        var configs = System.IO.Path.Combine(workspace.Path, "Configurations");
        var secretStore = new InMemorySecretStore();
        var store = new FileAIConfigurationStore(System.IO.Path.Combine(workspace.Path, "Library"), configs, secretStore);

        store.AddConfiguration(new UserConfiguration
        {
            Name = "Main",
            ProviderId = "openai",
            ModelId = "gpt",
            ApiKey = "secret",
            IsActive = true
        });

        var stored = Assert.Single(store.GetAllConfigurations());
        var configFile = System.IO.Path.Combine(configs, "user_configurations.json");
        var json = File.ReadAllText(configFile);
        var reloaded = new FileAIConfigurationStore(System.IO.Path.Combine(workspace.Path, "Library"), configs, secretStore);

        Assert.Equal("secret", secretStore.GetSecret(stored.Id));
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("secret", reloaded.GetActiveConfiguration()?.ApiKey);
    }

    [Fact]
    public void UpdateConfiguration_with_blank_api_key_preserves_existing_secret()
    {
        using var workspace = new TempDirectory();
        var configs = System.IO.Path.Combine(workspace.Path, "Configurations");
        var secretStore = new InMemorySecretStore();
        var store = new FileAIConfigurationStore(System.IO.Path.Combine(workspace.Path, "Library"), configs, secretStore);

        store.AddConfiguration(new UserConfiguration
        {
            Name = "Main",
            ProviderId = "openai",
            ModelId = "gpt",
            ApiKey = "secret"
        });
        var config = store.GetAllConfigurations()[0];
        config.Name = "Renamed";
        config.ApiKey = string.Empty;

        store.UpdateConfiguration(config);
        var reloaded = new FileAIConfigurationStore(System.IO.Path.Combine(workspace.Path, "Library"), configs, secretStore);

        Assert.Equal("secret", secretStore.GetSecret(config.Id));
        Assert.Equal("secret", reloaded.GetAllConfigurations()[0].ApiKey);
        Assert.Equal("Renamed", reloaded.GetAllConfigurations()[0].Name);
    }

    [Fact]
    public void DeleteConfiguration_removes_secret_store_entry()
    {
        using var workspace = new TempDirectory();
        var configs = System.IO.Path.Combine(workspace.Path, "Configurations");
        var secretStore = new InMemorySecretStore();
        var store = new FileAIConfigurationStore(System.IO.Path.Combine(workspace.Path, "Library"), configs, secretStore);
        store.AddConfiguration(new UserConfiguration
        {
            Name = "Only",
            ProviderId = "openai",
            ModelId = "gpt",
            ApiKey = "secret"
        });
        var id = store.GetAllConfigurations()[0].Id;

        store.DeleteConfiguration(id);

        Assert.Null(secretStore.GetSecret(id));
    }

    [Fact]
    public void AddConfiguration_updates_existing_provider_model_pair_instead_of_duplicating()
    {
        using var workspace = new TempDirectory();
        var configs = System.IO.Path.Combine(workspace.Path, "Configurations");
        var store = new FileAIConfigurationStore(System.IO.Path.Combine(workspace.Path, "Library"), configs);

        store.AddConfiguration(new UserConfiguration
        {
            Name = "First",
            ProviderId = "openai",
            ModelId = "gpt"
        });
        var id = store.GetAllConfigurations()[0].Id;
        store.AddConfiguration(new UserConfiguration
        {
            Name = "Renamed",
            ProviderId = "OPENAI",
            ModelId = "gpt",
            Temperature = 0.2
        });

        var config = Assert.Single(store.GetAllConfigurations());
        Assert.Equal(id, config.Id);
        Assert.Equal("Renamed", config.Name);
        Assert.Equal(0.2, config.Temperature);
    }

    [Fact]
    public void DeleteConfiguration_removes_config_and_persists_change()
    {
        using var workspace = new TempDirectory();
        var configs = System.IO.Path.Combine(workspace.Path, "Configurations");
        var store = new FileAIConfigurationStore(System.IO.Path.Combine(workspace.Path, "Library"), configs);
        store.AddConfiguration(new UserConfiguration { Name = "Only", ProviderId = "openai", ModelId = "gpt" });
        var id = store.GetAllConfigurations()[0].Id;

        store.DeleteConfiguration(id);
        var reloaded = new FileAIConfigurationStore(System.IO.Path.Combine(workspace.Path, "Library"), configs);

        Assert.Empty(reloaded.GetAllConfigurations());
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-ai-config-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }

    private sealed class InMemorySecretStore : IApiKeySecretStore
    {
        private readonly Dictionary<string, string> _secrets = new();

        public string? GetSecret(string configId)
        {
            return _secrets.TryGetValue(configId, out var secret) ? secret : null;
        }

        public void SaveSecret(string configId, string apiKey)
        {
            _secrets[configId] = apiKey;
        }

        public void DeleteSecret(string configId)
        {
            _secrets.Remove(configId);
        }
    }
}
