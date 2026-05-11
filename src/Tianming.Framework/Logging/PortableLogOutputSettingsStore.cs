using System.Text.Json;

namespace TM.Framework.Logging;

public sealed class FileLogOutputSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public FileLogOutputSettingsStore(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Log output settings file path is required.", nameof(filePath))
            : filePath;
    }

    public async Task<PortableLogOutputSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return PortableLogOutputSettings.CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<PortableLogOutputSettings>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false) ?? PortableLogOutputSettings.CreateDefault();
        }
        catch (JsonException)
        {
            return PortableLogOutputSettings.CreateDefault();
        }
        catch (IOException)
        {
            return PortableLogOutputSettings.CreateDefault();
        }
    }

    public async Task SaveAsync(
        PortableLogOutputSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings.Clone(), JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }
}

public static class PortableLogOutputTargetBuilder
{
    public static IReadOnlyList<PortableLogOutputTarget> BuildTargets(PortableLogOutputSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var targets = new List<PortableLogOutputTarget>();

        if (settings.EnableFileOutput)
        {
            targets.Add(new PortableLogOutputTarget
            {
                Name = "file",
                Type = PortableLogOutputTargetType.File,
                IsEnabled = true,
                Priority = 0,
                Settings = { ["Path"] = settings.FileOutputPath }
            });
        }

        if (settings.EnableConsoleOutput)
        {
            targets.Add(new PortableLogOutputTarget
            {
                Name = "console",
                Type = PortableLogOutputTargetType.Console,
                IsEnabled = true,
                Priority = 10
            });
        }

        if (settings.EnableEventLog)
        {
            targets.Add(new PortableLogOutputTarget
            {
                Name = "event-log",
                Type = PortableLogOutputTargetType.EventLog,
                IsEnabled = true,
                Priority = 20,
                Settings = { ["Tag"] = settings.EventLogSource }
            });
        }

        if (settings.EnableRemoteOutput)
        {
            targets.Add(new PortableLogOutputTarget
            {
                Name = "remote",
                Type = ResolveRemoteTargetType(settings.RemoteProtocol),
                IsEnabled = true,
                Priority = 30,
                Settings = { ["Address"] = settings.RemoteAddress }
            });
        }

        foreach (var target in settings.OutputTargets)
        {
            targets.Add(new PortableLogOutputTarget
            {
                Name = target.Name,
                Type = target.Type,
                IsEnabled = target.IsEnabled,
                Priority = target.Priority,
                Settings = new Dictionary<string, string>(target.Settings, StringComparer.OrdinalIgnoreCase)
            });
        }

        return targets
            .OrderBy(target => target.Priority)
            .ToList();
    }

    private static PortableLogOutputTargetType ResolveRemoteTargetType(string protocol)
    {
        return string.Equals(protocol, "TCP", StringComparison.OrdinalIgnoreCase)
            ? PortableLogOutputTargetType.RemoteTcp
            : PortableLogOutputTargetType.RemoteHttp;
    }
}
