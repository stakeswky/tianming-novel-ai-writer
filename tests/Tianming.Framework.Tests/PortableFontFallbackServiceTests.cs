using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableFontFallbackServiceTests
{
    [Fact]
    public async Task LoadAsync_returns_original_default_chain_when_file_is_missing()
    {
        using var workspace = new TempDirectory();
        var service = new FileFontFallbackService(Path.Combine(workspace.Path, "fallback_chain.json"));

        var chain = await service.LoadAsync();

        Assert.Equal("Consolas", chain.PrimaryFont);
        Assert.Equal(["Microsoft YaHei", "SimSun"], chain.FallbackFonts);
        Assert.True(chain.AutoDetectMissing);
    }

    [Fact]
    public async Task SetFallbackChainAsync_persists_chain_atomically_and_normalizes_duplicates()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "fallback_chain.json");
        var service = new FileFontFallbackService(path);

        await service.SetFallbackChainAsync(new PortableFontFallbackChain
        {
            PrimaryFont = "Menlo",
            FallbackFonts = ["PingFang SC", "pingfang sc", "Hiragino Sans", "Menlo"],
            AutoDetectMissing = false
        });

        var reloaded = await new FileFontFallbackService(path).LoadAsync();

        Assert.False(File.Exists(path + ".tmp"));
        Assert.Equal("Menlo", reloaded.PrimaryFont);
        Assert.Equal(["PingFang SC", "Hiragino Sans"], reloaded.FallbackFonts);
        Assert.False(reloaded.AutoDetectMissing);
    }

    [Fact]
    public async Task AddAndRemoveFallbackFontAsync_updates_chain_without_duplicates()
    {
        using var workspace = new TempDirectory();
        var service = new FileFontFallbackService(Path.Combine(workspace.Path, "fallback_chain.json"));

        await service.AddFallbackFontAsync("PingFang SC");
        await service.AddFallbackFontAsync("pingfang sc");
        await service.RemoveFallbackFontAsync("SimSun");
        var chain = await service.LoadAsync();

        Assert.Equal(["Microsoft YaHei", "PingFang SC"], chain.FallbackFonts);
    }

    [Fact]
    public void BuildFontFamilyName_joins_primary_and_distinct_fallback_fonts()
    {
        var chain = new PortableFontFallbackChain
        {
            PrimaryFont = "Menlo",
            FallbackFonts = ["PingFang SC", "Menlo", "Hiragino Sans"]
        };

        var familyName = PortableFontFallbackService.BuildFontFamilyName(chain);

        Assert.Equal("Menlo, PingFang SC, Hiragino Sans", familyName);
    }

    [Theory]
    [InlineData("JetBrains Mono", new[] { "Consolas", "Courier New", "Lucida Console", "Microsoft YaHei UI", "Microsoft YaHei", "SimSun", "Microsoft JhengHei", "Malgun Gothic" })]
    [InlineData("Helvetica Neue", new[] { "Segoe UI", "Arial", "Microsoft YaHei", "SimSun", "Microsoft JhengHei", "Malgun Gothic" })]
    public void RecommendFallbacks_matches_original_monospace_and_general_rules(string primaryFont, string[] expected)
    {
        var recommendations = PortableFontFallbackService.RecommendFallbacks(primaryFont);

        Assert.Equal(expected, recommendations);
        Assert.DoesNotContain(primaryFont, recommendations, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LoadAsync_recovers_from_invalid_json()
    {
        using var workspace = new TempDirectory();
        var path = Path.Combine(workspace.Path, "fallback_chain.json");
        await File.WriteAllTextAsync(path, "{ invalid json");
        var service = new FileFontFallbackService(path);

        var chain = await service.LoadAsync();

        Assert.Equal("Consolas", chain.PrimaryFont);
        Assert.Equal(["Microsoft YaHei", "SimSun"], chain.FallbackFonts);
    }
}
