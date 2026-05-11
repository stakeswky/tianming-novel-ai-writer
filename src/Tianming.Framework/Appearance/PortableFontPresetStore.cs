using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Appearance;

public sealed class PortableFontSettings
{
    [JsonPropertyName("fontFamily")] public string FontFamily { get; set; } = "Microsoft YaHei UI";
    [JsonPropertyName("fontSize")] public double FontSize { get; set; } = 14;
    [JsonPropertyName("fontWeight")] public string FontWeight { get; set; } = "Normal";
    [JsonPropertyName("lineHeight")] public double LineHeight { get; set; } = 1.5;
    [JsonPropertyName("letterSpacing")] public double LetterSpacing { get; set; }
    [JsonPropertyName("textRenderingMode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PortableTextRenderingMode TextRendering { get; set; } = PortableTextRenderingMode.Auto;
    [JsonPropertyName("textFormattingMode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PortableTextFormattingMode TextFormatting { get; set; } = PortableTextFormattingMode.Ideal;
    [JsonPropertyName("textHintingMode")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PortableTextHintingMode TextHinting { get; set; } = PortableTextHintingMode.Auto;
    [JsonPropertyName("enableLigatures")] public bool EnableLigatures { get; set; }
    [JsonPropertyName("showZeroWidthChars")] public bool ShowZeroWidthChars { get; set; }
    [JsonPropertyName("visualizeWhitespace")] public bool VisualizeWhitespace { get; set; }
    [JsonPropertyName("tabSymbol")] public string TabSymbol { get; set; } = "->";
    [JsonPropertyName("spaceSymbol")] public string SpaceSymbol { get; set; } = ".";
}

public enum PortableTextRenderingMode
{
    Auto,
    Aliased,
    Grayscale,
    ClearType
}

public enum PortableTextFormattingMode
{
    Ideal,
    Display
}

public enum PortableTextHintingMode
{
    Auto,
    Fixed
}

public sealed class PortableFontPreset
{
    [JsonPropertyName("Name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("Description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("FontFamily")] public string FontFamily { get; set; } = "Microsoft YaHei UI";
    [JsonPropertyName("FontSize")] public double FontSize { get; set; } = 14;
    [JsonPropertyName("FontWeight")] public string FontWeight { get; set; } = "Normal";
    [JsonPropertyName("LineHeight")] public double LineHeight { get; set; } = 1.5;
    [JsonPropertyName("LetterSpacing")] public double LetterSpacing { get; set; }
    [JsonPropertyName("IsBuiltIn")] public bool IsBuiltIn { get; set; }
}

public sealed class PortableFontPresetImportResult
{
    public int ImportedCount { get; init; }
    public int SkippedCount { get; init; }
}

public sealed class FileFontPresetStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly IReadOnlyList<PortableFontPreset> _builtInPresets;

    public FileFontPresetStore(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Font preset file path is required.", nameof(filePath))
            : filePath;
        _builtInPresets = CreateBuiltInPresets();
    }

    public async Task<IReadOnlyList<PortableFontPreset>> GetAllPresetsAsync(
        CancellationToken cancellationToken = default)
    {
        var custom = await GetCustomPresetsAsync(cancellationToken).ConfigureAwait(false);
        return _builtInPresets.Concat(custom).ToList();
    }

    public Task<IReadOnlyList<PortableFontPreset>> GetBuiltInPresetsAsync()
    {
        return Task.FromResult(_builtInPresets);
    }

    public async Task<IReadOnlyList<PortableFontPreset>> GetCustomPresetsAsync(
        CancellationToken cancellationToken = default)
    {
        return await LoadCustomPresetsAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveAsPresetAsync(
        string name,
        string description,
        PortableFontSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Preset name is required.", nameof(name));
        }

        var customPresets = await LoadCustomPresetsAsync(cancellationToken).ConfigureAwait(false);
        var existing = customPresets.FirstOrDefault(preset => preset.Name == name);
        if (existing is null)
        {
            customPresets.Add(CreatePreset(name, description, settings));
        }
        else
        {
            existing.Description = description;
            existing.FontFamily = settings.FontFamily;
            existing.FontSize = settings.FontSize;
            existing.FontWeight = settings.FontWeight;
            existing.LineHeight = settings.LineHeight;
            existing.LetterSpacing = settings.LetterSpacing;
            existing.IsBuiltIn = false;
        }

        await SaveCustomPresetsAsync(customPresets, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> DeletePresetAsync(string name, CancellationToken cancellationToken = default)
    {
        if (_builtInPresets.Any(preset => preset.Name == name))
        {
            return false;
        }

        var customPresets = await LoadCustomPresetsAsync(cancellationToken).ConfigureAwait(false);
        var removed = customPresets.RemoveAll(preset => preset.Name == name) > 0;
        if (removed)
        {
            await SaveCustomPresetsAsync(customPresets, cancellationToken).ConfigureAwait(false);
        }

        return removed;
    }

    public async Task<PortableFontPresetImportResult> ImportPresetsAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return new PortableFontPresetImportResult();
        }

        List<PortableFontPreset>? importedPresets;
        try
        {
            await using var stream = File.OpenRead(filePath);
            importedPresets = await JsonSerializer.DeserializeAsync<List<PortableFontPreset>>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            return new PortableFontPresetImportResult();
        }

        if (importedPresets is null || importedPresets.Count == 0)
        {
            return new PortableFontPresetImportResult();
        }

        var customPresets = await LoadCustomPresetsAsync(cancellationToken).ConfigureAwait(false);
        var imported = 0;
        var skipped = 0;
        foreach (var preset in importedPresets)
        {
            if (string.IsNullOrWhiteSpace(preset.Name) || customPresets.Any(existing => existing.Name == preset.Name))
            {
                skipped++;
                continue;
            }

            preset.IsBuiltIn = false;
            customPresets.Add(preset);
            imported++;
        }

        if (imported > 0)
        {
            await SaveCustomPresetsAsync(customPresets, cancellationToken).ConfigureAwait(false);
        }

        return new PortableFontPresetImportResult
        {
            ImportedCount = imported,
            SkippedCount = skipped
        };
    }

    public async Task ExportPresetsAsync(
        string filePath,
        IReadOnlyList<PortableFontPreset> presets,
        CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, presets, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, filePath, overwrite: true);
    }

    public static PortableFontSettings ApplyPreset(PortableFontPreset preset)
    {
        return new PortableFontSettings
        {
            FontFamily = preset.FontFamily,
            FontSize = preset.FontSize,
            FontWeight = preset.FontWeight,
            LineHeight = preset.LineHeight,
            LetterSpacing = preset.LetterSpacing
        };
    }

    private async Task<List<PortableFontPreset>> LoadCustomPresetsAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return [];
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<List<PortableFontPreset>>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false) ?? [];
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

    private async Task SaveCustomPresetsAsync(
        List<PortableFontPreset> customPresets,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        foreach (var preset in customPresets)
        {
            preset.IsBuiltIn = false;
        }

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, customPresets, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }

    private static PortableFontPreset CreatePreset(
        string name,
        string description,
        PortableFontSettings settings)
    {
        return new PortableFontPreset
        {
            Name = name,
            Description = description,
            FontFamily = settings.FontFamily,
            FontSize = settings.FontSize,
            FontWeight = settings.FontWeight,
            LineHeight = settings.LineHeight,
            LetterSpacing = settings.LetterSpacing,
            IsBuiltIn = false
        };
    }

    private static IReadOnlyList<PortableFontPreset> CreateBuiltInPresets()
    {
        return
        [
            new PortableFontPreset
            {
                Name = "办公场景",
                Description = "适合日常办公和文档编辑",
                FontFamily = "Microsoft YaHei UI",
                FontSize = 14,
                FontWeight = "Normal",
                LineHeight = 1.5,
                LetterSpacing = 0,
                IsBuiltIn = true
            },
            new PortableFontPreset
            {
                Name = "阅读场景",
                Description = "适合长时间阅读,减轻眼睛疲劳",
                FontFamily = "宋体",
                FontSize = 16,
                FontWeight = "Light",
                LineHeight = 1.8,
                LetterSpacing = 0.2,
                IsBuiltIn = true
            },
            new PortableFontPreset
            {
                Name = "设计场景",
                Description = "适合UI设计和创作工作",
                FontFamily = "苹方",
                FontSize = 13,
                FontWeight = "Medium",
                LineHeight = 1.6,
                LetterSpacing = 0,
                IsBuiltIn = true
            },
            new PortableFontPreset
            {
                Name = "极简场景",
                Description = "简洁明快,适合专注工作",
                FontFamily = "Segoe UI",
                FontSize = 12,
                FontWeight = "Light",
                LineHeight = 1.4,
                LetterSpacing = 0,
                IsBuiltIn = true
            },
            new PortableFontPreset
            {
                Name = "演示场景",
                Description = "适合屏幕演示和投影展示",
                FontFamily = "Microsoft YaHei UI",
                FontSize = 18,
                FontWeight = "SemiBold",
                LineHeight = 1.6,
                LetterSpacing = 0.3,
                IsBuiltIn = true
            }
        ];
    }
}
