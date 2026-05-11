namespace TM.Framework.Appearance;

public sealed class PortableFontInfoModel
{
    public string FontName { get; set; } = string.Empty;

    public string Designer { get; set; } = "未知";

    public string Version { get; set; } = "未知";

    public string License { get; set; } = "未知";

    public string Copyright { get; set; } = "未知";

    public List<string> SupportedScripts { get; set; } = [];

    public int MinWeight { get; set; } = 400;

    public int MaxWeight { get; set; } = 400;

    public bool SupportsItalic { get; set; }

    public bool IsMonospace { get; set; }

    public int GlyphCount { get; set; }

    public bool SupportsLatin { get; set; }

    public bool SupportsCyrillic { get; set; }

    public bool SupportsCJK { get; set; }

    public bool SupportsArabic { get; set; }

    public bool SupportsSymbols { get; set; }
}

public sealed class PortableFontProbeResult
{
    public string? Designer { get; set; }

    public string? Version { get; set; }

    public string? License { get; set; }

    public string? Copyright { get; set; }

    public bool SupportsItalic { get; set; }

    public IReadOnlyCollection<char> CharacterMap { get; set; } = [];
}

public sealed class PortableFontInfoService
{
    private readonly Func<string, PortableFontProbeResult?> _fontProbe;
    private readonly Func<string, IReadOnlyList<string>> _variantProbe;
    private readonly Dictionary<string, PortableFontInfoModel> _cache = new(StringComparer.OrdinalIgnoreCase);

    public PortableFontInfoService(
        Func<string, PortableFontProbeResult?> fontProbe,
        Func<string, IReadOnlyList<string>>? variantProbe = null)
    {
        _fontProbe = fontProbe ?? throw new ArgumentNullException(nameof(fontProbe));
        _variantProbe = variantProbe ?? (_ => []);
    }

    public PortableFontInfoModel GetFontInfo(string fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName))
        {
            return new PortableFontInfoModel { FontName = "未选择字体" };
        }

        if (_cache.TryGetValue(fontName, out var cachedInfo))
        {
            return cachedInfo;
        }

        var info = ParseFontInfo(fontName);
        _cache[fontName] = info;
        return info;
    }

    public void ClearCache()
    {
        _cache.Clear();
    }

    public List<string> GetFontVariants(string fontName)
    {
        try
        {
            return _variantProbe(fontName).ToList();
        }
        catch
        {
            return [];
        }
    }

    private PortableFontInfoModel ParseFontInfo(string fontName)
    {
        var info = new PortableFontInfoModel { FontName = fontName };

        try
        {
            var probe = _fontProbe(fontName);
            if (probe is null)
            {
                return info;
            }

            info.Designer = string.IsNullOrWhiteSpace(probe.Designer) ? "未知" : probe.Designer;
            info.Version = string.IsNullOrWhiteSpace(probe.Version) ? "未知" : probe.Version;
            info.License = string.IsNullOrWhiteSpace(probe.License) ? "未知" : probe.License;
            info.Copyright = string.IsNullOrWhiteSpace(probe.Copyright) ? "未知" : probe.Copyright;
            info.GlyphCount = probe.CharacterMap.Count;
            info.MinWeight = 400;
            info.MaxWeight = 700;
            info.SupportsItalic = probe.SupportsItalic;
            info.IsMonospace = PortableFontCatalog.ClassifyFont(fontName) == PortableFontCategory.Monospace;
            DetectCharacterSetSupport(probe.CharacterMap, info);
        }
        catch
        {
            return info;
        }

        return info;
    }

    private static void DetectCharacterSetSupport(
        IReadOnlyCollection<char> characterMap,
        PortableFontInfoModel info)
    {
        info.SupportsLatin = characterMap.Contains('A') && characterMap.Contains('z');
        info.SupportsCyrillic = characterMap.Contains('\u0410');
        info.SupportsCJK = characterMap.Contains('\u4E00')
            || characterMap.Contains('\u4E2D')
            || characterMap.Contains('\u6587');
        info.SupportsArabic = characterMap.Contains('\u0627');
        info.SupportsSymbols = characterMap.Contains('©')
            || characterMap.Contains('®')
            || characterMap.Contains('™');

        info.SupportedScripts.Clear();
        if (info.SupportsLatin)
        {
            info.SupportedScripts.Add("拉丁");
        }

        if (info.SupportsCyrillic)
        {
            info.SupportedScripts.Add("西里尔");
        }

        if (info.SupportsCJK)
        {
            info.SupportedScripts.Add("中日韩");
        }

        if (info.SupportsArabic)
        {
            info.SupportedScripts.Add("阿拉伯");
        }

        if (info.SupportsSymbols)
        {
            info.SupportedScripts.Add("符号");
        }
    }
}
