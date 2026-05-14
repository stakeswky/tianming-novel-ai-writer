using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TM.Services.Framework.AI.Core;

namespace Tianming.Desktop.Avalonia.ViewModels.AI;

/// <summary>
/// M4.6.2 模型管理页 ViewModel — 管理用户 AI 配置（Provider+Model+Endpoint+Temperature）。
/// </summary>
public partial class ModelManagementViewModel : ObservableObject
{
    public static IReadOnlyList<string> PurposeChoices { get; } =
    [
        "Default",
        "Chat",
        "Writing",
        "Polish",
        "Validation",
    ];

    private readonly FileAIConfigurationStore _store;

    public ObservableCollection<ModelConfigItem> Models { get; } = new();

    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private ModelConfigItem? _editingItem;

    // New model form
    [ObservableProperty] private string _newProviderId = string.Empty;
    [ObservableProperty] private string _newModelId = string.Empty;
    [ObservableProperty] private string _newEndpoint = string.Empty;
    [ObservableProperty] private double _newTemperature = 0.7;
    [ObservableProperty] private int _newMaxTokens = 4096;
    [ObservableProperty] private string _newName = string.Empty;

    // 可用 Provider 列表（从 store 加载）
    public ObservableCollection<string> ProviderIds { get; } = new();

    public ModelManagementViewModel(FileAIConfigurationStore store)
    {
        _store = store;
        LoadProviders();
        LoadModels();
        Models.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoModels));
    }

    public bool HasNoModels => Models.Count == 0;

    private void LoadProviders()
    {
        var providers = _store.GetAllProviders();
        foreach (var p in providers)
            ProviderIds.Add(p.Id);
    }

    private void LoadModels()
    {
        Models.Clear();
        foreach (var config in _store.GetAllConfigurations())
        {
            Models.Add(new ModelConfigItem
            {
                Id = config.Id,
                ProviderId = config.ProviderId,
                ModelId = config.ModelId,
                Endpoint = config.CustomEndpoint ?? string.Empty,
                Temperature = config.Temperature,
                MaxTokens = config.MaxTokens,
                IsActive = config.IsActive,
                DisplayName = config.GetDisplayName(),
                Name = config.Name,
                Purpose = string.IsNullOrWhiteSpace(config.Purpose) ? "Default" : config.Purpose
            });
        }
    }

    [RelayCommand]
    private async Task AddModelAsync()
    {
        var config = new UserConfiguration
        {
            ProviderId = NewProviderId,
            ModelId = NewModelId,
            CustomEndpoint = string.IsNullOrWhiteSpace(NewEndpoint) ? null : NewEndpoint,
            Temperature = NewTemperature,
            MaxTokens = NewMaxTokens,
            Name = NewName,
            Purpose = "Default",
            IsActive = Models.Count == 0 // 第一个自动设为 active
        };

        _store.AddConfiguration(config);
        NewProviderId = string.Empty;
        NewModelId = string.Empty;
        NewEndpoint = string.Empty;
        NewName = string.Empty;
        IsEditing = false;
        LoadModels();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SetActiveAsync(string configId)
    {
        _store.SetActiveConfiguration(configId);
        LoadModels();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task DeleteModelAsync(string configId)
    {
        _store.DeleteConfiguration(configId);
        LoadModels();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveModelAsync(ModelConfigItem item)
    {
        var config = new UserConfiguration
        {
            Id = item.Id,
            ProviderId = item.ProviderId,
            ModelId = item.ModelId,
            CustomEndpoint = string.IsNullOrWhiteSpace(item.Endpoint) ? null : item.Endpoint,
            Temperature = item.Temperature,
            MaxTokens = item.MaxTokens,
            Name = item.Name,
            Purpose = string.IsNullOrWhiteSpace(item.Purpose) ? "Default" : item.Purpose,
            IsActive = item.IsActive
        };
        _store.UpdateConfiguration(config);
        IsEditing = false;
        EditingItem = null;
        LoadModels();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private Task StartEditAsync(ModelConfigItem item)
    {
        EditingItem = item;
        IsEditing = true;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task CancelEditAsync()
    {
        IsEditing = false;
        EditingItem = null;
        return Task.CompletedTask;
    }
}

public partial class ModelConfigItem : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _providerId = string.Empty;
    [ObservableProperty] private string _modelId = string.Empty;
    [ObservableProperty] private string _endpoint = string.Empty;
    [ObservableProperty] private double _temperature = 0.7;
    [ObservableProperty] private int _maxTokens = 4096;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _purpose = "Default";

    public IReadOnlyList<string> PurposeOptions { get; } = ModelManagementViewModel.PurposeChoices;
}
