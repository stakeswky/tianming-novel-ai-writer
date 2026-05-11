using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Appearance;

public sealed class PortableFontFallbackChain
{
    [JsonPropertyName("PrimaryFont")] public string PrimaryFont { get; set; } = "Consolas";

    [JsonPropertyName("FallbackFonts")] public List<string> FallbackFonts { get; set; } = new();

    [JsonPropertyName("AutoDetectMissing")] public bool AutoDetectMissing { get; set; } = true;
}

public static class PortableFontFallbackService
{
    public static PortableFontFallbackChain CreateDefaultChain()
    {
        return new PortableFontFallbackChain
        {
            PrimaryFont = "Consolas",
            FallbackFonts = ["Microsoft YaHei", "SimSun"],
            AutoDetectMissing = true
        };
    }

    public static string BuildFontFamilyName(PortableFontFallbackChain chain)
    {
        var normalized = Normalize(chain);
        return string.Join(", ", new[] { normalized.PrimaryFont }.Concat(normalized.FallbackFonts));
    }

    public static IReadOnlyList<string> RecommendFallbacks(string primaryFont)
    {
        var recommendations = new List<string>();
        if (PortableFontCatalog.ClassifyFont(primaryFont) == PortableFontCategory.Monospace)
        {
            recommendations.AddRange(["Consolas", "Courier New", "Lucida Console", "Microsoft YaHei UI"]);
        }
        else
        {
            recommendations.AddRange(["Segoe UI", "Arial", "Microsoft YaHei", "SimSun"]);
        }

        recommendations.AddRange(["Microsoft YaHei", "SimSun", "Microsoft JhengHei", "Malgun Gothic"]);

        return recommendations
            .Where(font => !string.Equals(font, primaryFont, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static PortableFontFallbackChain Normalize(PortableFontFallbackChain? chain)
    {
        if (chain is null)
        {
            return CreateDefaultChain();
        }

        var primary = string.IsNullOrWhiteSpace(chain.PrimaryFont) ? "Consolas" : chain.PrimaryFont.Trim();
        return new PortableFontFallbackChain
        {
            PrimaryFont = primary,
            AutoDetectMissing = chain.AutoDetectMissing,
            FallbackFonts = chain.FallbackFonts
                .Where(font => !string.IsNullOrWhiteSpace(font))
                .Select(font => font.Trim())
                .Where(font => !string.Equals(font, primary, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
    }
}

public sealed class FileFontFallbackService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;

    public FileFontFallbackService(string filePath)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Font fallback file path is required.", nameof(filePath))
            : filePath;
    }

    public async Task<PortableFontFallbackChain> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return PortableFontFallbackService.CreateDefaultChain();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var chain = await JsonSerializer.DeserializeAsync<PortableFontFallbackChain>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            return PortableFontFallbackService.Normalize(chain);
        }
        catch (JsonException)
        {
            return PortableFontFallbackService.CreateDefaultChain();
        }
        catch (IOException)
        {
            return PortableFontFallbackService.CreateDefaultChain();
        }
    }

    public async Task SetFallbackChainAsync(
        PortableFontFallbackChain chain,
        CancellationToken cancellationToken = default)
    {
        await SaveAsync(PortableFontFallbackService.Normalize(chain), cancellationToken).ConfigureAwait(false);
    }

    public async Task AddFallbackFontAsync(string fontName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fontName))
        {
            return;
        }

        var chain = await LoadAsync(cancellationToken).ConfigureAwait(false);
        if (!chain.FallbackFonts.Contains(fontName, StringComparer.OrdinalIgnoreCase)
            && !string.Equals(chain.PrimaryFont, fontName, StringComparison.OrdinalIgnoreCase))
        {
            chain.FallbackFonts.Add(fontName);
            await SaveAsync(chain, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task RemoveFallbackFontAsync(string fontName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fontName))
        {
            return;
        }

        var chain = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var removed = chain.FallbackFonts.RemoveAll(
            font => string.Equals(font, fontName, StringComparison.OrdinalIgnoreCase)) > 0;
        if (removed)
        {
            await SaveAsync(chain, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SaveAsync(PortableFontFallbackChain chain, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                PortableFontFallbackService.Normalize(chain),
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }
}
