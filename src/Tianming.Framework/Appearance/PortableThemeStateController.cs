using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Appearance;

public sealed class PortableThemeState
{
    [JsonPropertyName("CurrentTheme")] public PortableThemeType CurrentTheme { get; set; } = PortableThemeType.Light;

    [JsonPropertyName("CurrentThemeFileName")] public string? CurrentThemeFileName { get; set; }

    public PortableThemeState Clone()
    {
        return new PortableThemeState
        {
            CurrentTheme = CurrentTheme,
            CurrentThemeFileName = CurrentThemeFileName
        };
    }
}

public sealed record PortableThemeApplicationRequest(
    PortableThemeApplicationPlan Plan,
    IReadOnlyDictionary<string, string> Brushes);

public sealed record PortableThemeChangedEventArgs(
    PortableThemeType OldTheme,
    PortableThemeType NewTheme,
    string? OldThemeFileName,
    string? NewThemeFileName);

public sealed record PortableThemeApplyResult(
    bool Applied,
    PortableThemeApplicationPlan? Plan,
    string Message);

public sealed class FileThemeStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public FileThemeStateStore(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Theme state file path is required.", nameof(filePath))
            : filePath;
    }

    public async Task<PortableThemeState> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new PortableThemeState();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var state = await JsonSerializer.DeserializeAsync<PortableThemeState>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            return Normalize(state);
        }
        catch (JsonException)
        {
            return new PortableThemeState();
        }
        catch (IOException)
        {
            return new PortableThemeState();
        }
    }

    public async Task SaveAsync(PortableThemeState state, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, Normalize(state), JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }

    private static PortableThemeState Normalize(PortableThemeState? state)
    {
        if (state is null)
        {
            return new PortableThemeState();
        }

        if (state.CurrentTheme != PortableThemeType.Custom)
        {
            state.CurrentThemeFileName = null;
        }

        return state;
    }
}

public sealed class PortableThemeStateController
{
    private readonly PortableThemeState _state;
    private readonly Func<PortableThemeApplicationRequest, Task> _applyThemeAsync;
    private readonly Func<PortableThemeState, CancellationToken, Task> _saveStateAsync;
    private readonly string? _customThemeDirectory;

    public PortableThemeStateController(
        PortableThemeState state,
        Func<PortableThemeApplicationRequest, Task> applyThemeAsync,
        Func<PortableThemeState, CancellationToken, Task>? saveStateAsync = null,
        string? customThemeDirectory = null)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _applyThemeAsync = applyThemeAsync ?? throw new ArgumentNullException(nameof(applyThemeAsync));
        _saveStateAsync = saveStateAsync ?? ((_, _) => Task.CompletedTask);
        _customThemeDirectory = customThemeDirectory;
    }

    public event EventHandler<PortableThemeChangedEventArgs>? ThemeChanged;

    public PortableThemeType CurrentTheme => _state.CurrentTheme;

    public string? CurrentThemeFileName => _state.CurrentThemeFileName;

    public Task<PortableThemeApplyResult> ApplyThemeAsync(
        string themeNameOrFileName,
        CancellationToken cancellationToken = default)
    {
        if (PortableThemeApplicationPlanner.TryParseTheme(themeNameOrFileName, out var theme)
            && theme != PortableThemeType.Custom)
        {
            return SwitchThemeAsync(theme, cancellationToken: cancellationToken);
        }

        return ApplyThemeFromFileAsync(themeNameOrFileName, cancellationToken);
    }

    public async Task<PortableThemeApplyResult> SwitchThemeAsync(
        PortableThemeType theme,
        PortableSystemThemeSnapshot? systemSnapshot = null,
        CancellationToken cancellationToken = default)
    {
        var plan = PortableThemeApplicationPlanner.CreateBuiltInPlan(theme, systemSnapshot);
        if (_state.CurrentTheme == plan.ThemeType && _state.CurrentThemeFileName is null)
        {
            return new PortableThemeApplyResult(false, plan, "主题未变更");
        }

        var previous = _state.Clone();
        var brushes = PortableThemeResourcePalette.GetBrushes(plan.ThemeType, systemSnapshot);
        await _applyThemeAsync(new PortableThemeApplicationRequest(plan, brushes)).ConfigureAwait(false);

        _state.CurrentTheme = plan.ThemeType;
        _state.CurrentThemeFileName = null;
        await _saveStateAsync(_state.Clone(), cancellationToken).ConfigureAwait(false);
        RaiseThemeChanged(previous, _state);

        return new PortableThemeApplyResult(true, plan, "主题已切换");
    }

    public async Task<PortableThemeApplyResult> ApplyThemeFromFileAsync(
        string themeFileName,
        CancellationToken cancellationToken = default)
    {
        var plan = PortableThemeApplicationPlanner.CreateCustomPlan(themeFileName);
        if (_state.CurrentTheme == PortableThemeType.Custom
            && string.Equals(_state.CurrentThemeFileName, plan.ResourceFileName, StringComparison.OrdinalIgnoreCase))
        {
            return new PortableThemeApplyResult(false, plan, "主题未变更");
        }

        var previous = _state.Clone();
        var paletteLoad = await LoadCustomThemeBrushesAsync(plan.ResourceFileName, cancellationToken).ConfigureAwait(false);
        if (!paletteLoad.Success)
        {
            return new PortableThemeApplyResult(
                false,
                plan,
                paletteLoad.ErrorMessage ?? "自定义主题文件加载失败");
        }

        await _applyThemeAsync(new PortableThemeApplicationRequest(plan, paletteLoad.Brushes)).ConfigureAwait(false);

        _state.CurrentTheme = PortableThemeType.Custom;
        _state.CurrentThemeFileName = plan.ResourceFileName;
        await _saveStateAsync(_state.Clone(), cancellationToken).ConfigureAwait(false);
        RaiseThemeChanged(previous, _state);

        return new PortableThemeApplyResult(true, plan, "主题已切换");
    }

    private async Task<PortableThemeFilePaletteLoadResult> LoadCustomThemeBrushesAsync(
        string resourceFileName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_customThemeDirectory))
        {
            return new PortableThemeFilePaletteLoadResult
            {
                Success = true,
                FileName = resourceFileName,
                Brushes = new Dictionary<string, string>()
            };
        }

        var path = Path.Combine(_customThemeDirectory, resourceFileName);
        return await PortableThemeFilePaletteLoader.LoadAsync(path, cancellationToken).ConfigureAwait(false);
    }

    private void RaiseThemeChanged(PortableThemeState previous, PortableThemeState current)
    {
        ThemeChanged?.Invoke(
            this,
            new PortableThemeChangedEventArgs(
                previous.CurrentTheme,
                current.CurrentTheme,
                previous.CurrentThemeFileName,
                current.CurrentThemeFileName));
    }
}
