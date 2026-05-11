using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableLoadingAnimationSettingsStoreTests
{
    [Fact]
    public void Default_settings_match_original_loading_animation_defaults()
    {
        var settings = PortableLoadingAnimationSettings.CreateDefault();

        Assert.Equal(PortableLoadingAnimationType.Spinner, settings.AnimationType);
        Assert.Equal(PortableLoadingPosition.Center, settings.Position);
        Assert.Equal(PortableOverlayMode.Transparent, settings.Overlay);
        Assert.Equal(100, settings.AnimationSpeed);
        Assert.Equal(48, settings.Size);
        Assert.Equal("#3B82F6", settings.PrimaryColor);
        Assert.Equal("#60A5FA", settings.SecondaryColor);
        Assert.Equal(0.9, settings.Opacity);
        Assert.True(settings.ShowText);
        Assert.Equal("加载中...", settings.LoadingText);
        Assert.Equal(14, settings.TextSize);
        Assert.Equal("#FFFFFF", settings.TextColor);
        Assert.False(settings.ShowPercentage);
        Assert.Equal(0.5, settings.OverlayOpacity);
        Assert.Equal("#000000", settings.OverlayColor);
        Assert.Equal(8, settings.BlurRadius);
        Assert.Equal(300, settings.MinDisplayTime);
        Assert.Equal(200, settings.DelayTime);
        Assert.False(settings.CancelOnClick);
        Assert.False(settings.EnableSound);
        Assert.Equal(string.Empty, settings.SoundPath);
    }

    [Fact]
    public void Clone_returns_independent_copy_with_all_values()
    {
        var settings = PortableLoadingAnimationSettings.CreateDefault();
        settings.AnimationType = PortableLoadingAnimationType.Wave;
        settings.Position = PortableLoadingPosition.BottomRight;
        settings.Overlay = PortableOverlayMode.Blur;
        settings.LoadingText = "处理中";
        settings.EnableSound = true;
        settings.SoundPath = "/tmp/done.wav";

        var clone = settings.Clone();
        clone.LoadingText = "changed";

        Assert.Equal(PortableLoadingAnimationType.Wave, clone.AnimationType);
        Assert.Equal(PortableLoadingPosition.BottomRight, clone.Position);
        Assert.Equal(PortableOverlayMode.Blur, clone.Overlay);
        Assert.True(clone.EnableSound);
        Assert.Equal("/tmp/done.wav", clone.SoundPath);
        Assert.Equal("处理中", settings.LoadingText);
    }

    [Fact]
    public async Task Store_round_trips_settings_atomically()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "settings.json");
        var store = new FileLoadingAnimationSettingsStore(path);
        var settings = PortableLoadingAnimationSettings.CreateDefault();
        settings.AnimationType = PortableLoadingAnimationType.Progress;
        settings.Position = PortableLoadingPosition.TopRight;
        settings.Overlay = PortableOverlayMode.Full;
        settings.AnimationSpeed = 160;
        settings.ShowPercentage = true;
        settings.CancelOnClick = true;

        await store.SaveAsync(settings);
        var reloaded = await new FileLoadingAnimationSettingsStore(path).LoadAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal(PortableLoadingAnimationType.Progress, reloaded.AnimationType);
        Assert.Equal(PortableLoadingPosition.TopRight, reloaded.Position);
        Assert.Equal(PortableOverlayMode.Full, reloaded.Overlay);
        Assert.Equal(160, reloaded.AnimationSpeed);
        Assert.True(reloaded.ShowPercentage);
        Assert.True(reloaded.CancelOnClick);
    }

    [Fact]
    public async Task LoadAsync_recovers_from_missing_or_invalid_json()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "settings.json");
        var missingStore = new FileLoadingAnimationSettingsStore(path);

        Assert.Equal(PortableLoadingAnimationType.Spinner, (await missingStore.LoadAsync()).AnimationType);

        await File.WriteAllTextAsync(path, "{ invalid json");
        var invalidStore = new FileLoadingAnimationSettingsStore(path);

        Assert.Equal("加载中...", (await invalidStore.LoadAsync()).LoadingText);
    }

    [Fact]
    public async Task Store_creates_parent_directory()
    {
        using var workspace = new TempDirectory();
        var nestedPath = Path.Combine(workspace.Path, "Appearance", "Animation", "LoadingAnimation", "settings.json");
        var store = new FileLoadingAnimationSettingsStore(nestedPath);

        await store.SaveAsync(PortableLoadingAnimationSettings.CreateDefault());

        Assert.True(File.Exists(nestedPath));
    }
}
