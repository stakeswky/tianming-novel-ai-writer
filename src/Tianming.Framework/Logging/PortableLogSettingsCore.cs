using System.Text.RegularExpressions;

namespace TM.Framework.Logging;

public enum PortableLogOutputFormatType
{
    Text,
    Json,
    Xml
}

public enum PortableLogValidationSeverity
{
    Error,
    Warning,
    Info
}

public sealed class PortableLogFormatPreviewOptions
{
    public string FormatTemplate { get; set; } = "[{timestamp}] [{level}] {message}";
    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";
    public PortableLogOutputFormatType OutputFormat { get; set; } = PortableLogOutputFormatType.Text;
}

public sealed class PortableLogTemplateValidationResult
{
    public bool IsValid { get; set; }
    public PortableLogValidationSeverity Severity { get; set; }
    public string Message { get; set; } = string.Empty;
    public int Position { get; set; }
    public string Suggestion { get; set; } = string.Empty;
}

public sealed class PortableLogFormatCore
{
    private static readonly string[] BuiltInFields =
    [
        "timestamp",
        "level",
        "message",
        "caller",
        "threadid",
        "processid",
        "exception"
    ];

    private static readonly Regex PlaceholderRegex = new(@"\{(\w+)(?::[\w\-:\.]+)?\}", RegexOptions.Compiled);

    private readonly Func<DateTime> _clock;

    public PortableLogFormatCore(Func<DateTime>? clock = null)
    {
        _clock = clock ?? (() => DateTime.Now);
    }

    public string GeneratePreview(PortableLogFormatPreviewOptions options)
    {
        var now = _clock();
        var timestampFormat = string.IsNullOrWhiteSpace(options.TimestampFormat)
            ? "yyyy-MM-dd HH:mm:ss.fff"
            : options.TimestampFormat;
        var timestamp = now.ToString(timestampFormat);
        const string level = "INFO";
        const string message = "这是一条示例日志消息";
        const string caller = "LogFormatViewModel.GeneratePreview";
        const string threadId = "1234";
        const string processId = "5678";

        return options.OutputFormat switch
        {
            PortableLogOutputFormatType.Json =>
                $"{{\n  \"timestamp\": \"{timestamp}\",\n  \"level\": \"{level}\",\n  \"message\": \"{message}\"\n}}",
            PortableLogOutputFormatType.Xml =>
                $"<log>\n  <timestamp>{timestamp}</timestamp>\n  <level>{level}</level>\n  <message>{message}</message>\n</log>",
            _ => (string.IsNullOrWhiteSpace(options.FormatTemplate)
                    ? "[{timestamp}] [{level}] {message}"
                    : options.FormatTemplate)
                .Replace("{timestamp}", timestamp, StringComparison.OrdinalIgnoreCase)
                .Replace("{level}", level, StringComparison.OrdinalIgnoreCase)
                .Replace("{message}", message, StringComparison.OrdinalIgnoreCase)
                .Replace("{caller}", caller, StringComparison.OrdinalIgnoreCase)
                .Replace("{threadid}", threadId, StringComparison.OrdinalIgnoreCase)
                .Replace("{processid}", processId, StringComparison.OrdinalIgnoreCase)
                .Replace("{exception}", string.Empty, StringComparison.OrdinalIgnoreCase)
        };
    }

    public List<PortableLogTemplateValidationResult> ValidateTemplate(
        string template,
        IEnumerable<string>? customFields = null)
    {
        template ??= string.Empty;
        var results = new List<PortableLogTemplateValidationResult>();
        var allFields = new HashSet<string>(BuiltInFields, StringComparer.OrdinalIgnoreCase);
        foreach (var field in customFields ?? [])
        {
            if (!string.IsNullOrWhiteSpace(field))
            {
                allFields.Add(field);
            }
        }

        var placeholders = PlaceholderRegex.Matches(template);
        foreach (Match match in placeholders)
        {
            var fieldName = match.Groups[1].Value.ToLowerInvariant();
            if (!allFields.Contains(fieldName))
            {
                results.Add(new PortableLogTemplateValidationResult
                {
                    IsValid = false,
                    Severity = PortableLogValidationSeverity.Error,
                    Message = $"未知字段: {{{fieldName}}}",
                    Position = match.Index,
                    Suggestion = $"可用字段: {string.Join(", ", allFields.OrderBy(field => field, StringComparer.OrdinalIgnoreCase))}"
                });
            }
        }

        var openCount = template.Count(c => c == '{');
        var closeCount = template.Count(c => c == '}');
        if (openCount != closeCount)
        {
            results.Add(new PortableLogTemplateValidationResult
            {
                IsValid = false,
                Severity = PortableLogValidationSeverity.Error,
                Message = "大括号不匹配",
                Suggestion = $"打开: {openCount}, 关闭: {closeCount}"
            });
        }

        if (placeholders.Count > 10)
        {
            results.Add(new PortableLogTemplateValidationResult
            {
                IsValid = true,
                Severity = PortableLogValidationSeverity.Warning,
                Message = "字段数量较多，可能影响性能",
                Suggestion = "考虑减少字段数量或使用更简洁的模板"
            });
        }

        return results;
    }
}

public sealed class PortableLevelChangeRecord
{
    public DateTime Timestamp { get; set; }
    public string Target { get; set; } = string.Empty;
    public LogSeverity OldLevel { get; set; }
    public LogSeverity NewLevel { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
}

public sealed class PortableLogLevelStatistics
{
    public Dictionary<LogSeverity, int> LevelCounts { get; set; } = new();
    public DateTime LastUpdated { get; set; } = DateTime.Now;
    public int TotalLogs { get; set; }

    public void Increment(LogSeverity level)
    {
        LevelCounts[level] = LevelCounts.GetValueOrDefault(level) + 1;
        TotalLogs++;
        LastUpdated = DateTime.Now;
    }

    public double GetLevelPercentage(LogSeverity level)
    {
        return TotalLogs == 0 ? 0 : LevelCounts.GetValueOrDefault(level) * 100.0 / TotalLogs;
    }
}

public sealed class PortableLogLevelStatisticsItem
{
    public LogSeverity Level { get; set; }
    public int Count { get; set; }
    public double Percentage { get; set; }
    public string Color { get; set; } = string.Empty;
}

public sealed class PortableLogLevelCore
{
    private static readonly Dictionary<LogSeverity, string> LevelColors = new()
    {
        [LogSeverity.Trace] = "#808080",
        [LogSeverity.Debug] = "#00BFFF",
        [LogSeverity.Info] = "#00FF00",
        [LogSeverity.Warning] = "#FFA500",
        [LogSeverity.Error] = "#FF0000",
        [LogSeverity.Fatal] = "#8B0000"
    };

    private static readonly Dictionary<string, (string Name, string Description, LogSeverity Global, LogSeverity Minimum)> Presets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Development"] = ("开发环境", "详细调试，全部启用Trace级别", LogSeverity.Trace, LogSeverity.Trace),
            ["Testing"] = ("测试环境", "适度调试，Debug级别", LogSeverity.Debug, LogSeverity.Debug),
            ["Production"] = ("生产环境", "最小日志，Info/Warning级别", LogSeverity.Info, LogSeverity.Warning)
        };

    private readonly Func<DateTime> _clock;
    private readonly Func<string> _userProvider;

    public PortableLogLevelCore(Func<DateTime>? clock = null, Func<string>? userProvider = null)
    {
        _clock = clock ?? (() => DateTime.Now);
        _userProvider = userProvider ?? (() => Environment.UserName);
    }

    public PortableLevelChangeRecord ApplyPreset(PortableLogLevelSettings settings, string presetKey)
    {
        if (!Presets.TryGetValue(presetKey, out var preset))
        {
            throw new ArgumentException($"Unknown log level preset: {presetKey}", nameof(presetKey));
        }

        var oldLevel = settings.GlobalLevel;
        settings.GlobalLevel = preset.Global;
        settings.MinimumLevel = preset.Minimum;
        settings.ModuleLevels.Clear();

        return new PortableLevelChangeRecord
        {
            Timestamp = _clock(),
            Target = $"预设:{preset.Name}",
            OldLevel = oldLevel,
            NewLevel = preset.Global,
            Reason = $"应用{preset.Name}预设",
            User = _userProvider()
        };
    }

    public static IReadOnlyList<PortableLogLevelStatisticsItem> BuildStatistics(PortableLogLevelStatistics statistics)
    {
        return Enum.GetValues<LogSeverity>()
            .Select(level => new PortableLogLevelStatisticsItem
            {
                Level = level,
                Count = statistics.LevelCounts.GetValueOrDefault(level),
                Percentage = statistics.GetLevelPercentage(level),
                Color = LevelColors.GetValueOrDefault(level, "#FFFFFF")
            })
            .ToList();
    }
}

public enum PortableLogStorageStatus
{
    Normal,
    Warning,
    Critical
}

public sealed class PortableLogRotationSettings
{
    public int MaxFileSizeMB { get; set; } = 10;
    public int MaxRetainCount { get; set; } = 1;
    public int MaxRetainDays { get; set; } = 1;
    public int MaxRetainSizeMB { get; set; } = 1024;
    public int CompressAfterDays { get; set; } = 1;
}

public sealed class PortableLogStorageSpaceInfo
{
    public string DrivePath { get; set; } = string.Empty;
    public long TotalSpaceGB { get; set; }
    public long FreeSpaceGB { get; set; }
    public long UsedSpaceGB { get; set; }
    public double UsagePercentage { get; set; }
    public long LogsSpaceMB { get; set; }
    public PortableLogStorageStatus Status { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public DateTime LastChecked { get; set; }
}

public sealed class PortableLogRotationPrediction
{
    public DateTime PredictedNextRotation { get; set; }
    public TimeSpan TimeUntilNextRotation { get; set; }
    public long PredictedFileSizeMB { get; set; }
    public long PredictedStorageUsageMB { get; set; }
    public double AverageDailyGrowthMB { get; set; }
    public string RecommendedAction { get; set; } = string.Empty;
}

public sealed record PortableLogRotationHistory(DateTime RotationTime, long FileSizeBytes);

public sealed record PortableLogFileSnapshot(string Name, DateTime LastWriteTime, long SizeBytes, string Extension);

public sealed class PortableLogCleanupRecommendation
{
    public string Action { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public long EstimatedSpaceToFree { get; set; }
    public int FilesToDelete { get; set; }
    public string Priority { get; set; } = string.Empty;
}

public sealed class PortableLogRotationCore
{
    private readonly Func<DateTime> _clock;

    public PortableLogRotationCore(Func<DateTime>? clock = null)
    {
        _clock = clock ?? (() => DateTime.Now);
    }

    public PortableLogStorageSpaceInfo BuildStorageSpaceInfo(
        string drivePath,
        long totalBytes,
        long freeBytes,
        long logsSizeBytes,
        int warningThresholdPercentage,
        int criticalThresholdPercentage)
    {
        var totalSpaceGB = totalBytes / (1024 * 1024 * 1024);
        var freeSpaceGB = freeBytes / (1024 * 1024 * 1024);
        var usedBytes = Math.Max(0, totalBytes - freeBytes);
        var usagePercentage = totalBytes <= 0 ? 0 : (double)usedBytes / totalBytes * 100;
        var status = usagePercentage < warningThresholdPercentage
            ? PortableLogStorageStatus.Normal
            : usagePercentage < criticalThresholdPercentage
                ? PortableLogStorageStatus.Warning
                : PortableLogStorageStatus.Critical;

        return new PortableLogStorageSpaceInfo
        {
            DrivePath = drivePath,
            TotalSpaceGB = totalSpaceGB,
            FreeSpaceGB = freeSpaceGB,
            UsedSpaceGB = Math.Max(0, totalSpaceGB - freeSpaceGB),
            UsagePercentage = usagePercentage,
            LogsSpaceMB = logsSizeBytes / (1024 * 1024),
            Status = status,
            StatusMessage = status switch
            {
                PortableLogStorageStatus.Normal => "存储空间充足",
                PortableLogStorageStatus.Warning => $"存储空间不足（已使用{usagePercentage:F1}%）",
                _ => $"存储空间严重不足（已使用{usagePercentage:F1}%）！"
            },
            LastChecked = _clock()
        };
    }

    public PortableLogRotationPrediction PredictNextRotation(
        PortableLogRotationSettings settings,
        long currentLogsTotalSizeMB,
        IReadOnlyList<PortableLogRotationHistory> recentHistories)
    {
        if (recentHistories.Count < 2)
        {
            var estimatedNextRotation = _clock().AddDays(1);
            return new PortableLogRotationPrediction
            {
                PredictedNextRotation = estimatedNextRotation,
                TimeUntilNextRotation = estimatedNextRotation - _clock(),
                PredictedFileSizeMB = settings.MaxFileSizeMB,
                PredictedStorageUsageMB = currentLogsTotalSizeMB + settings.MaxFileSizeMB,
                AverageDailyGrowthMB = settings.MaxFileSizeMB,
                RecommendedAction = "数据不足，基于配置估算"
            };
        }

        var ordered = recentHistories.OrderByDescending(history => history.RotationTime).Take(10).ToList();
        var intervals = ordered.Zip(ordered.Skip(1), (current, previous) => current.RotationTime - previous.RotationTime).ToList();
        var averageInterval = TimeSpan.FromTicks((long)intervals.Average(interval => interval.Ticks));
        var predictedNextRotation = _clock() + averageInterval;
        var totalDays = Math.Max((ordered.First().RotationTime - ordered.Last().RotationTime).TotalDays, 0);
        var totalSizeMB = ordered.Sum(history => history.FileSizeBytes) / (1024.0 * 1024.0);
        var averageDailyGrowth = totalDays > 0 ? totalSizeMB / totalDays : 0;
        var predictedStorageUsage = currentLogsTotalSizeMB + (long)(averageDailyGrowth * averageInterval.TotalDays);

        return new PortableLogRotationPrediction
        {
            PredictedNextRotation = predictedNextRotation,
            TimeUntilNextRotation = predictedNextRotation - _clock(),
            PredictedFileSizeMB = settings.MaxFileSizeMB,
            PredictedStorageUsageMB = predictedStorageUsage,
            AverageDailyGrowthMB = averageDailyGrowth,
            RecommendedAction = averageInterval.TotalHours < 24
                ? "建议尽快检查日志设置"
                : predictedStorageUsage > settings.MaxRetainSizeMB * 0.8
                    ? "建议调整保留策略"
                    : "当前设置合理"
        };
    }

    public List<PortableLogCleanupRecommendation> GenerateCleanupRecommendations(
        PortableLogRotationSettings settings,
        IReadOnlyList<PortableLogFileSnapshot> files,
        PortableLogStorageSpaceInfo? storageInfo = null)
    {
        var ordered = files.OrderBy(file => file.LastWriteTime).ToList();
        var recommendations = new List<PortableLogCleanupRecommendation>();

        var expiredFiles = ordered.Where(file => file.LastWriteTime < _clock().AddDays(-settings.MaxRetainDays)).ToList();
        if (expiredFiles.Count > 0)
        {
            recommendations.Add(new PortableLogCleanupRecommendation
            {
                Action = "删除过期日志",
                Reason = $"超过{settings.MaxRetainDays}天的日志文件",
                EstimatedSpaceToFree = expiredFiles.Sum(file => file.SizeBytes) / (1024 * 1024),
                FilesToDelete = expiredFiles.Count,
                Priority = "高"
            });
        }

        var uncompressedOldFiles = ordered
            .Where(file => !file.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase)
                           && file.LastWriteTime < _clock().AddDays(-settings.CompressAfterDays))
            .ToList();
        if (uncompressedOldFiles.Count > 0)
        {
            recommendations.Add(new PortableLogCleanupRecommendation
            {
                Action = "压缩旧日志",
                Reason = $"超过{settings.CompressAfterDays}天的未压缩日志",
                EstimatedSpaceToFree = (long)(uncompressedOldFiles.Sum(file => file.SizeBytes) * 0.7 / (1024 * 1024)),
                FilesToDelete = 0,
                Priority = "中"
            });
        }

        if (ordered.Count > settings.MaxRetainCount)
        {
            var excessFiles = ordered.Count - settings.MaxRetainCount;
            recommendations.Add(new PortableLogCleanupRecommendation
            {
                Action = "减少日志文件数量",
                Reason = $"当前{ordered.Count}个文件，超过限制{settings.MaxRetainCount}个",
                EstimatedSpaceToFree = ordered.Take(excessFiles).Sum(file => file.SizeBytes) / (1024 * 1024),
                FilesToDelete = excessFiles,
                Priority = "中"
            });
        }

        if (storageInfo?.Status == PortableLogStorageStatus.Critical)
        {
            recommendations.Insert(0, new PortableLogCleanupRecommendation
            {
                Action = "紧急清理空间",
                Reason = $"磁盘空间严重不足（{storageInfo.UsagePercentage:F1}%）",
                EstimatedSpaceToFree = ordered.Take(ordered.Count / 2).Sum(file => file.SizeBytes) / (1024 * 1024),
                FilesToDelete = ordered.Count / 2,
                Priority = "紧急"
            });
        }

        return recommendations;
    }
}
