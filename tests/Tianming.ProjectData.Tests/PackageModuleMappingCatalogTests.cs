using TM.Services.Modules.ProjectData.Implementations;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class PackageModuleMappingCatalogTests
{
    [Fact]
    public void GetDefaultMappings_returns_original_publish_mapping_surface()
    {
        var mappings = PackageModuleMappingCatalog.GetDefaultMappings();

        Assert.Collection(
            mappings,
            mapping =>
            {
                Assert.Equal("Design", mapping.ModuleType);
                Assert.Equal("GlobalSettings", mapping.SubModule);
                Assert.Equal(["WorldRules"], mapping.SubDirectories);
                Assert.Equal("globalsettings.json", mapping.TargetFile);
            },
            mapping =>
            {
                Assert.Equal("Design", mapping.ModuleType);
                Assert.Equal("Elements", mapping.SubModule);
                Assert.Equal(["CharacterRules", "FactionRules", "LocationRules", "PlotRules"], mapping.SubDirectories);
                Assert.Equal("elements.json", mapping.TargetFile);
            },
            mapping =>
            {
                Assert.Equal("Generate", mapping.ModuleType);
                Assert.Equal("GlobalSettings", mapping.SubModule);
                Assert.Equal(["Outline"], mapping.SubDirectories);
                Assert.Equal("globalsettings.json", mapping.TargetFile);
            },
            mapping =>
            {
                Assert.Equal("Generate", mapping.ModuleType);
                Assert.Equal("Elements", mapping.SubModule);
                Assert.Equal(["VolumeDesign", "Chapter", "Blueprint"], mapping.SubDirectories);
                Assert.Equal("elements.json", mapping.TargetFile);
            });
    }

    [Fact]
    public void GetDefaultMappings_excludes_disabled_module_paths_when_statuses_are_provided()
    {
        var mappings = PackageModuleMappingCatalog.GetDefaultMappings(
            new Dictionary<string, bool>
            {
                ["Design/GlobalSettings"] = true,
                ["Design/Elements"] = false,
                ["Generate/GlobalSettings"] = true,
                ["Generate/Elements"] = true
            });

        Assert.DoesNotContain(mappings, mapping => mapping.ModuleType == "Design" && mapping.SubModule == "Elements");
        Assert.Equal(3, mappings.Count);
    }
}
