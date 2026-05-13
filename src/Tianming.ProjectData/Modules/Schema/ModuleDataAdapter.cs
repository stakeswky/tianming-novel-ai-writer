using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TM.Framework.Common.Models;

namespace TM.Services.Modules.ProjectData.Modules.Schema;

/// <summary>
/// FileModuleDataStore 之上的薄壳：携带 IModuleSchema，提供 CRUD/CascadeDelete 等
/// 接口供 Avalonia VM 消费。Update 走 Delete+Add（FileModuleDataStore 无原子 Update）。
/// </summary>
public sealed class ModuleDataAdapter<TCategory, TData>
    where TCategory : class, ICategory
    where TData : class, IDataItem
{
    private readonly FileModuleDataStore<TCategory, TData> _store;

    public IModuleSchema<TCategory, TData> Schema { get; }

    public ModuleDataAdapter(IModuleSchema<TCategory, TData> schema, string projectRoot)
    {
        Schema = schema ?? throw new System.ArgumentNullException(nameof(schema));
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new System.ArgumentException("项目根目录不能为空", nameof(projectRoot));

        var moduleDir = System.IO.Path.Combine(projectRoot, schema.ModuleRelativePath);
        _store = new FileModuleDataStore<TCategory, TData>(
            moduleDirectory: moduleDir,
            categoriesFileName: "categories.json",
            builtInCategoriesFileName: "built_in_categories.json",
            dataFileName: "data.json");
    }

    public Task LoadAsync() => _store.LoadAsync();

    public IReadOnlyList<TCategory> GetCategories() => _store.GetCategories();

    public IReadOnlyList<TData> GetData() => _store.GetData();

    public Task<bool> AddCategoryAsync(TCategory category) => _store.AddCategoryAsync(category);

    public Task AddAsync(TData data) => _store.AddDataAsync(data);

    public Task<bool> DeleteAsync(string dataId) => _store.DeleteDataAsync(dataId);

    public Task<CascadeDeleteResult> CascadeDeleteCategoryAsync(string categoryName)
        => _store.CascadeDeleteCategoryAsync(categoryName);

    /// <summary>Update = Delete by Id + Add（保留原 Id）。</summary>
    public async Task UpdateAsync(TData data)
    {
        if (data == null) throw new System.ArgumentNullException(nameof(data));
        await _store.DeleteDataAsync(data.Id).ConfigureAwait(false);
        await _store.AddDataAsync(data).ConfigureAwait(false);
    }

    /// <summary>按 category.Name 过滤。FileModuleDataStore 数据项的 Category 字段存 name。</summary>
    public IEnumerable<TData> GetDataForCategory(TCategory category)
    {
        if (category == null) throw new System.ArgumentNullException(nameof(category));
        return _store.GetData().Where(d => d.Category == category.Name);
    }
}
