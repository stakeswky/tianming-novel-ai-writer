using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Navigation;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class FileModuleEnabledStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly string _rootDirectory;

        public FileModuleEnabledStore(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("项目目录不能为空", nameof(rootDirectory));

            _rootDirectory = rootDirectory;
        }

        public async Task<int> SetModuleEnabledAsync(string moduleType, string subModule, bool enabled)
        {
            var updated = 0;
            foreach (var function in WritingNavigationCatalog.GetFunctionsBySubModule(moduleType, subModule))
                updated += await SetFunctionEnabledAsync(function.StoragePath, enabled).ConfigureAwait(false);
            return updated;
        }

        public async Task<ModuleEnabledStats> GetModuleEnabledStatsAsync(string moduleType, string subModule)
        {
            var enabled = 0;
            var total = 0;
            foreach (var function in WritingNavigationCatalog.GetFunctionsBySubModule(moduleType, subModule))
            {
                var stats = await GetFunctionEnabledStatsAsync(function.StoragePath).ConfigureAwait(false);
                enabled += stats.EnabledCount;
                total += stats.TotalCount;
            }

            return new ModuleEnabledStats(enabled, total);
        }

        public async Task<Dictionary<string, bool>> GetEnabledModulePathsAsync()
        {
            var result = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var mapping in PackageModuleMappingCatalog.GetDefaultMappings())
            {
                var stats = await GetModuleEnabledStatsAsync(mapping.ModuleType, mapping.SubModule).ConfigureAwait(false);
                result[$"{mapping.ModuleType}/{mapping.SubModule}"] = stats.TotalCount == 0 || stats.EnabledCount > 0;
            }

            return result;
        }

        private async Task<int> SetFunctionEnabledAsync(string storagePath, bool enabled)
        {
            var directory = ResolveStorageDirectory(storagePath);
            if (!Directory.Exists(directory))
                return 0;

            var updated = 0;
            foreach (var file in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetFileName(file), "categories.json", StringComparison.OrdinalIgnoreCase))
                    continue;
                updated += await UpdateJsonArrayEnabledAsync(file, enabled).ConfigureAwait(false);
            }

            return updated;
        }

        private async Task<int> UpdateJsonArrayEnabledAsync(string filePath, bool enabled)
        {
            var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
            if (JsonNode.Parse(json) is not JsonArray array || array.Count == 0)
                return 0;

            var updated = 0;
            foreach (var item in array)
            {
                if (item is not JsonObject obj)
                    continue;

                obj["IsEnabled"] = enabled;
                updated++;
            }

            if (updated == 0)
                return 0;

            var tempPath = filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, array.ToJsonString(JsonOptions)).ConfigureAwait(false);
            File.Move(tempPath, filePath, overwrite: true);
            return updated;
        }

        private async Task<ModuleEnabledStats> GetFunctionEnabledStatsAsync(string storagePath)
        {
            var directory = ResolveStorageDirectory(storagePath);
            if (!Directory.Exists(directory))
                return new ModuleEnabledStats(0, 0);

            var enabled = 0;
            var total = 0;
            foreach (var file in Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
            {
                if (string.Equals(Path.GetFileName(file), "categories.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                var stats = await CountEnabledAsync(file).ConfigureAwait(false);
                enabled += stats.EnabledCount;
                total += stats.TotalCount;
            }

            return new ModuleEnabledStats(enabled, total);
        }

        private static async Task<ModuleEnabledStats> CountEnabledAsync(string filePath)
        {
            var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
            if (JsonNode.Parse(json) is not JsonArray array)
                return new ModuleEnabledStats(0, 0);

            var enabled = 0;
            foreach (var item in array)
            {
                if (item is JsonObject obj
                    && obj["IsEnabled"] is JsonValue enabledValue
                    && enabledValue.TryGetValue<bool>(out var isEnabled)
                    && isEnabled)
                    enabled++;
            }

            return new ModuleEnabledStats(enabled, array.Count);
        }

        private string ResolveStorageDirectory(string storagePath)
        {
            var relative = storagePath.Trim().TrimStart('/', '\\').Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(_rootDirectory, relative));
        }
    }

    public readonly record struct ModuleEnabledStats(int EnabledCount, int TotalCount);
}
