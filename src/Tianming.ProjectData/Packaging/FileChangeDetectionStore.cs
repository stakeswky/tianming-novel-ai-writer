using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.ChangeDetection;
using TM.Services.Modules.ProjectData.Models.Publishing;
using TM.Services.Modules.ProjectData.Navigation;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class FileChangeDetectionStore
    {
        private readonly string _rootDirectory;
        private readonly FilePackageManifestStore _manifestStore;
        private DateTime? _lastPackageTime;

        public FileChangeDetectionStore(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("项目目录不能为空", nameof(rootDirectory));

            _rootDirectory = rootDirectory;
            _manifestStore = new FilePackageManifestStore(rootDirectory);
        }

        public async Task<IReadOnlyList<ChangeStatus>> GetAllStatusesAsync()
        {
            await LoadManifestTimeAsync().ConfigureAwait(false);
            return PackageModuleMappingCatalog
                .GetDefaultMappings()
                .Select(BuildModuleStatus)
                .ToList();
        }

        public async Task<IReadOnlyList<string>> GetChangedModulesAsync()
        {
            var statuses = await GetAllStatusesAsync().ConfigureAwait(false);
            return statuses
                .Where(status => status.Status is ChangeStatusType.Changed or ChangeStatusType.Never)
                .Select(status => status.ModulePath)
                .ToList();
        }

        public async Task MarkAllAsPackagedAsync(DateTime packagedAt)
        {
            var manifest = await _manifestStore.GetManifestAsync().ConfigureAwait(false) ?? new ManifestInfo
            {
                PublishTime = packagedAt,
                Version = 1
            };

            manifest.PublishTime = packagedAt;
            await _manifestStore.SaveManifestAsync(manifest).ConfigureAwait(false);
            _lastPackageTime = packagedAt;
        }

        private async Task LoadManifestTimeAsync()
        {
            var manifest = await _manifestStore.GetManifestAsync().ConfigureAwait(false);
            _lastPackageTime = manifest?.PublishTime;
        }

        private ChangeStatus BuildModuleStatus(PackageModuleMapping mapping)
        {
            var modulePath = $"{mapping.ModuleType}/{mapping.SubModule}";
            var status = new ChangeStatus
            {
                ModulePath = modulePath,
                DisplayName = WritingNavigationCatalog.GetSubModuleDisplayName(mapping.SubModule),
                LastPackaged = _lastPackageTime,
                IsEnabled = true
            };

            var moduleDirectory = Path.Combine(_rootDirectory, "Modules", mapping.ModuleType, mapping.SubModule);
            if (!Directory.Exists(moduleDirectory))
            {
                status.Status = ChangeStatusType.Never;
                return status;
            }

            var jsonFiles = Directory.GetFiles(moduleDirectory, "*.json", SearchOption.AllDirectories);
            if (jsonFiles.Length > 0)
            {
                status.LastModified = jsonFiles.Select(File.GetLastWriteTime).Max();
                status.ItemCount = CountDataItems(jsonFiles);
            }

            if (_lastPackageTime == null)
                status.Status = ChangeStatusType.Never;
            else if (status.LastModified > _lastPackageTime)
                status.Status = ChangeStatusType.Changed;
            else
                status.Status = ChangeStatusType.Latest;

            return status;
        }

        private static int CountDataItems(IEnumerable<string> jsonFiles)
        {
            var count = 0;
            foreach (var file in jsonFiles)
            {
                var fileName = Path.GetFileName(file);
                if (string.Equals(fileName, "categories.json", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(fileName, "built_in_categories.json", StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(file));
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        count += doc.RootElement.GetArrayLength();
                }
                catch (JsonException)
                {
                }
                catch (IOException)
                {
                }
            }

            return count;
        }
    }
}
