using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableThemeStateControllerTests
{
    [Fact]
    public async Task FileThemeStateStore_loads_default_and_round_trips_state_atomically()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "Appearance", "ThemeManagement", "state.json");
        var store = new FileThemeStateStore(path);

        var missing = await store.LoadAsync();
        Assert.Equal(PortableThemeType.Light, missing.CurrentTheme);
        Assert.Null(missing.CurrentThemeFileName);

        await store.SaveAsync(new PortableThemeState
        {
            CurrentTheme = PortableThemeType.Custom,
            CurrentThemeFileName = "Midnight.xaml"
        });

        var reloaded = await new FileThemeStateStore(path).LoadAsync();
        Assert.Equal(PortableThemeType.Custom, reloaded.CurrentTheme);
        Assert.Equal("Midnight.xaml", reloaded.CurrentThemeFileName);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task FileThemeStateStore_recovers_from_invalid_json()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "state.json");
        await File.WriteAllTextAsync(path, "{ invalid json");
        var store = new FileThemeStateStore(path);

        var state = await store.LoadAsync();

        Assert.Equal(PortableThemeType.Light, state.CurrentTheme);
        Assert.Null(state.CurrentThemeFileName);
    }

    [Fact]
    public async Task SwitchThemeAsync_applies_built_in_theme_saves_state_and_raises_event()
    {
        var state = new PortableThemeState { CurrentTheme = PortableThemeType.Light };
        var applied = new List<PortableThemeApplicationRequest>();
        var saved = new List<PortableThemeState>();
        var changed = new List<PortableThemeChangedEventArgs>();
        var controller = new PortableThemeStateController(
            state,
            request =>
            {
                applied.Add(request);
                return Task.CompletedTask;
            },
            (newState, _) =>
            {
                saved.Add(newState.Clone());
                return Task.CompletedTask;
            });
        controller.ThemeChanged += (_, args) => changed.Add(args);

        var result = await controller.SwitchThemeAsync(PortableThemeType.Dark);

        Assert.True(result.Applied);
        Assert.Equal(PortableThemeType.Dark, controller.CurrentTheme);
        Assert.Null(controller.CurrentThemeFileName);
        var request = Assert.Single(applied);
        Assert.Equal(PortableThemeType.Dark, request.Plan.ThemeType);
        Assert.Equal("#0F172A", request.Brushes["UnifiedBackground"]);
        var savedState = Assert.Single(saved);
        Assert.Equal(PortableThemeType.Dark, savedState.CurrentTheme);
        var change = Assert.Single(changed);
        Assert.Equal(PortableThemeType.Light, change.OldTheme);
        Assert.Equal(PortableThemeType.Dark, change.NewTheme);
    }

    [Fact]
    public async Task SwitchThemeAsync_skips_when_theme_is_unchanged()
    {
        var state = new PortableThemeState { CurrentTheme = PortableThemeType.Dark };
        var applyCount = 0;
        var saveCount = 0;
        var controller = new PortableThemeStateController(
            state,
            _ =>
            {
                applyCount++;
                return Task.CompletedTask;
            },
            (_, _) =>
            {
                saveCount++;
                return Task.CompletedTask;
            });

        var result = await controller.SwitchThemeAsync(PortableThemeType.Dark);

        Assert.False(result.Applied);
        Assert.Equal("主题未变更", result.Message);
        Assert.Equal(0, applyCount);
        Assert.Equal(0, saveCount);
    }

    [Fact]
    public async Task ApplyThemeFromFileAsync_normalizes_custom_file_and_clears_builtin_theme()
    {
        var state = new PortableThemeState { CurrentTheme = PortableThemeType.Dark };
        PortableThemeApplicationRequest? request = null;
        PortableThemeState? saved = null;
        var controller = new PortableThemeStateController(
            state,
            applied =>
            {
                request = applied;
                return Task.CompletedTask;
            },
            (newState, _) =>
            {
                saved = newState.Clone();
                return Task.CompletedTask;
            });

        var result = await controller.ApplyThemeFromFileAsync("custom/Midnight");

        Assert.True(result.Applied);
        Assert.Equal(PortableThemeType.Custom, controller.CurrentTheme);
        Assert.Equal("Midnight.xaml", controller.CurrentThemeFileName);
        Assert.NotNull(request);
        Assert.Equal(PortableThemeType.Custom, request.Plan.ThemeType);
        Assert.Equal("Midnight.xaml", request.Plan.ResourceFileName);
        Assert.Empty(request.Brushes);
        Assert.NotNull(saved);
        Assert.Equal(PortableThemeType.Custom, saved.CurrentTheme);
        Assert.Equal("Midnight.xaml", saved.CurrentThemeFileName);
    }

    [Fact]
    public async Task ApplyThemeAsync_parses_builtin_or_treats_unknown_as_custom_file()
    {
        var state = new PortableThemeState { CurrentTheme = PortableThemeType.Light };
        var applied = new List<PortableThemeApplicationRequest>();
        var controller = new PortableThemeStateController(
            state,
            request =>
            {
                applied.Add(request);
                return Task.CompletedTask;
            });

        await controller.ApplyThemeAsync("ModernBlueTheme.xaml");
        await controller.ApplyThemeAsync("MyTheme");

        Assert.Equal(PortableThemeType.ModernBlue, applied[0].Plan.ThemeType);
        Assert.Equal(PortableThemeType.Custom, applied[1].Plan.ThemeType);
        Assert.Equal("MyTheme.xaml", applied[1].Plan.ResourceFileName);
    }
}
