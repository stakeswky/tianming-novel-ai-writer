using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using TM.Framework.Common.Models;
using TM.Services.Modules.ProjectData.Modules;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels;

/// <summary>
/// Schema-driven 数据管理 VM 基类。供 M4.1 设计模块 6 页、M4.2 生成规划 4 页继承。
/// 业务逻辑全部走 ModuleDataAdapter，不引 SK/Kernel/WPF。
/// </summary>
public abstract partial class DataManagementViewModel<TCategory, TData, TSchema> : ObservableObject
    where TCategory : class, ICategory
    where TData : class, IDataItem
    where TSchema : IModuleSchema<TCategory, TData>
{
    protected ModuleDataAdapter<TCategory, TData> Adapter { get; }

    public TSchema Schema => (TSchema)Adapter.Schema;

    public string PageTitle => Adapter.Schema.PageTitle;
    public string PageIcon => Adapter.Schema.PageIcon;
    public IReadOnlyList<FieldDescriptor> Fields => Adapter.Schema.Fields;

    public ObservableCollection<TCategory> Categories { get; } = new();
    public ObservableCollection<TData> Items { get; } = new();

    [ObservableProperty]
    private TCategory? _selectedCategory;

    [ObservableProperty]
    private TData? _selectedItem;

    [ObservableProperty]
    private bool _isLoading;

    protected DataManagementViewModel(ModuleDataAdapter<TCategory, TData> adapter)
    {
        Adapter = adapter ?? throw new System.ArgumentNullException(nameof(adapter));
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            await Adapter.LoadAsync().ConfigureAwait(false);
            Categories.Clear();
            foreach (var c in Adapter.GetCategories())
                Categories.Add(c);
            Items.Clear();
            foreach (var d in Adapter.GetData())
                Items.Add(d);
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<bool> AddCategoryAsync(string name)
    {
        var cat = Adapter.Schema.CreateNewCategory(name);
        var ok = await Adapter.AddCategoryAsync(cat).ConfigureAwait(false);
        if (ok)
            Categories.Add(cat);
        return ok;
    }

    public async Task<TData?> AddNewItemAsync(string categoryName, string name)
    {
        var item = Adapter.Schema.CreateNewItem();
        item.Name = name;
        item.Category = categoryName;
        await Adapter.AddAsync(item).ConfigureAwait(false);
        Items.Add(item);
        return item;
    }

    public async Task UpdateSelectedItemAsync()
    {
        if (SelectedItem == null) return;
        await Adapter.UpdateAsync(SelectedItem).ConfigureAwait(false);
    }

    public async Task<bool> DeleteSelectedItemAsync()
    {
        if (SelectedItem == null) return false;
        var ok = await Adapter.DeleteAsync(SelectedItem.Id).ConfigureAwait(false);
        if (ok)
        {
            Items.Remove(SelectedItem);
            SelectedItem = null;
        }
        return ok;
    }

    public Task<CascadeDeleteResult> CascadeDeleteCategoryAsync(string categoryName)
        => Adapter.CascadeDeleteCategoryAsync(categoryName);

    public IEnumerable<TData> ItemsInSelectedCategory()
        => SelectedCategory == null
            ? Enumerable.Empty<TData>()
            : Adapter.GetDataForCategory(SelectedCategory);
}
