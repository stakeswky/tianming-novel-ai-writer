namespace TM.Framework.Appearance;

public sealed class PortableLigatureDetector
{
    private static readonly string[] CommonLigatures =
    [
        "->", "=>", ">=", "<=", "!=", "==", "===", "!==",
        "&&", "||", "++", "--", "<<", ">>", "::", "..",
        "...", "/*", "*/", "//", "/**", "**/",
        "<-", "<->", "==>", "<==>", "<==", "|>", "<|",
        "~>", "<~", ">>=", "<<=", "***", "|||", "///"
    ];

    private static readonly string[] KnownLigatureFonts =
    [
        "fira code",
        "jetbrains mono",
        "cascadia code",
        "cascadia mono",
        "monoid",
        "hasklig",
        "victor mono"
    ];

    private readonly Dictionary<string, IReadOnlyList<string>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public bool SupportsLigatures(string fontFamilyName)
    {
        return GetSupportedLigatures(fontFamilyName).Count > 0;
    }

    public List<string> GetSupportedLigatures(string fontFamilyName)
    {
        if (string.IsNullOrWhiteSpace(fontFamilyName))
        {
            return [];
        }

        if (_cache.TryGetValue(fontFamilyName, out var cached))
        {
            return cached.ToList();
        }

        var lowerName = fontFamilyName.ToLowerInvariant();
        var ligatures = KnownLigatureFonts.Any(lowerName.Contains)
            ? CommonLigatures.ToList()
            : [];

        _cache[fontFamilyName] = ligatures;
        return ligatures.ToList();
    }

    public void ClearCache()
    {
        _cache.Clear();
    }

    public static string GenerateLigaturePreviewText(IReadOnlyList<string>? supportedLigatures)
    {
        if (supportedLigatures is null || supportedLigatures.Count == 0)
        {
            return "此字体不支持编程连字";
        }

        var lines = new List<string>
        {
            "// 编程连字预览",
            string.Empty
        };

        var arrows = supportedLigatures.Where(ligature =>
            ligature.Contains("->", StringComparison.Ordinal)
            || ligature.Contains("=>", StringComparison.Ordinal)
            || ligature.Contains("<-", StringComparison.Ordinal)).ToList();
        if (arrows.Count > 0)
        {
            lines.Add($"箭头: {string.Join("  ", arrows)}");
        }

        var comparisons = supportedLigatures.Where(ligature =>
            ligature.Contains('=', StringComparison.Ordinal)
            || ligature.Contains('!', StringComparison.Ordinal)).ToList();
        if (comparisons.Count > 0)
        {
            lines.Add($"比较: {string.Join("  ", comparisons)}");
        }

        var logical = supportedLigatures.Where(ligature =>
            ligature.Contains("&&", StringComparison.Ordinal)
            || ligature.Contains("||", StringComparison.Ordinal)).ToList();
        if (logical.Count > 0)
        {
            lines.Add($"逻辑: {string.Join("  ", logical)}");
        }

        var others = supportedLigatures.Except(arrows).Except(comparisons).Except(logical).ToList();
        if (others.Count > 0)
        {
            lines.Add($"其他: {string.Join("  ", others)}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
