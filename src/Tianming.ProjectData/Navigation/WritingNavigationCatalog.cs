using System;
using System.Collections.Generic;
using System.Linq;
using TM.Services.Modules.ProjectData.Models.Navigation;

namespace TM.Services.Modules.ProjectData.Navigation
{
    public static class WritingNavigationCatalog
    {
        private static readonly ModuleInfo[] Modules =
        [
            Create("Design", "SmartParsing", "智能拆书", "BookAnalysis", "拆书分析", "BookAnalysis"),
            Create("Design", "Templates", "创作模板", "CreativeMaterials", "模板管理", "CreativeMaterials"),
            Create("Design", "GlobalSettings", "全局设定", "WorldRules", "世界观规则", "WorldRules"),
            Create("Design", "Elements", "设计元素", "CharacterRules", "角色规则", "CharacterRules"),
            Create("Design", "Elements", "设计元素", "FactionRules", "势力规则", "FactionRules"),
            Create("Design", "Elements", "设计元素", "LocationRules", "位置规则", "LocationRules"),
            Create("Design", "Elements", "设计元素", "PlotRules", "剧情规则", "PlotRules"),
            Create("Generate", "GlobalSettings", "全书设定", "Outline", "大纲设计", "Outline"),
            Create("Generate", "Elements", "创作元素", "VolumeDesign", "分卷设计", "VolumeDesign"),
            Create("Generate", "Elements", "创作元素", "Chapter", "章节设计", "Chapter"),
            Create("Generate", "Elements", "创作元素", "Blueprint", "蓝图设计", "Blueprint"),
            Create("Generate", "Content", "正文配置", "Content", "数据中心", "Content"),
            Create("Generate", "Content", "正文配置", "ChapterPreview", "章节预览", "ChapterPreview"),
            Create("Validate", "ValidationSummary", "校验汇总", "ValidationResult", "校验结果", "ValidationResult"),
            Create("Validate", "ValidationIntro", "校验介绍", "WorldviewIntro", "世界观校验", "WorldviewIntro"),
            Create("Validate", "ValidationIntro", "校验介绍", "CharacterIntro", "角色校验", "CharacterIntro"),
            Create("Validate", "ValidationIntro", "校验介绍", "PlotIntro", "剧情校验", "PlotIntro"),
            Create("Validate", "ValidationIntro", "校验介绍", "OutlineIntro", "大纲校验", "OutlineIntro"),
            Create("Validate", "ValidationIntro", "校验介绍", "ChapterIntro", "章节校验", "ChapterIntro"),
            Create("Validate", "ValidationIntro", "校验介绍", "ContentIntro", "正文校验", "ContentIntro")
        ];

        public static IReadOnlyList<ModuleInfo> GetAllModules()
        {
            return Modules.Select(Clone).ToList();
        }

        public static IReadOnlyList<ModuleInfo> GetModulesByType(string moduleType)
        {
            return Modules
                .Where(module => module.ModuleType.Equals(moduleType, StringComparison.OrdinalIgnoreCase))
                .Select(Clone)
                .ToList();
        }

        public static IReadOnlyList<ModuleInfo> GetFunctionsBySubModule(string moduleType, string subModule)
        {
            return Modules
                .Where(module =>
                    module.ModuleType.Equals(moduleType, StringComparison.OrdinalIgnoreCase)
                    && module.SubModule.Equals(subModule, StringComparison.OrdinalIgnoreCase))
                .Select(Clone)
                .ToList();
        }

        public static IReadOnlyList<(string SubModule, string DisplayName)> GetSubModules(string moduleType)
        {
            return Modules
                .Where(module => module.ModuleType.Equals(moduleType, StringComparison.OrdinalIgnoreCase))
                .GroupBy(module => module.SubModule)
                .Select(group => (group.Key, group.First().SubModuleDisplayName))
                .ToList();
        }

        public static string GetDisplayName(string functionName)
        {
            return Modules
                .FirstOrDefault(module => module.FunctionName.Equals(functionName, StringComparison.OrdinalIgnoreCase))
                ?.DisplayName ?? functionName;
        }

        public static string GetSubModuleDisplayName(string subModule)
        {
            return Modules
                .FirstOrDefault(module => module.SubModule.Equals(subModule, StringComparison.OrdinalIgnoreCase))
                ?.SubModuleDisplayName ?? subModule;
        }

        public static string GetStoragePath(string functionName)
        {
            return Modules
                .FirstOrDefault(module => module.FunctionName.Equals(functionName, StringComparison.OrdinalIgnoreCase))
                ?.StoragePath ?? string.Empty;
        }

        private static ModuleInfo Create(
            string moduleType,
            string subModule,
            string subModuleDisplayName,
            string functionName,
            string displayName,
            string route)
        {
            return new ModuleInfo
            {
                ModuleType = moduleType,
                SubModule = subModule,
                SubModuleDisplayName = subModuleDisplayName,
                FunctionName = functionName,
                DisplayName = displayName,
                Icon = string.Empty,
                StoragePath = $"Modules/{moduleType}/{subModule}/{functionName}",
                ViewPath = $"tianming://writing/{moduleType}/{subModule}/{route}"
            };
        }

        private static ModuleInfo Clone(ModuleInfo module)
        {
            return new ModuleInfo
            {
                ModuleType = module.ModuleType,
                SubModule = module.SubModule,
                SubModuleDisplayName = module.SubModuleDisplayName,
                FunctionName = module.FunctionName,
                DisplayName = module.DisplayName,
                Icon = module.Icon,
                StoragePath = module.StoragePath,
                ViewPath = module.ViewPath
            };
        }
    }
}
