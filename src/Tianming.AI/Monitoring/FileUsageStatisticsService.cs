using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TM.Services.Framework.AI.Monitoring;

public sealed class FileUsageStatisticsService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly List<ApiCallRecord> _records = new();
    private readonly object _lock = new();
    private readonly string _storagePath;
    private readonly int _retentionDays;

    public FileUsageStatisticsService(string storagePath, int retentionDays = 3)
    {
        if (string.IsNullOrWhiteSpace(storagePath))
            throw new ArgumentException("统计文件路径不能为空", nameof(storagePath));

        _storagePath = storagePath;
        _retentionDays = Math.Max(retentionDays, 1);
        LoadRecords();
        TrimExpiredRecords();
    }

    public void RecordCall(
        string modelName,
        string provider,
        bool success,
        int responseTimeMs,
        int inputTokens = 0,
        int outputTokens = 0,
        string? errorMessage = null)
    {
        var record = new ApiCallRecord
        {
            Timestamp = DateTime.Now,
            ModelName = modelName,
            Provider = provider,
            Success = success,
            ResponseTimeMs = responseTimeMs,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            ErrorMessage = errorMessage
        };

        lock (_lock)
        {
            _records.Add(record);
        }

        SaveRecords();
    }

    public void RecordCall(ApiCallRecord record)
    {
        if (record == null)
            return;

        lock (_lock)
        {
            _records.Add(record);
        }

        SaveRecords();
    }

    public IReadOnlyList<ApiCallRecord> GetAllRecords()
    {
        lock (_lock)
        {
            return _records.ToList();
        }
    }

    public StatisticsSummary GetSummary()
    {
        List<ApiCallRecord> snapshot;
        lock (_lock)
        {
            snapshot = _records.ToList();
        }

        return BuildSummary(snapshot);
    }

    public IReadOnlyList<DailyStatistics> GetDailyStatistics(int days = 7)
    {
        var startDate = DateTime.Now.Date.AddDays(-days);
        List<ApiCallRecord> records;
        lock (_lock)
        {
            records = _records
                .Where(record => record.Timestamp >= startDate)
                .ToList();
        }

        return records
            .GroupBy(record => record.Timestamp.Date)
            .Select(group => new DailyStatistics
            {
                Date = group.Key,
                TotalCalls = group.Count(),
                SuccessCalls = group.Count(record => record.Success),
                FailedCalls = group.Count(record => !record.Success),
                AverageResponseTime = group.Average(record => record.ResponseTimeMs)
            })
            .OrderBy(day => day.Date)
            .ToList();
    }

    public IReadOnlyDictionary<string, StatisticsSummary> GetStatisticsByModel()
    {
        List<ApiCallRecord> snapshot;
        lock (_lock)
        {
            snapshot = _records.ToList();
        }

        return snapshot
            .GroupBy(record => record.ModelName)
            .ToDictionary(group => group.Key, group => BuildSummary(group.ToList()));
    }

    public IReadOnlyList<ApiCallRecord> GetRecentRecords(int count = 50)
    {
        lock (_lock)
        {
            return _records
                .OrderByDescending(record => record.Timestamp)
                .Take(Math.Max(count, 0))
                .ToList();
        }
    }

    public void ClearStatistics()
    {
        lock (_lock)
        {
            _records.Clear();
        }

        SaveRecords();
    }

    private static StatisticsSummary BuildSummary(IReadOnlyList<ApiCallRecord> records)
    {
        if (records.Count == 0)
            return new StatisticsSummary();

        return new StatisticsSummary
        {
            TotalCalls = records.Count,
            SuccessCalls = records.Count(record => record.Success),
            FailedCalls = records.Count(record => !record.Success),
            AverageResponseTime = records.Average(record => record.ResponseTimeMs),
            TotalInputTokens = records.Sum(record => record.InputTokens),
            TotalOutputTokens = records.Sum(record => record.OutputTokens),
            FirstCallTime = records.Min(record => record.Timestamp),
            LastCallTime = records.Max(record => record.Timestamp)
        };
    }

    private void LoadRecords()
    {
        if (!File.Exists(_storagePath))
            return;

        try
        {
            var json = File.ReadAllText(_storagePath);
            var records = JsonSerializer.Deserialize<List<ApiCallRecord>>(json, JsonOptions);
            if (records == null)
                return;

            lock (_lock)
            {
                _records.AddRange(records);
            }
        }
        catch (JsonException)
        {
        }
        catch (IOException)
        {
        }
    }

    private void TrimExpiredRecords()
    {
        var cutoff = DateTime.Now.AddDays(-_retentionDays);
        var removed = false;

        lock (_lock)
        {
            var before = _records.Count;
            _records.RemoveAll(record => record.Timestamp < cutoff);
            removed = before != _records.Count;
        }

        if (removed)
            SaveRecords();
    }

    private void SaveRecords()
    {
        var directory = Path.GetDirectoryName(_storagePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        List<ApiCallRecord> snapshot;
        lock (_lock)
        {
            snapshot = _records.ToList();
        }

        var tempPath = $"{_storagePath}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tempPath, JsonSerializer.Serialize(snapshot, JsonOptions));
        File.Move(tempPath, _storagePath, overwrite: true);
    }
}
