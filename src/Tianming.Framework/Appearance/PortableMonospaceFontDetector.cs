namespace TM.Framework.Appearance;

public sealed class PortableMonospaceFontDetector
{
    public static readonly IReadOnlyList<char> TestChars = ['i', 'l', 'I', '1', 'm', 'W', 'M', '0', 'O'];

    private static readonly string[] KnownMonospaceFonts =
    [
        "consolas",
        "courier",
        "courier new",
        "monaco",
        "menlo",
        "fira code",
        "fira mono",
        "jetbrains mono",
        "cascadia code",
        "cascadia mono",
        "source code pro",
        "inconsolata",
        "dejavu sans mono",
        "ubuntu mono",
        "roboto mono",
        "sf mono",
        "droid sans mono",
        "liberation mono",
        "noto mono",
        "hack",
        "anonymous pro",
        "meslo",
        "input mono"
    ];

    private readonly Func<string, double, IReadOnlyDictionary<char, double>>? _measureCharacterWidths;
    private readonly Dictionary<string, bool> _cache = new(StringComparer.OrdinalIgnoreCase);

    public PortableMonospaceFontDetector(
        Func<string, double, IReadOnlyDictionary<char, double>>? measureCharacterWidths = null)
    {
        _measureCharacterWidths = measureCharacterWidths;
    }

    public bool IsMonospace(string fontFamilyName)
    {
        if (string.IsNullOrWhiteSpace(fontFamilyName))
        {
            return false;
        }

        if (_cache.TryGetValue(fontFamilyName, out var cached))
        {
            return cached;
        }

        if (IsKnownMonospaceFont(fontFamilyName))
        {
            _cache[fontFamilyName] = true;
            return true;
        }

        var result = MeasureIsMonospace(fontFamilyName);
        _cache[fontFamilyName] = result;
        return result;
    }

    public Dictionary<char, double> GetCharacterWidths(string fontFamilyName, double fontSize = 12.0)
    {
        if (_measureCharacterWidths is null || string.IsNullOrWhiteSpace(fontFamilyName))
        {
            return [];
        }

        return _measureCharacterWidths(fontFamilyName, fontSize).ToDictionary();
    }

    public void ClearCache()
    {
        _cache.Clear();
    }

    private bool MeasureIsMonospace(string fontFamilyName)
    {
        var widths = GetCharacterWidths(fontFamilyName);
        var values = TestChars
            .Where(widths.ContainsKey)
            .Select(ch => widths[ch])
            .Where(width => width > 0)
            .ToList();

        if (values.Count < 2)
        {
            return false;
        }

        var average = values.Average();
        if (average <= 0)
        {
            return false;
        }

        var maxDeviation = values.Max(width => Math.Abs(width - average));
        var tolerance = average * 0.05;
        return maxDeviation <= tolerance;
    }

    private static bool IsKnownMonospaceFont(string fontName)
    {
        var lowerName = fontName.ToLowerInvariant();
        return KnownMonospaceFonts.Any(lowerName.Contains);
    }
}
