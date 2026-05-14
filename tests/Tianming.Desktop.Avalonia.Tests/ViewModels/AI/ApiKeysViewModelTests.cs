using System;
using System.Collections.Generic;
using System.IO;
using Tianming.Desktop.Avalonia.ViewModels.AI;
using TM.Services.Framework.AI.Core;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.AI;

public class ApiKeysViewModelTests
{
    [Fact]
    public void Empty_library_still_exposes_default_provider_groups()
    {
        using var workspace = new TempDirectory();
        var store = new FileAIConfigurationStore(
            Path.Combine(workspace.Path, "Library"),
            Path.Combine(workspace.Path, "Configurations"));

        var vm = new ApiKeysViewModel(store, new InMemorySecretStore());

        Assert.Contains(vm.Providers, provider =>
            provider.ProviderId == "openai" && !string.IsNullOrWhiteSpace(provider.ProviderName));
    }

    private sealed class InMemorySecretStore : IApiKeySecretStore
    {
        private readonly Dictionary<string, string> _secrets = new();

        public string? GetSecret(string configId)
            => _secrets.TryGetValue(configId, out var secret) ? secret : null;

        public void SaveSecret(string configId, string apiKey)
            => _secrets[configId] = apiKey;

        public void DeleteSecret(string configId)
            => _secrets.Remove(configId);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-api-keys-vm-{Guid.NewGuid():N}");

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
}
