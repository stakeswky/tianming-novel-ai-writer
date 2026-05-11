using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TM.Framework.Common.Helpers.Id;
using TM.Framework.Common.Models;

namespace TM.Services.Modules.ProjectData.Modules
{
    public sealed class FileModuleDataStore<TCategory, TData>
        where TCategory : class, ICategory
        where TData : class, IDataItem
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private readonly string _categoriesPath;
        private readonly string _builtInCategoriesPath;
        private readonly string _dataPath;
        private List<TCategory> _categories = [];
        private List<TData> _data = [];

        public FileModuleDataStore(
            string moduleDirectory,
            string categoriesFileName,
            string builtInCategoriesFileName,
            string dataFileName)
        {
            if (string.IsNullOrWhiteSpace(moduleDirectory))
                throw new ArgumentException("模块目录不能为空", nameof(moduleDirectory));

            _categoriesPath = Path.Combine(moduleDirectory, categoriesFileName);
            _builtInCategoriesPath = Path.Combine(moduleDirectory, builtInCategoriesFileName);
            _dataPath = Path.Combine(moduleDirectory, dataFileName);
        }

        public async Task LoadAsync()
        {
            var userCategories = await ReadListAsync<TCategory>(_categoriesPath).ConfigureAwait(false);
            var builtInCategories = await ReadListAsync<TCategory>(_builtInCategoriesPath).ConfigureAwait(false);
            foreach (var category in builtInCategories)
                category.IsBuiltIn = true;

            _categories = MergeCategories(userCategories, builtInCategories);
            _data = await ReadListAsync<TData>(_dataPath).ConfigureAwait(false);

            var userCategoriesUpdated = EnsureCategoryIds(_categories.Where(category => !category.IsBuiltIn));
            var builtInCategoriesUpdated = EnsureCategoryIds(_categories.Where(category => category.IsBuiltIn));
            var dataUpdated = EnsureDataIdentifiers();

            if (userCategoriesUpdated)
                await SaveCategoriesAsync().ConfigureAwait(false);
            if (builtInCategoriesUpdated)
                await SaveBuiltInCategoriesAsync().ConfigureAwait(false);
            if (dataUpdated)
                await SaveDataAsync().ConfigureAwait(false);
        }

        public IReadOnlyList<TCategory> GetCategories()
        {
            return _categories.ToList();
        }

        public IReadOnlyList<TData> GetData()
        {
            return _data.ToList();
        }

        public async Task<bool> AddCategoryAsync(TCategory category)
        {
            if (category == null || !IsCategoryNameAvailable(category.Name))
                return false;

            EnsureCategoryId(category);
            _categories.Add(category);
            await SaveCategoriesAsync().ConfigureAwait(false);
            return true;
        }

        public async Task AddDataAsync(TData data)
        {
            if (data == null)
                return;

            EnsureDataId(data);
            EnsureDataCategoryId(data);
            _data.Add(data);
            await SaveDataAsync().ConfigureAwait(false);
        }

        public async Task<bool> DeleteDataAsync(string dataId)
        {
            var removed = _data.RemoveAll(item => string.Equals(item.Id, dataId, StringComparison.Ordinal)) > 0;
            if (removed)
                await SaveDataAsync().ConfigureAwait(false);
            return removed;
        }

        public async Task<CascadeDeleteResult> CascadeDeleteCategoryAsync(string categoryName)
        {
            var root = _categories.FirstOrDefault(category => string.Equals(category.Name, categoryName, StringComparison.Ordinal));
            if (root == null)
                return new CascadeDeleteResult(0, 0);

            if (root.IsBuiltIn)
            {
                var directDataDeleted = _data.RemoveAll(item => string.Equals(item.Category, categoryName, StringComparison.Ordinal));
                if (directDataDeleted > 0)
                    await SaveDataAsync().ConfigureAwait(false);
                return new CascadeDeleteResult(0, directDataDeleted);
            }

            var names = CollectCategoryTree(categoryName);
            var nameSet = names.ToHashSet(StringComparer.Ordinal);
            var dataDeleted = _data.RemoveAll(item => nameSet.Contains(item.Category));
            var categoriesDeleted = _categories.RemoveAll(category => !category.IsBuiltIn && nameSet.Contains(category.Name));

            if (categoriesDeleted > 0)
                await SaveCategoriesAsync().ConfigureAwait(false);
            if (dataDeleted > 0)
                await SaveDataAsync().ConfigureAwait(false);

            return new CascadeDeleteResult(categoriesDeleted, dataDeleted);
        }

        private static async Task<List<T>> ReadListAsync<T>(string path)
        {
            if (!File.Exists(path))
                return [];

            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? [];
        }

        private static List<TCategory> MergeCategories(List<TCategory> userCategories, List<TCategory> builtInCategories)
        {
            var result = new List<TCategory>();
            var userNames = userCategories.Select(category => category.Name).ToHashSet(StringComparer.Ordinal);
            result.AddRange(userCategories);
            result.AddRange(builtInCategories.Where(category => !userNames.Contains(category.Name)));
            return result.OrderBy(category => category.Order).ToList();
        }

        private bool EnsureCategoryIds(IEnumerable<TCategory> categories)
        {
            var updated = false;
            foreach (var category in categories)
            {
                if (!EnsureCategoryId(category))
                    continue;
                updated = true;
            }

            return updated;
        }

        private bool EnsureCategoryId(TCategory category)
        {
            if (!string.IsNullOrWhiteSpace(category.Id))
                return false;

            category.Id = category.IsBuiltIn
                ? ShortIdGenerator.NewDeterministic("C", category.Name)
                : ShortIdGenerator.New("C");
            return true;
        }

        private bool EnsureDataIdentifiers()
        {
            var updated = false;
            foreach (var item in _data)
            {
                if (EnsureDataId(item))
                    updated = true;
                if (EnsureDataCategoryId(item))
                    updated = true;
            }

            return updated;
        }

        private static bool EnsureDataId(TData data)
        {
            if (!string.IsNullOrWhiteSpace(data.Id))
                return false;

            data.Id = ShortIdGenerator.New("D");
            return true;
        }

        private bool EnsureDataCategoryId(TData data)
        {
            if (!string.IsNullOrWhiteSpace(data.CategoryId) || string.IsNullOrWhiteSpace(data.Category))
                return false;

            var category = _categories.FirstOrDefault(item => string.Equals(item.Name, data.Category, StringComparison.Ordinal));
            if (category == null || string.IsNullOrWhiteSpace(category.Id))
                return false;

            data.CategoryId = category.Id;
            return true;
        }

        private bool IsCategoryNameAvailable(string? name)
        {
            return !string.IsNullOrWhiteSpace(name)
                && _categories.All(category => !string.Equals(category.Name, name.Trim(), StringComparison.Ordinal));
        }

        private List<string> CollectCategoryTree(string categoryName)
        {
            var result = new List<string>();
            void Collect(string name)
            {
                result.Add(name);
                foreach (var child in _categories.Where(category => string.Equals(category.ParentCategory, name, StringComparison.Ordinal)))
                    Collect(child.Name);
            }

            Collect(categoryName);
            return result;
        }

        private Task SaveCategoriesAsync()
        {
            return WriteJsonAtomicAsync(_categoriesPath, _categories.Where(category => !category.IsBuiltIn).ToList());
        }

        private Task SaveBuiltInCategoriesAsync()
        {
            return WriteJsonAtomicAsync(_builtInCategoriesPath, _categories.Where(category => category.IsBuiltIn).ToList());
        }

        private Task SaveDataAsync()
        {
            return WriteJsonAtomicAsync(_dataPath, _data);
        }

        private static async Task WriteJsonAtomicAsync<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var json = JsonSerializer.Serialize(value, JsonOptions);
            await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
            File.Move(tempPath, path, overwrite: true);
        }
    }

    public readonly record struct CascadeDeleteResult(int CategoriesDeleted, int DataDeleted);
}
