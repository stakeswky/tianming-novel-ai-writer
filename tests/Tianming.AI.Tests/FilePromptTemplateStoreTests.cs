using TM.Modules.AIAssistant.PromptTools.PromptManagement.Models;
using TM.Modules.AIAssistant.PromptTools.PromptManagement.Services;
using Xunit;

namespace Tianming.AI.Tests;

public class FilePromptTemplateStoreTests
{
    [Fact]
    public void Loads_user_templates_and_merges_built_in_templates_with_user_override()
    {
        using var workspace = new TempDirectory();
        var categoriesPath = Path.Combine(workspace.Path, "categories.json");
        var userTemplatesPath = Path.Combine(workspace.Path, "templates");
        var builtInTemplatesPath = Path.Combine(workspace.Path, "built_in_templates");
        Directory.CreateDirectory(userTemplatesPath);
        Directory.CreateDirectory(builtInTemplatesPath);

        File.WriteAllText(categoriesPath, """
        [
          { "Id": "cat-root", "Name": "业务提示词", "Icon": "P", "Level": 1, "Order": 1, "IsEnabled": true }
        ]
        """);
        File.WriteAllText(Path.Combine(userTemplatesPath, "business.json"), """
        [
          { "Id": "user-1", "Name": "小说初稿生成器", "Category": "业务提示词", "SystemPrompt": "user prompt", "IsEnabled": true },
          { "Id": "user-2", "Name": "用户自定义", "Category": "业务提示词", "SystemPrompt": "custom", "IsEnabled": true }
        ]
        """);
        File.WriteAllText(Path.Combine(builtInTemplatesPath, "defaults.json"), """
        [
          { "Id": "built-in-1", "Name": "小说初稿生成器", "Category": "业务提示词", "SystemPrompt": "built in", "IsEnabled": true },
          { "Id": "built-in-2", "Name": "对话助手", "Category": "业务提示词", "SystemPrompt": "dialog", "IsEnabled": true }
        ]
        """);

        var store = new FilePromptTemplateStore(categoriesPath, userTemplatesPath, builtInTemplatesPath);

        Assert.Equal(["user-1", "user-2", "built-in-2"], store.GetAllTemplates().Select(template => template.Id).ToArray());
        Assert.False(store.GetTemplateById("user-1")?.IsBuiltIn);
        Assert.True(store.GetTemplateById("built-in-2")?.IsBuiltIn);
        Assert.Equal("user prompt", store.GetTemplateById("user-1")?.SystemPrompt);
        Assert.True(store.IsBuiltInTemplate("built-in-2"));
        Assert.False(store.IsBuiltInTemplate("user-1"));
    }

    [Fact]
    public void Add_update_and_delete_user_template_persists_changes()
    {
        using var workspace = new TempDirectory();
        var store = new FilePromptTemplateStore(
            Path.Combine(workspace.Path, "categories.json"),
            Path.Combine(workspace.Path, "templates"),
            Path.Combine(workspace.Path, "built_in_templates"));
        var changes = 0;
        store.TemplatesChanged += (_, _) => changes++;

        store.AddTemplate(new PromptTemplateData
        {
            Id = "custom",
            Name = "自定义模板",
            Category = "业务提示词",
            CategoryId = "cat-business",
            SystemPrompt = "first",
            IsEnabled = true
        });
        store.UpdateTemplate(new PromptTemplateData
        {
            Id = "custom",
            Name = "自定义模板",
            Category = "业务提示词",
            CategoryId = "cat-business",
            SystemPrompt = "updated",
            IsEnabled = true
        });

        var reloaded = new FilePromptTemplateStore(
            Path.Combine(workspace.Path, "categories.json"),
            Path.Combine(workspace.Path, "templates"),
            Path.Combine(workspace.Path, "built_in_templates"));

        Assert.Equal(2, changes);
        Assert.Equal("updated", reloaded.GetTemplateById("custom")?.SystemPrompt);

        reloaded.DeleteTemplate("custom");
        var afterDelete = new FilePromptTemplateStore(
            Path.Combine(workspace.Path, "categories.json"),
            Path.Combine(workspace.Path, "templates"),
            Path.Combine(workspace.Path, "built_in_templates"));

        Assert.Null(afterDelete.GetTemplateById("custom"));
    }

    [Fact]
    public void Built_in_templates_cannot_be_updated_deleted_or_cleared()
    {
        using var workspace = new TempDirectory();
        var builtInTemplatesPath = Path.Combine(workspace.Path, "built_in_templates");
        Directory.CreateDirectory(builtInTemplatesPath);
        File.WriteAllText(Path.Combine(builtInTemplatesPath, "defaults.json"), """
        [
          { "Id": "built-in-1", "Name": "对话助手", "Category": "业务提示词", "SystemPrompt": "dialog", "IsEnabled": true }
        ]
        """);

        var store = new FilePromptTemplateStore(
            Path.Combine(workspace.Path, "categories.json"),
            Path.Combine(workspace.Path, "templates"),
            builtInTemplatesPath);

        store.UpdateTemplate(new PromptTemplateData
        {
            Id = "built-in-1",
            Name = "对话助手",
            Category = "业务提示词",
            SystemPrompt = "changed"
        });
        store.DeleteTemplate("built-in-1");
        var removed = store.ClearAllTemplates();

        Assert.Equal(0, removed);
        Assert.Equal("dialog", store.GetTemplateById("built-in-1")?.SystemPrompt);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-prompt-store-{Guid.NewGuid():N}");

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
