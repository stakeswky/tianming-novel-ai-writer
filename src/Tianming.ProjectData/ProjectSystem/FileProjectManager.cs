using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.ProjectSystem
{
    public sealed class FileProjectManager
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private readonly string _projectsRoot;
        private readonly string _configPath;
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private ProjectConfig _config = new();

        public FileProjectManager(string storageRoot)
        {
            if (string.IsNullOrWhiteSpace(storageRoot))
                throw new ArgumentException("存储根目录不能为空", nameof(storageRoot));

            _projectsRoot = Path.Combine(storageRoot, "Projects");
            _configPath = Path.Combine(storageRoot, "Config", "projects.json");
        }

        public ProjectInfo? CurrentProject =>
            string.IsNullOrEmpty(_config.CurrentProject)
                ? null
                : _config.Projects.FirstOrDefault(project => project.Id == _config.CurrentProject);

        public IReadOnlyList<ProjectInfo> Projects => _config.Projects;

        public async Task LoadAsync()
        {
            Directory.CreateDirectory(_projectsRoot);
            if (File.Exists(_configPath))
            {
                var json = await File.ReadAllTextAsync(_configPath).ConfigureAwait(false);
                _config = JsonSerializer.Deserialize<ProjectConfig>(json, JsonOptions) ?? new ProjectConfig();
            }

            if (_config.Projects.Count == 0)
            {
                var defaultProject = new ProjectInfo
                {
                    Id = "default",
                    Name = "默认项目",
                    Description = "默认创作项目",
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now,
                    Path = Path.Combine(_projectsRoot, "默认项目")
                };

                _config.Projects.Add(defaultProject);
                _config.CurrentProject = defaultProject.Id;
                EnsureProjectDirectories(defaultProject.Path);
                await SaveConfigAsync().ConfigureAwait(false);
                return;
            }

            foreach (var project in _config.Projects)
                EnsureProjectDirectories(project.Path);

            if (_config.CurrentProject == null || _config.Projects.All(project => project.Id != _config.CurrentProject))
                _config.CurrentProject = _config.Projects.FirstOrDefault()?.Id;
        }

        public async Task<ProjectInfo> CreateProjectAsync(string name, string? description = null)
        {
            if (_config.Projects.Count > 0)
                return _config.Projects[0];

            var id = Guid.NewGuid().ToString("N")[..8];
            var safeName = SanitizeFileName(name);
            if (string.IsNullOrWhiteSpace(safeName))
                safeName = id;

            var path = GetUniqueProjectPath(safeName);
            var project = new ProjectInfo
            {
                Id = id,
                Name = name,
                Description = description ?? string.Empty,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now,
                Path = path
            };

            EnsureProjectDirectories(project.Path);
            _config.Projects.Add(project);
            _config.CurrentProject ??= project.Id;
            await SaveConfigAsync().ConfigureAwait(false);
            return project;
        }

        public async Task<bool> RenameProjectAsync(string projectId, string newName)
        {
            var project = _config.Projects.FirstOrDefault(item => item.Id == projectId);
            if (project == null)
                return false;

            var safeName = SanitizeFileName(newName);
            if (string.IsNullOrWhiteSpace(safeName))
                safeName = projectId;

            var oldPath = project.Path;
            var newPath = Path.Combine(_projectsRoot, safeName);
            if (!string.Equals(oldPath, newPath, StringComparison.Ordinal)
                && Directory.Exists(newPath))
                return false;

            if (!string.Equals(oldPath, newPath, StringComparison.Ordinal))
            {
                if (Directory.Exists(oldPath))
                    Directory.Move(oldPath, newPath);
                else
                    EnsureProjectDirectories(newPath);
                project.Path = newPath;
            }

            project.Name = newName;
            project.UpdatedAt = DateTime.Now;
            await SaveConfigAsync().ConfigureAwait(false);
            return true;
        }

        public async Task<bool> SwitchProjectAsync(string projectId)
        {
            if (_config.Projects.All(project => project.Id != projectId))
                return false;

            _config.CurrentProject = projectId;
            await SaveConfigAsync().ConfigureAwait(false);
            return true;
        }

        public async Task<bool> DeleteProjectAsync(string projectId)
        {
            var project = _config.Projects.FirstOrDefault(item => item.Id == projectId);
            if (project == null)
                return false;

            if (Directory.Exists(project.Path))
                Directory.Delete(project.Path, recursive: true);

            _config.Projects.Remove(project);
            if (_config.CurrentProject == projectId)
                _config.CurrentProject = _config.Projects.FirstOrDefault()?.Id;

            await SaveConfigAsync().ConfigureAwait(false);
            return true;
        }

        private string GetUniqueProjectPath(string safeName)
        {
            var path = Path.Combine(_projectsRoot, safeName);
            var suffix = 1;
            while (Directory.Exists(path) || _config.Projects.Any(project => string.Equals(project.Path, path, StringComparison.Ordinal)))
                path = Path.Combine(_projectsRoot, $"{safeName}_{suffix++}");
            return path;
        }

        private async Task SaveConfigAsync()
        {
            await _saveLock.WaitAsync().ConfigureAwait(false);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
                var tempPath = _configPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                var json = JsonSerializer.Serialize(_config, JsonOptions);
                await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
                File.Move(tempPath, _configPath, overwrite: true);
            }
            finally
            {
                _saveLock.Release();
            }
        }

        private static void EnsureProjectDirectories(string path)
        {
            Directory.CreateDirectory(Path.Combine(path, "Config", "Design"));
            Directory.CreateDirectory(Path.Combine(path, "Config", "Generate"));
            Directory.CreateDirectory(Path.Combine(path, "Generated", "chapters"));
            Directory.CreateDirectory(Path.Combine(path, "Validation", "reports"));
            Directory.CreateDirectory(Path.Combine(path, "History"));
        }

        private static string SanitizeFileName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var invalid = Path.GetInvalidFileNameChars()
                .Concat(['<', '>', ':', '"', '/', '\\', '|', '?', '*'])
                .ToHashSet();

            var sanitized = new string(name.Trim().Select(c => invalid.Contains(c) ? '_' : c).ToArray());
            return sanitized.Trim();
        }
    }

    public sealed class ProjectConfig
    {
        [JsonPropertyName("CurrentProject")] public string? CurrentProject { get; set; }
        [JsonPropertyName("Projects")] public List<ProjectInfo> Projects { get; set; } = new();
    }

    public sealed class ProjectInfo
    {
        [JsonPropertyName("Id")] public string Id { get; set; } = string.Empty;
        [JsonPropertyName("Name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("Description")] public string Description { get; set; } = string.Empty;
        [JsonPropertyName("Path")] public string Path { get; set; } = string.Empty;
        [JsonPropertyName("CreatedAt")] public DateTime CreatedAt { get; set; }
        [JsonPropertyName("UpdatedAt")] public DateTime UpdatedAt { get; set; }
    }
}
