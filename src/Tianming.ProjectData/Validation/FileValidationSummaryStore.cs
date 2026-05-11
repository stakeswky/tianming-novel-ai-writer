using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using TM.Framework.Common.Helpers.Id;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class FileValidationSummaryStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private static readonly Regex VolumeNumberRegex = new(@"^第(\d+)卷", RegexOptions.Compiled);

        private readonly string _dataDirectory;
        private readonly List<ValidationSummaryCategory> _categories;
        private readonly List<ValidationSummaryData> _dataItems = new();

        public FileValidationSummaryStore(
            string dataDirectory,
            IReadOnlyList<ValidationSummaryCategory>? categories = null)
        {
            if (string.IsNullOrWhiteSpace(dataDirectory))
                throw new ArgumentException("校验汇总目录不能为空", nameof(dataDirectory));

            _dataDirectory = dataDirectory;
            _categories = categories?.Select(CloneCategory).ToList() ?? new List<ValidationSummaryCategory>();
            Directory.CreateDirectory(_dataDirectory);
            LoadData();
        }

        public List<ValidationSummaryData> GetAllData()
        {
            return _dataItems.Select(CloneData).ToList();
        }

        public ValidationSummaryData? GetDataById(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return null;

            var data = _dataItems.FirstOrDefault(item => string.Equals(item.Id, id, StringComparison.Ordinal));
            return data == null ? null : CloneData(data);
        }

        public ValidationSummaryData? GetDataByVolumeNumber(int volumeNumber)
        {
            var data = _dataItems.FirstOrDefault(item => item.TargetVolumeNumber == volumeNumber);
            return data == null ? null : CloneData(data);
        }

        public void AddData(ValidationSummaryData data)
        {
            if (data == null)
                return;

            var next = CloneData(data);
            if (string.IsNullOrWhiteSpace(next.Id))
                next.Id = ShortIdGenerator.New("D");

            EnsureDataCategoryId(next);
            next.CreatedTime = DateTime.Now;
            next.ModifiedTime = DateTime.Now;

            _dataItems.RemoveAll(item => string.Equals(item.Id, next.Id, StringComparison.Ordinal));
            _dataItems.Add(next);
            SaveDataItem(next);
        }

        public void UpdateData(ValidationSummaryData data)
        {
            if (data == null || string.IsNullOrWhiteSpace(data.Id))
                return;

            var next = CloneData(data);
            EnsureDataCategoryId(next);

            var index = _dataItems.FindIndex(item => string.Equals(item.Id, next.Id, StringComparison.Ordinal));
            if (index < 0)
                return;

            next.CreatedTime = _dataItems[index].CreatedTime;
            next.ModifiedTime = DateTime.Now;
            _dataItems[index] = next;
            SaveDataItem(next);
        }

        public void DeleteData(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return;

            if (_dataItems.RemoveAll(item => string.Equals(item.Id, id, StringComparison.Ordinal)) > 0)
            {
                var filePath = Path.Combine(_dataDirectory, $"{id}.json");
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
        }

        public List<ValidationSummaryCategory> GetAllCategories()
        {
            return _categories.Select(CloneCategory).ToList();
        }

        public void SaveVolumeValidation(int volumeNumber, ValidationSummaryData data)
        {
            if (data == null)
                return;

            var next = CloneData(data);
            var volumeCategory = _categories.FirstOrDefault(category => category.Order == volumeNumber);
            var categoryName = volumeCategory?.Name ?? $"第{volumeNumber}卷";

            next.TargetVolumeNumber = volumeNumber;
            next.TargetVolumeName = categoryName;
            next.Name = $"{categoryName}校验";
            next.Category = categoryName;
            next.CategoryId = volumeCategory?.Id ?? string.Empty;
            next.LastValidatedTime = DateTime.Now;

            var existing = _dataItems.FirstOrDefault(item => item.TargetVolumeNumber == volumeNumber);
            if (existing == null)
            {
                AddData(next);
                return;
            }

            next.Id = existing.Id;
            next.CreatedTime = existing.CreatedTime;
            next.ModifiedTime = DateTime.Now;

            var index = _dataItems.FindIndex(item => string.Equals(item.Id, existing.Id, StringComparison.Ordinal));
            if (index >= 0)
                _dataItems[index] = next;
            SaveDataItem(next);
        }

        public int ParseVolumeNumber(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName))
                return -1;

            var match = VolumeNumberRegex.Match(categoryName);
            return match.Success && int.TryParse(match.Groups[1].Value, out var volumeNumber)
                ? volumeNumber
                : -1;
        }

        private void LoadData()
        {
            _dataItems.Clear();
            if (!Directory.Exists(_dataDirectory))
                return;

            foreach (var file in Directory.GetFiles(_dataDirectory, "*.json", SearchOption.TopDirectoryOnly))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var data = JsonSerializer.Deserialize<ValidationSummaryData>(json, JsonOptions);
                    if (data != null)
                        _dataItems.Add(data);
                }
                catch (JsonException)
                {
                }
                catch (IOException)
                {
                }
            }
        }

        private void EnsureDataCategoryId(ValidationSummaryData data)
        {
            if (string.IsNullOrWhiteSpace(data.Category) || !string.IsNullOrWhiteSpace(data.CategoryId))
                return;

            var category = _categories.FirstOrDefault(item => string.Equals(item.Name, data.Category, StringComparison.Ordinal));
            if (category != null)
                data.CategoryId = category.Id;
        }

        private void SaveDataItem(ValidationSummaryData data)
        {
            Directory.CreateDirectory(_dataDirectory);
            var filePath = Path.Combine(_dataDirectory, $"{data.Id}.json");
            var tmp = filePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(data, JsonOptions));
            File.Move(tmp, filePath, overwrite: true);
        }

        private static ValidationSummaryData CloneData(ValidationSummaryData data)
        {
            return new ValidationSummaryData
            {
                Id = data.Id,
                Name = data.Name,
                Icon = data.Icon,
                Category = data.Category,
                CategoryId = data.CategoryId,
                IsEnabled = data.IsEnabled,
                CreatedTime = data.CreatedTime,
                ModifiedTime = data.ModifiedTime,
                TargetVolumeNumber = data.TargetVolumeNumber,
                TargetVolumeName = data.TargetVolumeName,
                SampledChapterCount = data.SampledChapterCount,
                SampledChapterIds = data.SampledChapterIds.ToList(),
                LastValidatedTime = data.LastValidatedTime,
                OverallResult = data.OverallResult,
                ModuleResults = data.ModuleResults.Select(CloneModuleResult).ToList(),
                DependencyModuleVersions = new Dictionary<string, int>(data.DependencyModuleVersions, StringComparer.Ordinal)
            };
        }

        private static ModuleValidationResult CloneModuleResult(ModuleValidationResult result)
        {
            return new ModuleValidationResult
            {
                ModuleName = result.ModuleName,
                DisplayName = result.DisplayName,
                VerificationType = result.VerificationType,
                Result = result.Result,
                IssueDescription = result.IssueDescription,
                FixSuggestion = result.FixSuggestion,
                ExtendedDataJson = result.ExtendedDataJson,
                ProblemItemsJson = result.ProblemItemsJson
            };
        }

        private static ValidationSummaryCategory CloneCategory(ValidationSummaryCategory category)
        {
            return new ValidationSummaryCategory
            {
                Id = category.Id,
                Name = category.Name,
                Icon = category.Icon,
                ParentCategory = category.ParentCategory,
                Level = category.Level,
                Order = category.Order,
                IsEnabled = category.IsEnabled,
                IsBuiltIn = category.IsBuiltIn
            };
        }
    }
}
