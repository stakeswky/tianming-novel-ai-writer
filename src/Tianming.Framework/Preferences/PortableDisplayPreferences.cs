using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Preferences;

public enum PortableListDensity
{
    Compact,
    Standard,
    Comfortable
}

public sealed class PortableDisplaySettings
{
    [JsonPropertyName("ShowFunctionBar")]
    public bool ShowFunctionBar { get; set; } = true;

    [JsonPropertyName("ListDensity")]
    public PortableListDensity ListDensity { get; set; } = PortableListDensity.Standard;

    [JsonPropertyName("LastModified")]
    public DateTime LastModified { get; set; } = DateTime.Now;

    public static PortableDisplaySettings CreateDefault(DateTime? lastModified = null)
    {
        return new PortableDisplaySettings
        {
            ShowFunctionBar = true,
            ListDensity = PortableListDensity.Standard,
            LastModified = lastModified ?? DateTime.Now
        };
    }

    public PortableDisplaySettings Clone()
    {
        return new PortableDisplaySettings
        {
            ShowFunctionBar = ShowFunctionBar,
            ListDensity = ListDensity,
            LastModified = LastModified
        };
    }

    public void CopyFrom(PortableDisplaySettings other)
    {
        ShowFunctionBar = other.ShowFunctionBar;
        ListDensity = other.ListDensity;
        LastModified = other.LastModified;
    }
}

public sealed class FileDisplaySettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _path;
    private readonly Func<DateTime> _clock;

    public FileDisplaySettingsStore(string path, Func<DateTime>? clock = null)
    {
        _path = path;
        _clock = clock ?? (() => DateTime.Now);
    }

    public async Task<PortableDisplaySettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_path))
                return PortableDisplaySettings.CreateDefault(_clock());

            await using var stream = File.OpenRead(_path);
            var settings = await JsonSerializer.DeserializeAsync<PortableDisplaySettings>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            return settings ?? PortableDisplaySettings.CreateDefault(_clock());
        }
        catch (JsonException)
        {
            return PortableDisplaySettings.CreateDefault(_clock());
        }
        catch (IOException)
        {
            return PortableDisplaySettings.CreateDefault(_clock());
        }
        catch (UnauthorizedAccessException)
        {
            return PortableDisplaySettings.CreateDefault(_clock());
        }
    }

    public async Task SaveAsync(PortableDisplaySettings settings, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        settings.LastModified = _clock();
        var tempPath = _path + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, settings, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, _path, overwrite: true);
    }
}

public sealed class PortableDisplayController
{
    private readonly PortableDisplaySettings _settings;
    private readonly Func<DateTime> _clock;

    public PortableDisplayController(PortableDisplaySettings settings, Func<DateTime>? clock = null)
    {
        _settings = settings;
        _clock = clock ?? (() => DateTime.Now);
        UiScalePercent = 100;
    }

    public int UiScalePercent { get; private set; }

    public void UpdateUiScale(double scale)
    {
        UiScalePercent = ClampUiScalePercent(scale);
    }

    public void UpdateShowFunctionBar(bool show)
    {
        _settings.ShowFunctionBar = show;
        _settings.LastModified = _clock();
    }

    public void UpdateListDensity(PortableListDensity density)
    {
        _settings.ListDensity = density;
        _settings.LastModified = _clock();
    }

    public void ResetToDefaults()
    {
        _settings.CopyFrom(PortableDisplaySettings.CreateDefault(_clock()));
        UiScalePercent = 100;
    }

    public static PortableDisplayApplyPlan BuildApplyPlan(PortableDisplaySettings settings, double uiScale)
    {
        var percent = ClampUiScalePercent(uiScale);
        return new PortableDisplayApplyPlan(
            settings.ShowFunctionBar,
            settings.ListDensity,
            percent,
            GetDensityDescription(settings.ListDensity));
    }

    public static string GetDensityDescription(PortableListDensity density)
    {
        return density switch
        {
            PortableListDensity.Compact => "紧凑 - 显示更多内容",
            PortableListDensity.Comfortable => "宽松 - 更舒适的阅读体验",
            _ => "标准 - 平衡的显示效果"
        };
    }

    private static int ClampUiScalePercent(double scale)
    {
        var clamped = Math.Max(0.8, Math.Min(2.0, scale));
        return (int)Math.Round(clamped * 100.0);
    }
}

public sealed record PortableDisplayApplyPlan(
    bool ShowFunctionBar,
    PortableListDensity ListDensity,
    int UiScalePercent,
    string DensityDescription)
{
    public string UiScalePercentText => $"{UiScalePercent}%";
}
