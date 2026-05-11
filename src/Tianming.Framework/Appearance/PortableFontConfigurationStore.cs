using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Appearance;

public sealed class PortableFontConfiguration
{
    [JsonPropertyName("uiFont")] public PortableFontSettings UIFont { get; set; } = new();

    [JsonPropertyName("editorFont")] public PortableFontSettings EditorFont { get; set; } = new();

    public static PortableFontConfiguration CreateDefault()
    {
        return new PortableFontConfiguration
        {
            UIFont = new PortableFontSettings
            {
                FontFamily = "Microsoft YaHei UI",
                FontSize = 14,
                FontWeight = "Normal",
                LineHeight = 1.5,
                LetterSpacing = 0
            },
            EditorFont = new PortableFontSettings
            {
                FontFamily = "Consolas",
                FontSize = 13,
                FontWeight = "Normal",
                LineHeight = 1.6,
                LetterSpacing = 0.5
            }
        };
    }
}

public sealed class PortableFontSharePackage
{
    [JsonPropertyName("Version")] public string Version { get; set; } = "1.0";

    [JsonPropertyName("ExportTime")] public DateTime ExportTime { get; set; }

    [JsonPropertyName("ExportBy")] public string ExportBy { get; set; } = string.Empty;

    [JsonPropertyName("Configuration")] public PortableFontConfiguration Configuration { get; set; } = new();
}

public sealed class FileFontConfigurationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly Func<DateTime> _clock;
    private readonly Func<string> _userName;

    public FileFontConfigurationStore(
        string filePath,
        Func<DateTime>? clock = null,
        Func<string>? userName = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Font configuration file path is required.", nameof(filePath))
            : filePath;
        _clock = clock ?? (() => DateTime.Now);
        _userName = userName ?? (() => Environment.UserName);
    }

    public async Task<PortableFontConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return PortableFontConfiguration.CreateDefault();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var config = await JsonSerializer.DeserializeAsync<PortableFontConfiguration>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            return Normalize(config);
        }
        catch (JsonException)
        {
            return PortableFontConfiguration.CreateDefault();
        }
        catch (IOException)
        {
            return PortableFontConfiguration.CreateDefault();
        }
    }

    public async Task SaveAsync(
        PortableFontConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        await WriteJsonAtomicallyAsync(_filePath, Normalize(configuration), cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateUIFontAsync(
        PortableFontSettings uiFont,
        CancellationToken cancellationToken = default)
    {
        var config = await LoadAsync(cancellationToken).ConfigureAwait(false);
        config.UIFont = uiFont ?? throw new ArgumentNullException(nameof(uiFont));
        await SaveAsync(config, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateEditorFontAsync(
        PortableFontSettings editorFont,
        CancellationToken cancellationToken = default)
    {
        var config = await LoadAsync(cancellationToken).ConfigureAwait(false);
        config.EditorFont = editorFont ?? throw new ArgumentNullException(nameof(editorFont));
        await SaveAsync(config, cancellationToken).ConfigureAwait(false);
    }

    public async Task ResetToDefaultAsync(CancellationToken cancellationToken = default)
    {
        await SaveAsync(PortableFontConfiguration.CreateDefault(), cancellationToken).ConfigureAwait(false);
    }

    public async Task ExportConfigurationAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var config = await LoadAsync(cancellationToken).ConfigureAwait(false);
        await WriteJsonAtomicallyAsync(filePath, config, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ImportConfigurationAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        PortableFontConfiguration? config;
        try
        {
            await using var stream = File.OpenRead(filePath);
            config = await JsonSerializer.DeserializeAsync<PortableFontConfiguration>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }

        if (config?.UIFont is null || config.EditorFont is null)
        {
            return false;
        }

        await SaveAsync(config, cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task ExportShareableAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        var package = new PortableFontSharePackage
        {
            Version = "1.0",
            ExportTime = _clock(),
            ExportBy = _userName(),
            Configuration = await LoadAsync(cancellationToken).ConfigureAwait(false)
        };

        await WriteJsonAtomicallyAsync(filePath, package, cancellationToken).ConfigureAwait(false);
    }

    private static PortableFontConfiguration Normalize(PortableFontConfiguration? config)
    {
        if (config?.UIFont is null || config.EditorFont is null)
        {
            return PortableFontConfiguration.CreateDefault();
        }

        return config;
    }

    private static async Task WriteJsonAtomicallyAsync<T>(
        string filePath,
        T value,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, filePath, overwrite: true);
    }
}
