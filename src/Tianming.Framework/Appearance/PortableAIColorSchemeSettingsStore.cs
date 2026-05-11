using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Appearance;

public sealed class PortableAIColorSchemeUserConfig
{
    [JsonPropertyName("LastColorHarmony")] public string LastColorHarmony { get; set; } = "互补色";

    [JsonPropertyName("LastThemeType")] public string LastThemeType { get; set; } = "浅色主题";

    [JsonPropertyName("LastEmotion")] public string LastEmotion { get; set; } = "无";

    [JsonPropertyName("LastScene")] public string LastScene { get; set; } = "通用";

    [JsonPropertyName("LastKeywords")] public string LastKeywords { get; set; } = string.Empty;

    public static PortableAIColorSchemeUserConfig CreateDefault()
    {
        return new PortableAIColorSchemeUserConfig();
    }
}

public sealed class FileAIColorSchemeSettingsStore
{
    private static readonly string[] ColorHarmonies =
    {
        "互补色", "类似色", "三角色", "分裂互补色", "四色", "单色"
    };

    private static readonly string[] ThemeTypes =
    {
        "浅色主题", "暗色主题"
    };

    private static readonly string[] Emotions =
    {
        "无", "平静", "活力", "温暖", "清新", "神秘", "专业", "可爱"
    };

    private static readonly string[] Scenes =
    {
        "通用", "写作创作", "数据分析", "商务办公", "休闲娱乐", "科技感"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public FileAIColorSchemeSettingsStore(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("AI color scheme settings file path is required.", nameof(filePath))
            : filePath;
    }

    public async Task<PortableAIColorSchemeUserConfig> LoadUserConfigAsync(
        CancellationToken cancellationToken = default)
    {
        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        return Normalize(data.UserConfig);
    }

    public async Task SaveUserConfigAsync(
        PortableAIColorSchemeUserConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        await SaveAsync(
            new PortableAIColorSchemeSettingsData { UserConfig = Normalize(config) },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateUserConfigAsync(
        PortableAIColorSchemeUserConfig config,
        CancellationToken cancellationToken = default)
    {
        await SaveUserConfigAsync(config, cancellationToken).ConfigureAwait(false);
    }

    private async Task<PortableAIColorSchemeSettingsData> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new PortableAIColorSchemeSettingsData();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            return await JsonSerializer.DeserializeAsync<PortableAIColorSchemeSettingsData>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false) ?? new PortableAIColorSchemeSettingsData();
        }
        catch (JsonException)
        {
            return new PortableAIColorSchemeSettingsData();
        }
        catch (IOException)
        {
            return new PortableAIColorSchemeSettingsData();
        }
    }

    private async Task SaveAsync(
        PortableAIColorSchemeSettingsData data,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, data, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }

    private static PortableAIColorSchemeUserConfig Normalize(PortableAIColorSchemeUserConfig? config)
    {
        var source = config ?? PortableAIColorSchemeUserConfig.CreateDefault();
        return new PortableAIColorSchemeUserConfig
        {
            LastColorHarmony = NormalizeOption(source.LastColorHarmony, ColorHarmonies, "互补色"),
            LastThemeType = NormalizeOption(source.LastThemeType, ThemeTypes, "浅色主题"),
            LastEmotion = NormalizeOption(source.LastEmotion, Emotions, "无"),
            LastScene = NormalizeOption(source.LastScene, Scenes, "通用"),
            LastKeywords = source.LastKeywords ?? string.Empty
        };
    }

    private static string NormalizeOption(string value, IReadOnlyCollection<string> allowedValues, string fallback)
    {
        return allowedValues.Contains(value) ? value : fallback;
    }

    private sealed class PortableAIColorSchemeSettingsData
    {
        [JsonPropertyName("UserConfig")] public PortableAIColorSchemeUserConfig? UserConfig { get; set; } = new();
    }
}
