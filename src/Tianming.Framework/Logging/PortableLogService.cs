using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Framework.Logging;

public enum LogSeverity
{
    Trace = 0,
    Debug = 1,
    Info = 2,
    Warning = 3,
    Error = 4,
    Fatal = 5
}

public sealed class PortableLogLevelSettings
{
    public LogSeverity GlobalLevel { get; set; } = LogSeverity.Info;
    public LogSeverity MinimumLevel { get; set; } = LogSeverity.Trace;
    public Dictionary<string, LogSeverity> ModuleLevels { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class PortableLogFormatSettings
{
    public string FormatTemplate { get; set; } = "[{timestamp}] [{level}] {message}";
    public string TimestampFormat { get; set; } = "yyyy-MM-dd HH:mm:ss.fff";
}

public sealed class PortableLogOutputSettings
{
    [JsonPropertyName("EnableFileOutput")] public bool EnableFileOutput { get; set; } = true;
    [JsonPropertyName("FileOutputPath")] public string FileOutputPath { get; set; } = "Logs/application.log";
    [JsonPropertyName("FileNamingPattern")] public string FileNamingPattern { get; set; } = "{date}_{appname}.log";
    [JsonPropertyName("FileEncoding")] public PortableLogFileEncodingType FileEncoding { get; set; } = PortableLogFileEncodingType.UTF8;
    [JsonPropertyName("EnableConsoleOutput")] public bool EnableConsoleOutput { get; set; } = true;
    [JsonPropertyName("ConsoleColorCoding")] public bool ConsoleColorCoding { get; set; } = true;
    [JsonPropertyName("EnableEventLog")] public bool EnableEventLog { get; set; }
    [JsonPropertyName("EventLogSource")] public string EventLogSource { get; set; } = "TM";
    [JsonPropertyName("EnableRemoteOutput")] public bool EnableRemoteOutput { get; set; }
    [JsonPropertyName("RemoteProtocol")] public string RemoteProtocol { get; set; } = "HTTP";
    [JsonPropertyName("RemoteAddress")] public string RemoteAddress { get; set; } = "http://localhost:8080/log";
    [JsonPropertyName("BufferSize")] public int BufferSize { get; set; } = 1024;
    [JsonPropertyName("EnableAsyncOutput")] public bool EnableAsyncOutput { get; set; } = true;
    [JsonPropertyName("OutputTargets")] public List<PortableLogOutputTarget> OutputTargets { get; set; } = new();
    [JsonPropertyName("RetryConfiguration")] public PortableLogRetryConfig RetryConfiguration { get; set; } = new();

    public static PortableLogOutputSettings CreateDefault()
    {
        return new PortableLogOutputSettings();
    }

    public PortableLogOutputSettings Clone()
    {
        return new PortableLogOutputSettings
        {
            EnableFileOutput = EnableFileOutput,
            FileOutputPath = FileOutputPath,
            FileNamingPattern = FileNamingPattern,
            FileEncoding = FileEncoding,
            EnableConsoleOutput = EnableConsoleOutput,
            ConsoleColorCoding = ConsoleColorCoding,
            EnableEventLog = EnableEventLog,
            EventLogSource = EventLogSource,
            EnableRemoteOutput = EnableRemoteOutput,
            RemoteProtocol = RemoteProtocol,
            RemoteAddress = RemoteAddress,
            BufferSize = BufferSize,
            EnableAsyncOutput = EnableAsyncOutput,
            OutputTargets = OutputTargets.Select(CloneTarget).ToList(),
            RetryConfiguration = RetryConfiguration.Clone()
        };
    }

    private static PortableLogOutputTarget CloneTarget(PortableLogOutputTarget target)
    {
        return new PortableLogOutputTarget
        {
            Name = target.Name,
            Type = target.Type,
            IsEnabled = target.IsEnabled,
            Priority = target.Priority,
            Settings = new Dictionary<string, string>(target.Settings, StringComparer.OrdinalIgnoreCase)
        };
    }
}

public enum PortableLogFileEncodingType
{
    UTF8,
    UTF8BOM,
    ASCII,
    Unicode
}

public sealed class PortableLogRetryConfig
{
    [JsonPropertyName("EnableRetry")] public bool EnableRetry { get; set; } = true;
    [JsonPropertyName("MaxRetryAttempts")] public int MaxRetryAttempts { get; set; } = 3;
    [JsonPropertyName("RetryIntervalMs")] public int RetryIntervalMs { get; set; } = 1000;
    [JsonPropertyName("EnableExponentialBackoff")] public bool EnableExponentialBackoff { get; set; } = true;
    [JsonPropertyName("MaxRetryIntervalMs")] public int MaxRetryIntervalMs { get; set; } = 10000;

    public PortableLogRetryConfig Clone()
    {
        return new PortableLogRetryConfig
        {
            EnableRetry = EnableRetry,
            MaxRetryAttempts = MaxRetryAttempts,
            RetryIntervalMs = RetryIntervalMs,
            EnableExponentialBackoff = EnableExponentialBackoff,
            MaxRetryIntervalMs = MaxRetryIntervalMs
        };
    }
}

public sealed class PortableLogOptions
{
    public string StorageRoot { get; set; } = Directory.GetCurrentDirectory();
    public Func<DateTime> Clock { get; set; } = () => DateTime.Now;
    public PortableLogLevelSettings LevelSettings { get; set; } = new();
    public PortableLogFormatSettings FormatSettings { get; set; } = new();
    public PortableLogOutputSettings OutputSettings { get; set; } = new();
}

public sealed record PortableLogEntry(LogSeverity Level, string? Module, string Message, Exception? Exception = null);

public sealed class PortableLogService
{
    private static readonly Regex TimestampRegex = new(@"\{timestamp(?::([^}]+))?\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LevelRegex = new(@"\{level(?::([^}]+))?\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex MessageRegex = new(@"\{message\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CallerRegex = new(@"\{caller\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ThreadIdRegex = new(@"\{threadid\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ProcessIdRegex = new(@"\{processid\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ExceptionRegex = new(@"\{exception\}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ModuleParseRegex = new(@"^\[(?<module>[^\]]+)\]\s*(?<msg>.*)$", RegexOptions.Compiled);

    private readonly PortableLogOptions _options;

    public PortableLogService(PortableLogOptions? options = null)
    {
        _options = options ?? new PortableLogOptions();
    }

    public static PortableLogEntry ParseEntry(string message)
    {
        var (module, parsedMessage) = TryParseModule(message);
        var level = GuessLevel(parsedMessage);
        if (level == LogSeverity.Info && string.IsNullOrWhiteSpace(module))
        {
            level = LogSeverity.Debug;
        }

        return new PortableLogEntry(level, module, parsedMessage);
    }

    public static bool ShouldWrite(LogSeverity level, string? module, PortableLogLevelSettings settings)
    {
        var threshold = settings.GlobalLevel;
        if (!string.IsNullOrWhiteSpace(module) && settings.ModuleLevels.TryGetValue(module, out var moduleLevel))
        {
            threshold = moduleLevel;
        }

        if (threshold < settings.MinimumLevel)
        {
            threshold = settings.MinimumLevel;
        }

        return level >= threshold;
    }

    public string Format(PortableLogEntry entry)
    {
        var settings = _options.FormatSettings;
        var template = string.IsNullOrWhiteSpace(settings.FormatTemplate)
            ? "[{timestamp}] [{level}] {message}"
            : settings.FormatTemplate;
        var now = _options.Clock();

        template = TimestampRegex.Replace(template, match =>
        {
            var format = match.Groups[1].Success ? match.Groups[1].Value : settings.TimestampFormat;
            if (string.IsNullOrWhiteSpace(format))
            {
                format = "yyyy-MM-dd HH:mm:ss.fff";
            }
            return now.ToString(format);
        });

        template = LevelRegex.Replace(template, match =>
        {
            var format = match.Groups[1].Success ? match.Groups[1].Value : string.Empty;
            return string.Equals(format, "short", StringComparison.OrdinalIgnoreCase)
                ? ToShortLevel(entry.Level)
                : entry.Level.ToString().ToUpperInvariant();
        });

        template = MessageRegex.Replace(template, _ => entry.Message ?? string.Empty);
        template = CallerRegex.Replace(template, _ => entry.Module ?? string.Empty);
        template = ThreadIdRegex.Replace(template, _ => Environment.CurrentManagedThreadId.ToString());
        template = ProcessIdRegex.Replace(template, _ => Environment.ProcessId.ToString());
        template = ExceptionRegex.Replace(template, _ => entry.Exception?.ToString() ?? string.Empty);

        return template;
    }

    public async Task<bool> LogAsync(string message, CancellationToken cancellationToken = default)
    {
        var entry = ParseEntry(message);
        if (!ShouldWrite(entry.Level, entry.Module, _options.LevelSettings))
        {
            return false;
        }

        if (!_options.OutputSettings.EnableFileOutput)
        {
            return true;
        }

        var path = ResolveLogFilePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.AppendAllTextAsync(path, Format(entry) + Environment.NewLine, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private string ResolveLogFilePath()
    {
        var configuredPath = _options.OutputSettings.FileOutputPath;
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            configuredPath = "Logs/application.log";
        }

        var directory = Path.GetDirectoryName(configuredPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = "Logs";
        }

        if (!Path.IsPathRooted(directory))
        {
            directory = Path.Combine(_options.StorageRoot, directory);
        }

        var pattern = _options.OutputSettings.FileNamingPattern;
        if (string.IsNullOrWhiteSpace(pattern))
        {
            pattern = "{date}_{appname}.log";
        }

        var fileName = pattern
            .Replace("{date}", _options.Clock().ToString("yyyy-MM-dd"), StringComparison.Ordinal)
            .Replace("{appname}", "天命", StringComparison.Ordinal);

        return Path.Combine(directory, fileName);
    }

    private static (string? Module, string Message) TryParseModule(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return (null, string.Empty);
        }

        var match = ModuleParseRegex.Match(message);
        if (!match.Success)
        {
            return (null, message);
        }

        return (match.Groups["module"].Value.Trim(), match.Groups["msg"].Value);
    }

    private static LogSeverity GuessLevel(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return LogSeverity.Info;
        }

        if (message.Contains("Fatal", StringComparison.OrdinalIgnoreCase) || message.Contains("致命", StringComparison.OrdinalIgnoreCase))
        {
            return LogSeverity.Fatal;
        }

        if (message.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("失败", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("错误", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("异常", StringComparison.OrdinalIgnoreCase))
        {
            return LogSeverity.Error;
        }

        if (message.Contains("Warn", StringComparison.OrdinalIgnoreCase) || message.Contains("警告", StringComparison.OrdinalIgnoreCase))
        {
            return LogSeverity.Warning;
        }

        if (message.Contains("初始化完成", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("清理完成", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("数据已加载", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("数据已保存", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("任务完成", StringComparison.OrdinalIgnoreCase))
        {
            return LogSeverity.Debug;
        }

        if ((message.Contains("-> 200", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("-> 201", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("-> 204", StringComparison.OrdinalIgnoreCase)) &&
            (message.Contains("GET ", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("POST ", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("PUT ", StringComparison.OrdinalIgnoreCase) ||
             message.Contains("DELETE ", StringComparison.OrdinalIgnoreCase)))
        {
            return LogSeverity.Debug;
        }

        if (message.Contains("Debug", StringComparison.OrdinalIgnoreCase))
        {
            return LogSeverity.Debug;
        }

        if (message.Contains("Trace", StringComparison.OrdinalIgnoreCase))
        {
            return LogSeverity.Trace;
        }

        return LogSeverity.Info;
    }

    private static string ToShortLevel(LogSeverity level) =>
        level switch
        {
            LogSeverity.Trace => "TRC",
            LogSeverity.Debug => "DBG",
            LogSeverity.Info => "INF",
            LogSeverity.Warning => "WRN",
            LogSeverity.Error => "ERR",
            LogSeverity.Fatal => "FTL",
            _ => level.ToString().ToUpperInvariant()
        };
}
