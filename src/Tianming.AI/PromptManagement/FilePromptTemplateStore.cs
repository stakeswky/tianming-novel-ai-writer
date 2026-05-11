using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TM.Framework.Common.Helpers.Id;
using TM.Modules.AIAssistant.PromptTools.PromptManagement.Models;
using TM.Services.Framework.AI.Interfaces.Prompts;

namespace TM.Modules.AIAssistant.PromptTools.PromptManagement.Services;

public sealed class FilePromptTemplateStore : IPromptRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _categoriesPath;
    private readonly string _userTemplatesPath;
    private readonly string _builtInTemplatesPath;
    private readonly object _lock = new();
    private readonly HashSet<string> _builtInTemplateIds = new(StringComparer.Ordinal);
    private List<PromptCategory> _categories = new();
    private List<PromptTemplateData> _templates = new();

    public FilePromptTemplateStore(string categoriesPath, string userTemplatesPath, string builtInTemplatesPath)
    {
        if (string.IsNullOrWhiteSpace(categoriesPath))
            throw new ArgumentException("提示词分类文件路径不能为空", nameof(categoriesPath));
        if (string.IsNullOrWhiteSpace(userTemplatesPath))
            throw new ArgumentException("用户提示词目录不能为空", nameof(userTemplatesPath));
        if (string.IsNullOrWhiteSpace(builtInTemplatesPath))
            throw new ArgumentException("内置提示词目录不能为空", nameof(builtInTemplatesPath));

        _categoriesPath = categoriesPath;
        _userTemplatesPath = userTemplatesPath;
        _builtInTemplatesPath = builtInTemplatesPath;

        Reload();
    }

    public event EventHandler? TemplatesChanged;

    public void Reload()
    {
        var categories = LoadList<PromptCategory>(_categoriesPath)
            .OrderBy(category => category.Order)
            .ThenBy(category => category.Name, StringComparer.Ordinal)
            .ToList();
        var userTemplates = LoadTemplatesFromDirectory(_userTemplatesPath, isBuiltIn: false);
        var builtInTemplates = LoadTemplatesFromDirectory(_builtInTemplatesPath, isBuiltIn: true);
        var userKeys = userTemplates
            .Select(TemplateOverrideKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        lock (_lock)
        {
            _categories = categories;
            _builtInTemplateIds.Clear();

            foreach (var builtIn in builtInTemplates)
                _builtInTemplateIds.Add(builtIn.Id);

            _templates = userTemplates
                .Concat(builtInTemplates.Where(template => !userKeys.Contains(TemplateOverrideKey(template))))
                .ToList();
        }
    }

    public IReadOnlyList<PromptTemplateData> GetAllTemplates()
    {
        lock (_lock)
        {
            return _templates.Select(CloneTemplate).ToList();
        }
    }

    public IReadOnlyList<PromptTemplateData> GetTemplatesByCategory(string categoryName)
    {
        lock (_lock)
        {
            return _templates
                .Where(template => string.Equals(template.Category, categoryName, StringComparison.Ordinal))
                .Select(CloneTemplate)
                .ToList();
        }
    }

    public PromptTemplateData? GetTemplateById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        lock (_lock)
        {
            var template = _templates.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
            return template == null ? null : CloneTemplate(template);
        }
    }

    public void AddTemplate(PromptTemplateData template)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));

        var next = CloneTemplate(template);
        if (string.IsNullOrWhiteSpace(next.Id))
            next.Id = ShortIdGenerator.New("D");

        next.IsBuiltIn = false;
        next.CreatedTime = DateTime.Now;
        next.ModifiedTime = DateTime.Now;
        FillCategoryId(next);

        lock (_lock)
        {
            _templates.RemoveAll(item => string.Equals(item.Id, next.Id, StringComparison.Ordinal));
            _templates.Add(next);
            SaveUserTemplates();
        }

        TemplatesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateTemplate(PromptTemplateData template)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));
        if (IsBuiltInTemplate(template.Id))
            return;

        var next = CloneTemplate(template);
        next.IsBuiltIn = false;
        next.ModifiedTime = DateTime.Now;
        FillCategoryId(next);

        var changed = false;
        lock (_lock)
        {
            var index = _templates.FindIndex(item => string.Equals(item.Id, next.Id, StringComparison.Ordinal));
            if (index >= 0)
            {
                next.CreatedTime = _templates[index].CreatedTime;
                _templates[index] = next;
                SaveUserTemplates();
                changed = true;
            }
        }

        if (changed)
            TemplatesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeleteTemplate(string id)
    {
        if (IsBuiltInTemplate(id))
            return;

        var changed = false;
        lock (_lock)
        {
            changed = _templates.RemoveAll(item => string.Equals(item.Id, id, StringComparison.Ordinal)) > 0;
            if (changed)
                SaveUserTemplates();
        }

        if (changed)
            TemplatesChanged?.Invoke(this, EventArgs.Empty);
    }

    public int ClearAllTemplates()
    {
        int removed;
        lock (_lock)
        {
            removed = _templates.RemoveAll(template => !IsBuiltInTemplateLocked(template.Id));
            if (removed > 0)
                SaveUserTemplates();
        }

        if (removed > 0)
            TemplatesChanged?.Invoke(this, EventArgs.Empty);

        return removed;
    }

    public IReadOnlyList<PromptCategory> GetAllCategories()
    {
        lock (_lock)
        {
            return _categories.Select(CloneCategory).ToList();
        }
    }

    public PromptCategory? GetCategoryByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        lock (_lock)
        {
            var category = _categories.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.Ordinal));
            return category == null ? null : CloneCategory(category);
        }
    }

    public bool IsBuiltInTemplate(string templateId)
    {
        if (string.IsNullOrWhiteSpace(templateId))
            return false;

        lock (_lock)
        {
            return IsBuiltInTemplateLocked(templateId);
        }
    }

    public bool IsTemplateOperationAllowed(string templateId) => !IsBuiltInTemplate(templateId);

    public bool IsBuiltInCategory(string categoryName)
    {
        var category = GetCategoryByName(categoryName);
        return category?.IsBuiltIn ?? false;
    }

    private bool IsBuiltInTemplateLocked(string templateId)
    {
        return _builtInTemplateIds.Contains(templateId)
            || (_templates.FirstOrDefault(template => string.Equals(template.Id, templateId, StringComparison.Ordinal))?.IsBuiltIn ?? false);
    }

    private void SaveUserTemplates()
    {
        Directory.CreateDirectory(_userTemplatesPath);
        foreach (var file in Directory.GetFiles(_userTemplatesPath, "*.json", SearchOption.AllDirectories))
            File.Delete(file);

        var userTemplates = _templates
            .Where(template => !IsBuiltInTemplateLocked(template.Id))
            .OrderBy(template => template.Category, StringComparer.Ordinal)
            .ThenBy(template => template.Name, StringComparer.Ordinal)
            .Select(CloneTemplate)
            .ToList();

        var path = Path.Combine(_userTemplatesPath, "templates.json");
        File.WriteAllText(path, JsonSerializer.Serialize(userTemplates, JsonOptions));
    }

    private void FillCategoryId(PromptTemplateData template)
    {
        if (!string.IsNullOrWhiteSpace(template.CategoryId) || string.IsNullOrWhiteSpace(template.Category))
            return;

        var category = _categories.FirstOrDefault(item => string.Equals(item.Name, template.Category, StringComparison.Ordinal));
        if (category != null)
            template.CategoryId = category.Id;
    }

    private static List<PromptTemplateData> LoadTemplatesFromDirectory(string path, bool isBuiltIn)
    {
        if (!Directory.Exists(path))
            return new List<PromptTemplateData>();

        var byId = new Dictionary<string, PromptTemplateData>(StringComparer.Ordinal);
        foreach (var file in Directory.GetFiles(path, "*.json", SearchOption.AllDirectories).OrderBy(File.GetLastWriteTimeUtc))
        {
            foreach (var template in LoadList<PromptTemplateData>(file))
            {
                if (string.IsNullOrWhiteSpace(template.Id))
                    continue;

                template.IsBuiltIn = isBuiltIn;
                byId[template.Id] = template;
            }
        }

        return byId.Values.ToList();
    }

    private static List<T> LoadList<T>(string path)
    {
        if (!File.Exists(path))
            return new List<T>();

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? new List<T>();
        }
        catch (JsonException)
        {
            return new List<T>();
        }
        catch (IOException)
        {
            return new List<T>();
        }
    }

    private static string TemplateOverrideKey(PromptTemplateData template)
    {
        return $"{template.Category}:{template.Name}";
    }

    private static PromptTemplateData CloneTemplate(PromptTemplateData template)
    {
        return new PromptTemplateData
        {
            Id = template.Id,
            Name = template.Name,
            Icon = template.Icon,
            Category = template.Category,
            CategoryId = template.CategoryId,
            IsEnabled = template.IsEnabled,
            CreatedTime = template.CreatedTime,
            ModifiedTime = template.ModifiedTime,
            SystemPrompt = template.SystemPrompt,
            UserTemplate = template.UserTemplate,
            Variables = template.Variables,
            Tags = template.Tags,
            Description = template.Description,
            IsBuiltIn = template.IsBuiltIn,
            IsDefault = template.IsDefault
        };
    }

    private static PromptCategory CloneCategory(PromptCategory category)
    {
        return new PromptCategory
        {
            Id = category.Id,
            Name = category.Name,
            Icon = category.Icon,
            ParentCategory = category.ParentCategory,
            Level = category.Level,
            Order = category.Order,
            IsEnabled = category.IsEnabled,
            IsBuiltIn = category.IsBuiltIn
        };
    }
}
