using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Publishing;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class FilePackagePublisher
    {
        private readonly string _rootDirectory;
        private readonly FilePackageBuilder _builder;
        private readonly FilePackageManifestStore _manifestStore;
        private readonly FilePackageStatisticsBuilder _statisticsBuilder;

        public FilePackagePublisher(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("项目目录不能为空", nameof(rootDirectory));

            _rootDirectory = rootDirectory;
            var publishedRoot = Path.Combine(rootDirectory, "Published");
            _builder = new FilePackageBuilder(rootDirectory, publishedRoot);
            _manifestStore = new FilePackageManifestStore(rootDirectory);
            _statisticsBuilder = new FilePackageStatisticsBuilder(
                Path.Combine(rootDirectory, "Generated"),
                publishedRoot);
        }

        public async Task<PublishResult> PublishDefaultAsync(PackagePublishRequest request)
        {
            return await PublishAsync(
                request,
                PackageModuleMappingCatalog.GetDefaultMappings(request.EnabledModulePaths),
                preserveCurrentFiles: false).ConfigureAwait(false);
        }

        public async Task<PublishResult> PublishModuleAsync(string moduleType, PackagePublishRequest request)
        {
            if (string.IsNullOrWhiteSpace(moduleType))
                throw new ArgumentException("模块类型不能为空", nameof(moduleType));

            var mappings = PackageModuleMappingCatalog
                .GetDefaultMappings(request.EnabledModulePaths)
                .Where(mapping => string.Equals(mapping.ModuleType, moduleType, StringComparison.Ordinal))
                .ToList();

            return await PublishAsync(request, mappings, preserveCurrentFiles: true).ConfigureAwait(false);
        }

        private async Task<PublishResult> PublishAsync(
            PackagePublishRequest request,
            IReadOnlyList<PackageModuleMapping> mappings,
            bool preserveCurrentFiles)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            if (string.IsNullOrWhiteSpace(request.SourceBookId))
                return PublishResult.Failed("Scope为空，禁止打包。请先选择来源拆书。");

            var missingData = GetMissingDataPaths(mappings).ToList();
            if (missingData.Count > 0)
                return PublishResult.Failed($"以下业务尚未构建数据，无法打包：{string.Join("、", missingData)}。请先完成构建后重新打包。");

            var currentManifest = await _manifestStore.GetManifestAsync().ConfigureAwait(false);
            if (currentManifest != null)
                await _manifestStore.SaveCurrentToHistoryAsync().ConfigureAwait(false);

            var files = await _builder.PackageModulesAsync(mappings, request.SourceBookId).ConfigureAwait(false);
            if (preserveCurrentFiles && currentManifest != null)
                files = MergeFiles(currentManifest.Files, files);

            var manifest = new ManifestInfo
            {
                ProjectName = string.IsNullOrWhiteSpace(request.ProjectName) ? "我的小说" : request.ProjectName,
                SourceBookId = request.SourceBookId,
                PublishTime = DateTime.Now,
                Version = (currentManifest?.Version ?? 0) + 1,
                Files = files,
                EnabledModules = BuildEnabledModulesMap(request.EnabledModulePaths),
                Statistics = await _statisticsBuilder.BuildStatisticsAsync().ConfigureAwait(false)
            };

            await _manifestStore.SaveManifestAsync(manifest).ConfigureAwait(false);

            var packagedModules = mappings.Select(mapping => $"{mapping.ModuleType}/{mapping.SubModule}").ToList();
            return PublishResult.Success(manifest.Version, packagedModules);
        }

        private static Dictionary<string, List<string>> MergeFiles(
            Dictionary<string, List<string>> currentFiles,
            Dictionary<string, List<string>> packagedFiles)
        {
            var merged = currentFiles.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.ToList(),
                StringComparer.Ordinal);

            foreach (var (moduleType, files) in packagedFiles)
                merged[moduleType] = files;

            return merged;
        }

        private IEnumerable<string> GetMissingDataPaths(IReadOnlyList<PackageModuleMapping> mappings)
        {
            foreach (var mapping in mappings)
            {
                var sourceBasePath = Path.Combine(_rootDirectory, "Modules", mapping.ModuleType, mapping.SubModule);
                foreach (var subDirectory in mapping.SubDirectories)
                {
                    var path = Path.Combine(sourceBasePath, subDirectory);
                    if (!Directory.Exists(path) || Directory.GetFiles(path, "*.json", SearchOption.TopDirectoryOnly).Length == 0)
                        yield return $"{mapping.ModuleType}/{mapping.SubModule}/{subDirectory}";
                }
            }
        }

        private static Dictionary<string, Dictionary<string, bool>> BuildEnabledModulesMap(
            IReadOnlyDictionary<string, bool> enabledModulePaths)
        {
            var enabled = new Dictionary<string, Dictionary<string, bool>>(StringComparer.Ordinal);
            foreach (var mapping in PackageModuleMappingCatalog.GetDefaultMappings())
            {
                if (!enabled.TryGetValue(mapping.ModuleType, out var moduleMap))
                {
                    moduleMap = new Dictionary<string, bool>(StringComparer.Ordinal);
                    enabled[mapping.ModuleType] = moduleMap;
                }

                var modulePath = $"{mapping.ModuleType}/{mapping.SubModule}";
                moduleMap[mapping.SubModule] = !enabledModulePaths.TryGetValue(modulePath, out var isEnabled) || isEnabled;
            }

            return enabled;
        }
    }

    public sealed class PackagePublishRequest
    {
        public string ProjectName { get; set; } = string.Empty;
        public string SourceBookId { get; set; } = string.Empty;
        public Dictionary<string, bool> EnabledModulePaths { get; } = new(StringComparer.Ordinal);
    }
}
