using System.Collections.Generic;
using System.Linq;
using TM.Services.Modules.ProjectData.Navigation;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public static class PackageModuleMappingCatalog
    {
        private static readonly Dictionary<string, HashSet<string>> PackageSubModuleAllowlist = new()
        {
            ["Design"] = new HashSet<string>(["全局设定", "设计元素"], System.StringComparer.OrdinalIgnoreCase),
            ["Generate"] = new HashSet<string>(["全书设定", "创作元素"], System.StringComparer.OrdinalIgnoreCase)
        };

        public static IReadOnlyList<PackageModuleMapping> GetDefaultMappings(
            IReadOnlyDictionary<string, bool>? enabledModulePaths = null)
        {
            var defaultMappings = BuildDefaultMappings();
            if (enabledModulePaths is null)
                return defaultMappings;

            return defaultMappings
                .Where(mapping => IsEnabled(mapping, enabledModulePaths))
                .ToList();
        }

        private static List<PackageModuleMapping> BuildDefaultMappings()
        {
            var mappings = new List<PackageModuleMapping>();
            foreach (var (moduleType, subModuleAllowlist) in PackageSubModuleAllowlist)
            {
                foreach (var (subModule, displayName) in WritingNavigationCatalog.GetSubModules(moduleType))
                {
                    if (!subModuleAllowlist.Contains(displayName))
                        continue;

                    var functions = WritingNavigationCatalog.GetFunctionsBySubModule(moduleType, subModule);
                    if (functions.Count == 0)
                        continue;

                    mappings.Add(new PackageModuleMapping(
                        moduleType,
                        subModule,
                        functions.Select(function => function.FunctionName).ToArray(),
                        $"{subModule.ToLowerInvariant()}.json"));
                }
            }

            return mappings;
        }

        private static bool IsEnabled(
            PackageModuleMapping mapping,
            IReadOnlyDictionary<string, bool> enabledModulePaths)
        {
            var modulePath = $"{mapping.ModuleType}/{mapping.SubModule}";
            return !enabledModulePaths.TryGetValue(modulePath, out var enabled) || enabled;
        }
    }
}
