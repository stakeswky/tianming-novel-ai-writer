using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Services.Modules.VersionTracking
{
    public sealed class FileVersionTrackingService
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true
        };

        private readonly string _registryPath;
        private readonly Action<string, IReadOnlyList<string>>? _downstreamNotifier;
        private readonly HashSet<string> _pendingDownstreamNotifications = new(StringComparer.Ordinal);
        private VersionState _state;

        public FileVersionTrackingService(
            string rootDirectory,
            Action<string, IReadOnlyList<string>>? downstreamNotifier = null)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("项目目录不能为空", nameof(rootDirectory));

            _registryPath = Path.Combine(rootDirectory, "version_registry.json");
            _downstreamNotifier = downstreamNotifier;
            _state = LoadState();
        }

        public bool SuppressDownstreamToast { get; set; }

        public int GetModuleVersion(string moduleName)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                return 0;

            return _state.ModuleVersions.TryGetValue(moduleName, out var version) ? version : 0;
        }

        public int IncrementModuleVersion(string moduleName, bool showDownstreamToast = true)
        {
            if (string.IsNullOrWhiteSpace(moduleName))
                throw new ArgumentException("模块名不能为空", nameof(moduleName));

            _state.ModuleVersions[moduleName] = GetModuleVersion(moduleName) + 1;
            SaveState();

            if (showDownstreamToast)
                NotifyDownstreamModules(moduleName);

            return _state.ModuleVersions[moduleName];
        }

        public void FlushPendingDownstreamNotifications()
        {
            var pending = _pendingDownstreamNotifications.ToList();
            _pendingDownstreamNotifications.Clear();

            foreach (var moduleName in pending)
                NotifyDownstreamModules(moduleName);
        }

        public Dictionary<string, int> GetDependencySnapshot(string currentModule)
        {
            return DependencyConfig
                .GetDependencies(currentModule)
                .ToDictionary(moduleName => moduleName, GetModuleVersion);
        }

        public List<string> CheckOutdatedDependencies(Dictionary<string, int>? savedVersions)
        {
            if (savedVersions == null || savedVersions.Count == 0)
                return new List<string>();

            return savedVersions
                .Where(pair => GetModuleVersion(pair.Key) > pair.Value)
                .Select(pair => pair.Key)
                .ToList();
        }

        private void NotifyDownstreamModules(string moduleName)
        {
            if (SuppressDownstreamToast)
            {
                _pendingDownstreamNotifications.Add(moduleName);
                return;
            }

            var downstream = DependencyConfig.GetDownstreamModules(moduleName);
            if (downstream.Count > 0)
                _downstreamNotifier?.Invoke(moduleName, downstream);
        }

        private VersionState LoadState()
        {
            if (!File.Exists(_registryPath))
                return new VersionState();

            try
            {
                var json = File.ReadAllText(_registryPath);
                return JsonSerializer.Deserialize<VersionState>(json, JsonOptions) ?? new VersionState();
            }
            catch (JsonException)
            {
                return new VersionState();
            }
            catch (IOException)
            {
                return new VersionState();
            }
        }

        private void SaveState()
        {
            var directory = Path.GetDirectoryName(_registryPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var tempPath = $"{_registryPath}.{Guid.NewGuid():N}.tmp";
            var json = JsonSerializer.Serialize(_state, JsonOptions);
            File.WriteAllText(tempPath, json);
            File.Move(tempPath, _registryPath, overwrite: true);
        }

        private sealed class VersionState
        {
            [JsonPropertyName("ModuleVersions")]
            public Dictionary<string, int> ModuleVersions { get; set; } = new();
        }
    }
}
