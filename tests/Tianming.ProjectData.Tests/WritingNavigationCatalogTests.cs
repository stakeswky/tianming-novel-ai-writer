using TM.Services.Modules.ProjectData.Navigation;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class WritingNavigationCatalogTests
{
    [Fact]
    public void GetAllModules_returns_writer_navigation_without_wpf_bindings()
    {
        var modules = WritingNavigationCatalog.GetAllModules();

        Assert.Equal(20, modules.Count);
        Assert.DoesNotContain(modules, module => module.ViewPath.Contains("typeof", StringComparison.Ordinal));
        Assert.Contains(modules, module =>
            module.ModuleType == "Design"
            && module.SubModule == "Elements"
            && module.FunctionName == "CharacterRules"
            && module.DisplayName == "角色规则"
            && module.StoragePath == "Modules/Design/Elements/CharacterRules");
        Assert.Contains(modules, module =>
            module.ModuleType == "Generate"
            && module.SubModule == "Content"
            && module.FunctionName == "ChapterPreview"
            && module.DisplayName == "章节预览");
        Assert.Contains(modules, module =>
            module.ModuleType == "Validate"
            && module.SubModule == "ValidationIntro"
            && module.FunctionName == "ContentIntro"
            && module.DisplayName == "正文校验");
    }

    [Fact]
    public void Query_helpers_match_original_navigation_lookup_semantics()
    {
        var designSubModules = WritingNavigationCatalog.GetSubModules("Design");
        var designElements = WritingNavigationCatalog.GetFunctionsBySubModule("Design", "Elements");

        Assert.Equal(
            ["SmartParsing", "Templates", "GlobalSettings", "Elements"],
            designSubModules.Select(item => item.SubModule).ToArray());
        Assert.Equal(["CharacterRules", "FactionRules", "LocationRules", "PlotRules"], designElements.Select(item => item.FunctionName).ToArray());
        Assert.Equal("设计元素", WritingNavigationCatalog.GetSubModuleDisplayName("Elements"));
        Assert.Equal("角色规则", WritingNavigationCatalog.GetDisplayName("CharacterRules"));
        Assert.Equal("Modules/Generate/Elements/Chapter", WritingNavigationCatalog.GetStoragePath("Chapter"));
    }
}
