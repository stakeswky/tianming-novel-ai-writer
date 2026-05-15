using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Tianming.Desktop.Avalonia.ViewModels.AI;
using TM.Services.Framework.AI.Core;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.ViewModels.AI;

public class ModelManagementViewModelTests
{
    [Fact]
    public void LoadModels_reads_purpose_from_store()
    {
        using var workspace = new TempDirectory();
        var store = new FileAIConfigurationStore(
            Path.Combine(workspace.Path, "Library"),
            Path.Combine(workspace.Path, "Configurations"));
        store.AddConfiguration(new UserConfiguration
        {
            ProviderId = "openai",
            ModelId = "gpt",
            Purpose = "Writing"
        });

        var vm = new ModelManagementViewModel(store);

        Assert.Equal("Writing", Assert.Single(vm.Models).Purpose);
    }

    [Fact]
    public async Task SaveModelCommand_persists_updated_purpose()
    {
        using var workspace = new TempDirectory();
        var configs = Path.Combine(workspace.Path, "Configurations");
        var store = new FileAIConfigurationStore(Path.Combine(workspace.Path, "Library"), configs);
        store.AddConfiguration(new UserConfiguration
        {
            ProviderId = "openai",
            ModelId = "gpt",
            Purpose = "Default"
        });
        var vm = new ModelManagementViewModel(store);
        var item = Assert.Single(vm.Models);
        item.Purpose = "Polish";

        await vm.SaveModelCommand.ExecuteAsync(item);

        var reloaded = new FileAIConfigurationStore(Path.Combine(workspace.Path, "Library"), configs);
        Assert.Equal("Polish", Assert.Single(reloaded.GetAllConfigurations()).Purpose);
    }

    [Fact]
    public async Task SaveModelCommand_preserves_existing_developer_message()
    {
        using var workspace = new TempDirectory();
        var configs = Path.Combine(workspace.Path, "Configurations");
        var store = new FileAIConfigurationStore(Path.Combine(workspace.Path, "Library"), configs);
        store.AddConfiguration(new UserConfiguration
        {
            ProviderId = "openai",
            ModelId = "gpt",
            Purpose = "Default",
            DeveloperMessage = "keep me"
        });
        var vm = new ModelManagementViewModel(store);
        var item = Assert.Single(vm.Models);
        item.Purpose = "Validation";

        await vm.SaveModelCommand.ExecuteAsync(item);

        var reloaded = new FileAIConfigurationStore(Path.Combine(workspace.Path, "Library"), configs);
        Assert.Equal("keep me", Assert.Single(reloaded.GetAllConfigurations()).DeveloperMessage);
    }

    [Fact]
    public async Task AddModelCommand_defaults_new_models_to_default_purpose()
    {
        using var workspace = new TempDirectory();
        var configs = Path.Combine(workspace.Path, "Configurations");
        var store = new FileAIConfigurationStore(Path.Combine(workspace.Path, "Library"), configs);
        var vm = new ModelManagementViewModel(store)
        {
            NewProviderId = "openai",
            NewModelId = "gpt"
        };

        await vm.AddModelCommand.ExecuteAsync(null);

        var reloaded = new FileAIConfigurationStore(Path.Combine(workspace.Path, "Library"), configs);
        Assert.Equal("Default", Assert.Single(reloaded.GetAllConfigurations()).Purpose);
    }

    [Fact]
    public void Empty_library_still_exposes_default_provider_ids()
    {
        using var workspace = new TempDirectory();
        var store = new FileAIConfigurationStore(
            Path.Combine(workspace.Path, "Library"),
            Path.Combine(workspace.Path, "Configurations"));

        var vm = new ModelManagementViewModel(store);

        Assert.Contains("openai", vm.ProviderIds);
        Assert.All(vm.ProviderIds, providerId => Assert.False(string.IsNullOrWhiteSpace(providerId)));
    }

    [Fact]
    public async Task MoveModelUpCommand_swaps_with_previous_item()
    {
        using var workspace = new TempDirectory();
        var store = new FileAIConfigurationStore(
            Path.Combine(workspace.Path, "Library"),
            Path.Combine(workspace.Path, "Configurations"));
        store.AddConfiguration(new UserConfiguration { ProviderId = "openai", ModelId = "gpt-1" });
        store.AddConfiguration(new UserConfiguration { ProviderId = "openai", ModelId = "gpt-2" });
        var vm = new ModelManagementViewModel(store);
        var second = vm.Models[1];

        await vm.MoveModelUpCommand.ExecuteAsync(second.Id);

        Assert.Equal(second.Id, vm.Models[0].Id);
    }

    [Fact]
    public async Task MoveModelUpCommand_at_top_is_noop()
    {
        using var workspace = new TempDirectory();
        var store = new FileAIConfigurationStore(
            Path.Combine(workspace.Path, "Library"),
            Path.Combine(workspace.Path, "Configurations"));
        store.AddConfiguration(new UserConfiguration { ProviderId = "openai", ModelId = "gpt-1" });
        store.AddConfiguration(new UserConfiguration { ProviderId = "openai", ModelId = "gpt-2" });
        var vm = new ModelManagementViewModel(store);
        var first = vm.Models[0];

        await vm.MoveModelUpCommand.ExecuteAsync(first.Id);

        Assert.Equal(first.Id, vm.Models[0].Id);  // 仍在第一位
    }

    [Fact]
    public async Task MoveModelDownCommand_swaps_with_next_item()
    {
        using var workspace = new TempDirectory();
        var store = new FileAIConfigurationStore(
            Path.Combine(workspace.Path, "Library"),
            Path.Combine(workspace.Path, "Configurations"));
        store.AddConfiguration(new UserConfiguration { ProviderId = "openai", ModelId = "gpt-1" });
        store.AddConfiguration(new UserConfiguration { ProviderId = "openai", ModelId = "gpt-2" });
        var vm = new ModelManagementViewModel(store);
        var first = vm.Models[0];

        await vm.MoveModelDownCommand.ExecuteAsync(first.Id);

        Assert.Equal(first.Id, vm.Models[1].Id);
    }

    [Fact]
    public void Empty_library_exposes_default_provider_display_names()
    {
        using var workspace = new TempDirectory();
        var store = new FileAIConfigurationStore(
            Path.Combine(workspace.Path, "Library"),
            Path.Combine(workspace.Path, "Configurations"));

        var vm = new ModelManagementViewModel(store);

        Assert.Contains(vm.Providers, provider => provider.Id == "openai" && provider.DisplayName == "OpenAI");
        Assert.DoesNotContain(vm.Providers, provider => provider.DisplayName == provider.Id);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-model-vm-{Guid.NewGuid():N}");

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
