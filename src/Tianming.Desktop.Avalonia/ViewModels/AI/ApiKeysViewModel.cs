using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TM.Services.Framework.AI.Core;

namespace Tianming.Desktop.Avalonia.ViewModels.AI;

/// <summary>
/// M4.6.3 API 密钥管理页 ViewModel — 按 Provider 分组管理 API Key。
/// IApiKeySecretStore 方法为同步，Commands 中用 Task.Run 包装。
/// </summary>
public partial class ApiKeysViewModel : ObservableObject
{
    private readonly FileAIConfigurationStore _configStore;
    private readonly IApiKeySecretStore _secretStore;

    public ObservableCollection<ProviderKeyGroup> Providers { get; } = new();

    [ObservableProperty] private string _newKeyInput = string.Empty;
    [ObservableProperty] private string _newKeyRemark = string.Empty;
    [ObservableProperty] private ProviderKeyGroup? _selectedProviderItem;
    [ObservableProperty] private bool _showNewKeyPlain;

    public ApiKeysViewModel(FileAIConfigurationStore configStore, IApiKeySecretStore secretStore)
    {
        _configStore = configStore;
        _secretStore = secretStore;
        LoadProviders();
    }

    public bool HasNoKeys => Providers.All(p => p.Keys.Count == 0);

    private void LoadProviders()
    {
        Providers.Clear();
        var configs = _configStore.GetAllConfigurations();
        var allProviders = _configStore.GetAllProviders();

        var grouped = configs
            .GroupBy(c => c.ProviderId)
            .OrderBy(g => g.Key);

        var hasGroups = false;
        foreach (var group in grouped)
        {
            hasGroups = true;
            var providerName = allProviders
                .FirstOrDefault(p => p.Id == group.Key)?.Name ?? group.Key;

            var providerGroup = new ProviderKeyGroup
            {
                ProviderId = group.Key,
                ProviderName = providerName
            };

            foreach (var config in group)
            {
                var secret = _secretStore.GetSecret(config.Id);
                providerGroup.Keys.Add(new ApiKeyItem
                {
                    Id = config.Id,
                    ConfigName = config.GetDisplayName(),
                    MaskedKey = MaskKey(secret),
                    IsEnabled = config.IsEnabled,
                    HasKey = !string.IsNullOrEmpty(secret)
                });
            }

            Providers.Add(providerGroup);
        }

        if (!hasGroups && allProviders.Count == 0)
        {
            foreach (var provider in DefaultAIProviders.Options)
            {
                Providers.Add(new ProviderKeyGroup
                {
                    ProviderId = provider.Id,
                    ProviderName = provider.Name,
                });
            }
        }
    }

    private static string MaskKey(string? key)
    {
        if (string.IsNullOrEmpty(key))
            return "未设置";
        if (key.Length <= 4)
            return "****";
        return "****" + key[^4..];
    }

    [RelayCommand]
    private async Task SaveKeyAsync()
    {
        if (SelectedProviderItem == null || string.IsNullOrWhiteSpace(NewKeyInput))
            return;

        // 找到该 provider 下第一个没有 key 的配置，或新建
        var configs = _configStore.GetAllConfigurations()
            .Where(c => c.ProviderId == SelectedProviderItem.ProviderId)
            .ToList();

        var config = configs.FirstOrDefault(c => string.IsNullOrEmpty(_secretStore.GetSecret(c.Id)));
        if (config == null && configs.Count > 0)
        {
            config = configs.First();
        }

        if (config == null)
        {
            // 需要用户先在模型管理页创建配置
            return;
        }

        await Task.Run(() => _secretStore.SaveSecret(config.Id, NewKeyInput));

        // 更新配置的 IsEnabled
        config.IsEnabled = true;
        _configStore.UpdateConfiguration(config);

        NewKeyInput = string.Empty;
        NewKeyRemark = string.Empty;
        LoadProviders();
        OnPropertyChanged(nameof(HasNoKeys));
    }

    [RelayCommand]
    private async Task ToggleKeyAsync(string keyId)
    {
        var config = _configStore.GetAllConfigurations()
            .FirstOrDefault(c => c.Id == keyId);
        if (config == null)
            return;

        config.IsEnabled = !config.IsEnabled;
        _configStore.UpdateConfiguration(config);
        await Task.CompletedTask;
        LoadProviders();
    }

    [RelayCommand]
    private async Task DeleteKeyAsync(string keyId)
    {
        await Task.Run(() => _secretStore.DeleteSecret(keyId));
        LoadProviders();
    }
}

public partial class ProviderKeyGroup : ObservableObject
{
    [ObservableProperty] private string _providerId = string.Empty;
    [ObservableProperty] private string _providerName = string.Empty;
    public ObservableCollection<ApiKeyItem> Keys { get; } = new();
}

public partial class ApiKeyItem : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _configName = string.Empty;
    [ObservableProperty] private string _maskedKey = string.Empty;
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private bool _hasKey;
}
