using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class FilePackageBuilder
    {
        private readonly string _storageRoot;
        private readonly string _publishedRoot;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public FilePackageBuilder(string storageRoot, string publishedRoot)
        {
            if (string.IsNullOrWhiteSpace(storageRoot))
                throw new ArgumentException("模块数据根目录不能为空", nameof(storageRoot));
            if (string.IsNullOrWhiteSpace(publishedRoot))
                throw new ArgumentException("发布目录不能为空", nameof(publishedRoot));

            _storageRoot = storageRoot;
            _publishedRoot = publishedRoot;
            Directory.CreateDirectory(_publishedRoot);
        }

        public async Task<Dictionary<string, List<string>>> PackageModulesAsync(
            IReadOnlyList<PackageModuleMapping> mappings,
            string? sourceBookId)
        {
            var files = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var mapping in mappings)
            {
                await PackageModuleAsync(mapping, sourceBookId).ConfigureAwait(false);
                if (!files.TryGetValue(mapping.ModuleType, out var moduleFiles))
                {
                    moduleFiles = new List<string>();
                    files[mapping.ModuleType] = moduleFiles;
                }
                moduleFiles.Add(mapping.TargetFile);
            }
            return files;
        }

        public Task<Dictionary<string, List<string>>> PackageDefaultModulesAsync(
            string? sourceBookId,
            IReadOnlyDictionary<string, bool>? enabledModulePaths = null)
        {
            return PackageModulesAsync(
                PackageModuleMappingCatalog.GetDefaultMappings(enabledModulePaths),
                sourceBookId);
        }

        public async Task PackageModuleAsync(PackageModuleMapping mapping, string? sourceBookId)
        {
            var sourceBasePath = Path.Combine(_storageRoot, "Modules", mapping.ModuleType, mapping.SubModule);
            var packageData = new Dictionary<string, object?>
            {
                ["module"] = mapping.SubModule,
                ["publishTime"] = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
                ["version"] = 1
            };
            var data = new Dictionary<string, object?>(StringComparer.Ordinal);

            foreach (var subDirectory in mapping.SubDirectories)
            {
                var subDirectoryPath = Path.Combine(sourceBasePath, subDirectory);
                if (!Directory.Exists(subDirectoryPath))
                    continue;

                data[subDirectory.ToLowerInvariant()] = await LoadSubDirectoryDataAsync(subDirectoryPath, sourceBookId)
                    .ConfigureAwait(false);
            }

            packageData["data"] = data;

            var targetPath = Path.Combine(_publishedRoot, mapping.ModuleType, mapping.TargetFile);
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            var tempPath = targetPath + ".tmp";
            var json = JsonSerializer.Serialize(packageData, _jsonOptions);
            await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
            File.Move(tempPath, targetPath, overwrite: true);
        }

        private async Task<Dictionary<string, object?>> LoadSubDirectoryDataAsync(string directoryPath, string? sourceBookId)
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            foreach (var file in Directory.GetFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                var json = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                var node = JsonNode.Parse(json);

                if (node is JsonArray array)
                    result[fileName] = FilterArray(array, sourceBookId);
                else
                    result[fileName] = JsonSerializer.Deserialize<object>(json, _jsonOptions);
            }
            return result;
        }

        private static JsonArray FilterArray(JsonArray array, string? sourceBookId)
        {
            var filtered = new JsonArray();
            foreach (var item in array)
            {
                if (item is not JsonObject obj)
                {
                    filtered.Add(item?.DeepClone());
                    continue;
                }

                if (obj["IsEnabled"] is JsonValue enabledValue &&
                    enabledValue.TryGetValue<bool>(out var isEnabled) &&
                    !isEnabled)
                    continue;

                if (!string.IsNullOrEmpty(sourceBookId))
                {
                    if (obj["SourceBookId"] is not JsonValue sourceBookValue ||
                        !sourceBookValue.TryGetValue<string>(out var itemSourceBookId) ||
                        !string.Equals(itemSourceBookId, sourceBookId, StringComparison.Ordinal))
                        continue;
                }

                filtered.Add(obj.DeepClone());
            }
            return filtered;
        }
    }

    public sealed class PackageModuleMapping
    {
        public PackageModuleMapping(string moduleType, string subModule, IReadOnlyList<string> subDirectories, string targetFile)
        {
            ModuleType = moduleType;
            SubModule = subModule;
            SubDirectories = subDirectories;
            TargetFile = targetFile;
        }

        public string ModuleType { get; }
        public string SubModule { get; }
        public IReadOnlyList<string> SubDirectories { get; }
        public string TargetFile { get; }
    }
}
