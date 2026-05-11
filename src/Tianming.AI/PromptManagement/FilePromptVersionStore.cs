using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TM.Framework.Common.Helpers.Id;
using TM.Modules.AIAssistant.PromptTools.VersionTesting.Models;

namespace TM.Modules.AIAssistant.PromptTools.VersionTesting.Services;

public interface IPromptVersionStore
{
    IReadOnlyList<TestVersionData> GetAllVersions();
    void AddVersion(TestVersionData version);
    void UpdateVersion(TestVersionData version);
}

public sealed class FilePromptVersionStore : IPromptVersionStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _dataFilePath;
    private readonly object _lock = new();
    private List<TestVersionData> _versions = new();

    public FilePromptVersionStore(string dataFilePath)
    {
        if (string.IsNullOrWhiteSpace(dataFilePath))
            throw new ArgumentException("提示词版本测试文件路径不能为空", nameof(dataFilePath));

        _dataFilePath = dataFilePath;
        LoadData();
    }

    public IReadOnlyList<TestVersionData> GetAllVersions()
    {
        lock (_lock)
        {
            return _versions
                .OrderByDescending(version => version.CreatedTime)
                .Select(Clone)
                .ToList();
        }
    }

    public void AddVersion(TestVersionData version)
    {
        if (version == null || string.IsNullOrWhiteSpace(version.Name))
            throw new ArgumentException("版本名称不能为空", nameof(version));

        var next = Clone(version);
        if (string.IsNullOrWhiteSpace(next.Id))
            next.Id = ShortIdGenerator.New("D");

        next.CreatedTime = DateTime.Now;
        next.ModifiedTime = DateTime.Now;

        lock (_lock)
        {
            _versions.RemoveAll(item => string.Equals(item.Id, next.Id, StringComparison.Ordinal));
            _versions.Add(next);
            SaveData();
        }
    }

    public void UpdateVersion(TestVersionData version)
    {
        if (version == null)
            throw new ArgumentNullException(nameof(version));

        lock (_lock)
        {
            var existing = _versions.FirstOrDefault(item => string.Equals(item.Id, version.Id, StringComparison.Ordinal));
            if (existing == null)
                return;

            existing.Name = version.Name;
            existing.Category = version.Category;
            existing.CategoryId = version.CategoryId;
            existing.IsEnabled = version.IsEnabled;
            existing.PromptId = version.PromptId;
            existing.VersionNumber = version.VersionNumber;
            existing.Description = version.Description;
            existing.TestInput = version.TestInput;
            existing.ExpectedOutput = version.ExpectedOutput;
            existing.TestScenario = version.TestScenario;
            existing.ActualOutput = version.ActualOutput;
            existing.Rating = version.Rating;
            existing.TestNotes = version.TestNotes;
            existing.TestStatus = version.TestStatus;
            existing.TestTime = version.TestTime;
            existing.ModifiedTime = DateTime.Now;
            SaveData();
        }
    }

    public void DeleteVersion(string id)
    {
        lock (_lock)
        {
            if (_versions.RemoveAll(item => string.Equals(item.Id, id, StringComparison.Ordinal)) > 0)
                SaveData();
        }
    }

    public int ClearAllVersions()
    {
        lock (_lock)
        {
            var count = _versions.Count;
            _versions.Clear();
            SaveData();
            return count;
        }
    }

    private void LoadData()
    {
        if (!File.Exists(_dataFilePath))
            return;

        try
        {
            var json = File.ReadAllText(_dataFilePath);
            _versions = JsonSerializer.Deserialize<List<TestVersionData>>(json, JsonOptions) ?? new List<TestVersionData>();
        }
        catch (JsonException)
        {
            _versions = new List<TestVersionData>();
        }
        catch (IOException)
        {
            _versions = new List<TestVersionData>();
        }
    }

    private void SaveData()
    {
        var directory = Path.GetDirectoryName(_dataFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var tmp = _dataFilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_versions, JsonOptions));
        File.Move(tmp, _dataFilePath, overwrite: true);
    }

    private static TestVersionData Clone(TestVersionData version)
    {
        return new TestVersionData
        {
            Id = version.Id,
            Name = version.Name,
            Category = version.Category,
            CategoryId = version.CategoryId,
            IsEnabled = version.IsEnabled,
            PromptId = version.PromptId,
            VersionNumber = version.VersionNumber,
            Description = version.Description,
            TestInput = version.TestInput,
            ExpectedOutput = version.ExpectedOutput,
            TestScenario = version.TestScenario,
            ActualOutput = version.ActualOutput,
            Rating = version.Rating,
            TestNotes = version.TestNotes,
            TestStatus = version.TestStatus,
            TestTime = version.TestTime,
            CreatedTime = version.CreatedTime,
            ModifiedTime = version.ModifiedTime
        };
    }
}
