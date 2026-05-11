using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Logging;

public sealed class PortableLogOutputStatistics
{
    [JsonPropertyName("TargetName")] public string TargetName { get; set; } = string.Empty;

    [JsonPropertyName("TargetType")] public PortableLogOutputTargetType TargetType { get; set; }

    [JsonPropertyName("TotalAttempts")] public int TotalAttempts { get; set; }

    [JsonPropertyName("SuccessCount")] public int SuccessCount { get; set; }

    [JsonPropertyName("FailureCount")] public int FailureCount { get; set; }

    [JsonPropertyName("AverageResponseTimeMs")] public long AverageResponseTimeMs { get; set; }

    [JsonPropertyName("TotalBytes")] public long TotalBytes { get; set; }

    [JsonPropertyName("LastUpdateTime")] public DateTime LastUpdateTime { get; set; }

    public double SuccessRate => TotalAttempts > 0
        ? SuccessCount * 100.0 / TotalAttempts
        : 0;
}

public sealed class PortableLogOutputTelemetryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _statisticsPath;
    private readonly string _failuresPath;
    private readonly Func<DateTime> _clock;

    public PortableLogOutputTelemetryStore(
        string statisticsPath,
        string failuresPath,
        Func<DateTime>? clock = null)
    {
        _statisticsPath = string.IsNullOrWhiteSpace(statisticsPath)
            ? throw new ArgumentException("Statistics path is required.", nameof(statisticsPath))
            : statisticsPath;
        _failuresPath = string.IsNullOrWhiteSpace(failuresPath)
            ? throw new ArgumentException("Failures path is required.", nameof(failuresPath))
            : failuresPath;
        _clock = clock ?? (() => DateTime.Now);
    }

    public async Task<IReadOnlyList<PortableLogOutputStatistics>> LoadStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        return await LoadListAsync<PortableLogOutputStatistics>(_statisticsPath, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PortableLogOutputFailureRecord>> LoadFailuresAsync(
        CancellationToken cancellationToken = default)
    {
        return await LoadListAsync<PortableLogOutputFailureRecord>(_failuresPath, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task RecordAttemptAsync(
        string targetName,
        PortableLogOutputTargetType targetType,
        bool success,
        long responseTimeMs,
        long bytes,
        CancellationToken cancellationToken = default)
    {
        var stats = (await LoadStatisticsAsync(cancellationToken).ConfigureAwait(false)).ToList();
        var stat = stats.FirstOrDefault(candidate =>
            string.Equals(candidate.TargetName, targetName, StringComparison.OrdinalIgnoreCase));
        if (stat is null)
        {
            stat = new PortableLogOutputStatistics
            {
                TargetName = targetName,
                TargetType = targetType
            };
            stats.Add(stat);
        }

        stat.TargetType = targetType;
        stat.TotalAttempts++;
        if (success)
        {
            stat.SuccessCount++;
        }
        else
        {
            stat.FailureCount++;
        }

        stat.AverageResponseTimeMs =
            (stat.AverageResponseTimeMs * (stat.TotalAttempts - 1) + Math.Max(0, responseTimeMs))
            / stat.TotalAttempts;
        stat.TotalBytes += Math.Max(0, bytes);
        stat.LastUpdateTime = _clock();

        await SaveListAsync(_statisticsPath, stats, cancellationToken).ConfigureAwait(false);
    }

    public async Task RecordFailureAsync(
        string targetName,
        PortableLogOutputTargetType targetType,
        string errorMessage,
        string logContent,
        CancellationToken cancellationToken = default)
    {
        var failures = (await LoadFailuresAsync(cancellationToken).ConfigureAwait(false)).ToList();
        failures.Insert(0, new PortableLogOutputFailureRecord(
            _clock(),
            targetName,
            targetType,
            errorMessage,
            logContent));

        if (failures.Count > 100)
        {
            failures.RemoveRange(100, failures.Count - 100);
        }

        await SaveListAsync(_failuresPath, failures, cancellationToken).ConfigureAwait(false);
    }

    public async Task ApplyDispatchResultAsync(
        PortableLogOutputDispatchResult result,
        IReadOnlyDictionary<string, long>? responseTimesMs = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        foreach (var attempt in result.Attempts)
        {
            var responseTimeMs = 0L;
            responseTimesMs?.TryGetValue(attempt.TargetName, out responseTimeMs);
            await RecordAttemptAsync(
                attempt.TargetName,
                attempt.TargetType,
                attempt.Success,
                responseTimeMs,
                0,
                cancellationToken).ConfigureAwait(false);
        }

        foreach (var failure in result.Failures)
        {
            await RecordFailureAsync(
                failure.TargetName,
                failure.TargetType,
                failure.ErrorMessage,
                failure.LogContent,
                cancellationToken).ConfigureAwait(false);
        }
    }

    public Task ResetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        return SaveListAsync(_statisticsPath, Array.Empty<PortableLogOutputStatistics>(), cancellationToken);
    }

    public Task ClearFailuresAsync(CancellationToken cancellationToken = default)
    {
        return SaveListAsync(_failuresPath, Array.Empty<PortableLogOutputFailureRecord>(), cancellationToken);
    }

    private static async Task<List<T>> LoadListAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions, cancellationToken)
                       .ConfigureAwait(false)
                   ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
    }

    private static async Task SaveListAsync<T>(
        string path,
        IReadOnlyCollection<T> values,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, values, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(tempPath, path, overwrite: true);
    }
}
