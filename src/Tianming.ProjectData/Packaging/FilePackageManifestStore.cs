using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Publishing;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class FilePackageManifestStore
    {
        private readonly string _rootDirectory;
        private readonly string _publishedDirectory;
        private readonly string _historyDirectory;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public FilePackageManifestStore(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("打包目录不能为空", nameof(rootDirectory));

            _rootDirectory = rootDirectory;
            _publishedDirectory = Path.Combine(rootDirectory, "Published");
            _historyDirectory = Path.Combine(rootDirectory, "History");
            Directory.CreateDirectory(_rootDirectory);
            Directory.CreateDirectory(_publishedDirectory);
        }

        public int RetainCount { get; set; } = 5;

        public async Task SaveManifestAsync(ManifestInfo manifest)
        {
            if (manifest == null)
                throw new ArgumentNullException(nameof(manifest));

            Directory.CreateDirectory(_publishedDirectory);
            await WriteJsonAtomicAsync(GetManifestPath(), manifest).ConfigureAwait(false);
        }

        public async Task<ManifestInfo?> GetManifestAsync()
        {
            var path = GetManifestPath();
            if (!File.Exists(path))
                return null;

            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            return JsonSerializer.Deserialize<ManifestInfo>(json, _jsonOptions);
        }

        public async Task<PublishStatus> GetPublishStatusAsync(int changedModuleCount)
        {
            var manifest = await GetManifestAsync().ConfigureAwait(false);
            return new PublishStatus
            {
                IsPublished = manifest != null,
                LastPublishTime = manifest?.PublishTime,
                CurrentVersion = manifest?.Version ?? 0,
                NeedsRepublish = changedModuleCount > 0,
                ChangedModuleCount = changedModuleCount
            };
        }

        public async Task<bool> SaveCurrentToHistoryAsync()
        {
            var manifest = await GetManifestAsync().ConfigureAwait(false);
            if (manifest == null)
                return false;

            var versionDirectory = Path.Combine(_historyDirectory, $"v{manifest.Version}");
            var tempDirectory = versionDirectory + "_tmp";
            if (Directory.Exists(tempDirectory))
                Directory.Delete(tempDirectory, recursive: true);
            Directory.CreateDirectory(tempDirectory);

            try
            {
                File.Copy(GetManifestPath(), Path.Combine(tempDirectory, "manifest.json"), overwrite: true);
                CopyDirectory(_publishedDirectory, Path.Combine(tempDirectory, "Published"));

                if (Directory.Exists(versionDirectory))
                    Directory.Delete(versionDirectory, recursive: true);
                Directory.Move(tempDirectory, versionDirectory);
                CleanupOldHistory();
                return true;
            }
            catch
            {
                if (Directory.Exists(tempDirectory))
                    Directory.Delete(tempDirectory, recursive: true);
                throw;
            }
        }

        public async Task<List<PackageHistoryEntry>> GetAllHistoryAsync(bool includeCurrent = true)
        {
            var entries = new List<PackageHistoryEntry>();
            var current = await GetManifestAsync().ConfigureAwait(false);
            if (includeCurrent && current != null)
                entries.Add(CreateHistoryEntry(current, isCurrent: true, _publishedDirectory));

            if (Directory.Exists(_historyDirectory))
            {
                foreach (var versionDirectory in GetVersionDirectories())
                {
                    var manifest = await ReadHistoryManifestAsync(versionDirectory).ConfigureAwait(false);
                    if (manifest != null && (!includeCurrent || manifest.Version != current?.Version))
                        entries.Add(CreateHistoryEntry(manifest, isCurrent: false, versionDirectory));
                }
            }

            return entries;
        }

        public async Task<PackageVersionDiff> GetVersionDiffAsync(int historyVersion)
        {
            var diff = new PackageVersionDiff { HistoryVersion = historyVersion };
            var current = await GetManifestAsync().ConfigureAwait(false);
            if (current == null)
                return diff;

            diff.CurrentVersion = current.Version;
            var historyManifest = await ReadHistoryManifestAsync(Path.Combine(_historyDirectory, $"v{historyVersion}")).ConfigureAwait(false);
            if (historyManifest == null)
                return diff;

            foreach (var (moduleType, currentModules) in current.EnabledModules)
            {
                historyManifest.EnabledModules.TryGetValue(moduleType, out var historyModules);
                historyModules ??= new Dictionary<string, bool>();
                foreach (var (subModule, currentEnabled) in currentModules)
                {
                    var historyEnabled = historyModules.GetValueOrDefault(subModule, true);
                    if (currentEnabled == historyEnabled)
                        continue;

                    diff.DiffItems.Add(new ModuleDiffItem
                    {
                        ModulePath = $"{moduleType}/{subModule}",
                        DisplayName = subModule,
                        Type = DiffType.EnabledChanged,
                        CurrentState = currentEnabled ? "启用" : "禁用",
                        HistoryState = historyEnabled ? "启用" : "禁用"
                    });
                }
            }

            return diff;
        }

        public Task<PackageRestoreCheck> CanRestoreVersionAsync(int version)
        {
            var versionDirectory = Path.Combine(_historyDirectory, $"v{version}");
            if (!Directory.Exists(versionDirectory))
                return Task.FromResult(new PackageRestoreCheck(false, false, $"版本 v{version} 不存在"));

            if (HasGeneratedContent())
                return Task.FromResult(new PackageRestoreCheck(false, true, "已存在生成正文，恢复历史版本前需要确认覆盖风险"));

            return Task.FromResult(new PackageRestoreCheck(true, false, "可以恢复"));
        }

        public Task<bool> RestoreVersionAsync(int version)
        {
            return RestoreVersionAsync(version, allowGeneratedContentOverwrite: false);
        }

        public async Task<bool> RestoreVersionAsync(int version, bool allowGeneratedContentOverwrite)
        {
            var versionDirectory = Path.Combine(_historyDirectory, $"v{version}");
            if (!Directory.Exists(versionDirectory))
                return false;

            if (!allowGeneratedContentOverwrite && HasGeneratedContent())
                return false;

            await SaveCurrentToHistoryAsync().ConfigureAwait(false);

            var historyManifest = Path.Combine(versionDirectory, "manifest.json");
            if (File.Exists(historyManifest))
                File.Copy(historyManifest, GetManifestPath(), overwrite: true);

            var historyPublished = Path.Combine(versionDirectory, "Published");
            if (Directory.Exists(historyPublished))
            {
                foreach (var path in Directory.GetFileSystemEntries(_publishedDirectory))
                {
                    if (string.Equals(Path.GetFileName(path), "manifest.json", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (Directory.Exists(path))
                        Directory.Delete(path, recursive: true);
                    else
                        File.Delete(path);
                }

                CopyDirectory(historyPublished, _publishedDirectory);
            }

            return true;
        }

        public Task ClearAllAsync()
        {
            DeleteIfExists(GetManifestPath());

            if (Directory.Exists(_publishedDirectory))
            {
                foreach (var path in Directory.GetFileSystemEntries(_publishedDirectory))
                {
                    if (Directory.Exists(path))
                        Directory.Delete(path, recursive: true);
                    else
                        File.Delete(path);
                }
            }

            DeleteDirectoryIfExists(_historyDirectory);
            DeleteDirectoryIfExists(Path.Combine(_rootDirectory, "Generated"));
            DeleteDirectoryIfExists(Path.Combine(_rootDirectory, "VectorIndex"));
            DeleteIfExists(Path.Combine(_rootDirectory, "vector_degraded.flag"));
            return Task.CompletedTask;
        }

        public void CleanupOldHistory()
        {
            if (!Directory.Exists(_historyDirectory))
                return;

            var versionDirectories = GetVersionDirectories().ToList();
            for (var i = Math.Clamp(RetainCount, 1, 10); i < versionDirectories.Count; i++)
                Directory.Delete(versionDirectories[i], recursive: true);
        }

        private string GetManifestPath()
        {
            return Path.Combine(_publishedDirectory, "manifest.json");
        }

        private IEnumerable<string> GetVersionDirectories()
        {
            return Directory
                .GetDirectories(_historyDirectory, "v*")
                .OrderByDescending(directory =>
                {
                    var name = Path.GetFileName(directory);
                    return int.TryParse(name.TrimStart('v'), out var version) ? version : 0;
                });
        }

        private async Task<ManifestInfo?> ReadHistoryManifestAsync(string versionDirectory)
        {
            var path = Path.Combine(versionDirectory, "manifest.json");
            if (!File.Exists(path))
                return null;

            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            return JsonSerializer.Deserialize<ManifestInfo>(json, _jsonOptions);
        }

        private static PackageHistoryEntry CreateHistoryEntry(ManifestInfo manifest, bool isCurrent, string path)
        {
            return new PackageHistoryEntry
            {
                Version = manifest.Version,
                PublishTime = manifest.PublishTime,
                EnabledModules = manifest.EnabledModules,
                EnabledSummary = BuildEnabledSummary(manifest.EnabledModules),
                IsCurrent = isCurrent,
                HistoryPath = path
            };
        }

        private static string BuildEnabledSummary(Dictionary<string, Dictionary<string, bool>> enabledModules)
        {
            var parts = new List<string>();
            foreach (var (moduleType, modules) in enabledModules)
                parts.Add($"{moduleType}({modules.Count(module => module.Value)}/{modules.Count})");
            return string.Join(" + ", parts);
        }

        private static async Task WriteJsonAtomicAsync<T>(string path, T value)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var tempPath = path + ".tmp";
            var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
            File.Move(tempPath, path, overwrite: true);
        }

        private static void CopyDirectory(string sourceDirectory, string destinationDirectory)
        {
            if (!Directory.Exists(sourceDirectory))
                return;

            Directory.CreateDirectory(destinationDirectory);
            foreach (var file in Directory.GetFiles(sourceDirectory))
            {
                var fileName = Path.GetFileName(file);
                if (string.Equals(fileName, "manifest.json", StringComparison.OrdinalIgnoreCase))
                    continue;
                File.Copy(file, Path.Combine(destinationDirectory, fileName), overwrite: true);
            }

            foreach (var directory in Directory.GetDirectories(sourceDirectory))
                CopyDirectory(directory, Path.Combine(destinationDirectory, Path.GetFileName(directory)));
        }

        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
                File.Delete(path);
        }

        private static void DeleteDirectoryIfExists(string path)
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }

        private bool HasGeneratedContent()
        {
            var generatedDirectory = Path.Combine(_rootDirectory, "Generated");
            return Directory.Exists(generatedDirectory)
                && Directory.GetFiles(generatedDirectory, "*.md", SearchOption.AllDirectories).Length > 0;
        }
    }

    public sealed record PackageRestoreCheck(
        bool CanRestore,
        bool RequiresGeneratedContentConfirmation,
        string Message);
}
