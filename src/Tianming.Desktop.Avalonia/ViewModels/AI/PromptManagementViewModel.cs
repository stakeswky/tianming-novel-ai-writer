using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TM.Modules.AIAssistant.PromptTools.PromptManagement.Models;
using TM.Modules.AIAssistant.PromptTools.PromptManagement.Services;

namespace Tianming.Desktop.Avalonia.ViewModels.AI;

/// <summary>
/// M4.6.4 提示词管理页 ViewModel — CRUD 提示词模板。
/// </summary>
public partial class PromptManagementViewModel : ObservableObject
{
    private readonly FilePromptTemplateStore _store;

    public ObservableCollection<PromptTemplateItem> Templates { get; } = new();

    [ObservableProperty] private PromptTemplateItem? _selectedItem;
    [ObservableProperty] private bool _isEditing;

    // 编辑表单
    [ObservableProperty] private string _editName = string.Empty;
    [ObservableProperty] private string _editDescription = string.Empty;
    [ObservableProperty] private string _editTemplate = string.Empty;
    [ObservableProperty] private string _editVariables = string.Empty;
    [ObservableProperty] private string _editCategory = string.Empty;

    public PromptManagementViewModel(FilePromptTemplateStore store)
    {
        _store = store;
        LoadTemplates();
    }

    private void LoadTemplates()
    {
        Templates.Clear();
        foreach (var t in _store.GetAllTemplates())
        {
            Templates.Add(new PromptTemplateItem
            {
                Id = t.Id,
                Name = t.Name,
                Description = t.Description,
                Template = t.SystemPrompt,
                Variables = t.Variables,
                Category = t.Category,
                IsBuiltIn = t.IsBuiltIn,
                IsEnabled = t.IsEnabled
            });
        }
    }

    [RelayCommand]
    private Task AddTemplateAsync()
    {
        SelectedItem = null;
        EditName = string.Empty;
        EditDescription = string.Empty;
        EditTemplate = string.Empty;
        EditVariables = string.Empty;
        EditCategory = string.Empty;
        IsEditing = true;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private Task SelectTemplateAsync(PromptTemplateItem item)
    {
        SelectedItem = item;
        EditName = item.Name;
        EditDescription = item.Description;
        EditTemplate = item.Template;
        EditVariables = item.Variables;
        EditCategory = item.Category;
        IsEditing = true;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveTemplateAsync()
    {
        if (SelectedItem != null)
        {
            // 更新
            var existing = _store.GetTemplateById(SelectedItem.Id);
            if (existing != null)
            {
                existing.Name = EditName;
                existing.Description = EditDescription;
                existing.SystemPrompt = EditTemplate;
                existing.Variables = EditVariables;
                existing.Category = EditCategory;
                _store.UpdateTemplate(existing);
            }
        }
        else
        {
            // 新增
            var template = new PromptTemplateData
            {
                Name = EditName,
                Description = EditDescription,
                SystemPrompt = EditTemplate,
                Variables = EditVariables,
                Category = EditCategory,
                IsEnabled = true
            };
            _store.AddTemplate(template);
        }

        IsEditing = false;
        SelectedItem = null;
        LoadTemplates();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task DeleteTemplateAsync(string id)
    {
        _store.DeleteTemplate(id);
        if (SelectedItem?.Id == id)
        {
            SelectedItem = null;
            IsEditing = false;
        }
        LoadTemplates();
        await Task.CompletedTask;
    }

    [RelayCommand]
    private Task CancelEditAsync()
    {
        IsEditing = false;
        SelectedItem = null;
        return Task.CompletedTask;
    }
}

public partial class PromptTemplateItem : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _template = string.Empty;
    [ObservableProperty] private string _variables = string.Empty;
    [ObservableProperty] private string _category = string.Empty;
    [ObservableProperty] private bool _isBuiltIn;
    [ObservableProperty] private bool _isEnabled;
}
