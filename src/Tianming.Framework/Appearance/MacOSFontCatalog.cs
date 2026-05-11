using System.Text.RegularExpressions;

namespace TM.Framework.Appearance;

public sealed class MacOSFontCatalog
{
    private static readonly string[] SupportedExtensions = [".ttf", ".ttc", ".otf"];

    private static readonly string[] StyleSuffixes =
    [
        "Regular",
        "Bold",
        "Italic",
        "Bold Italic",
        "Light",
        "Medium",
        "Semibold",
        "Semi Bold",
        "Demi Bold",
        "Heavy",
        "Black",
        "Thin",
        "Ultra Light",
        "Condensed"
    ];

    private readonly IReadOnlyList<string> _fontDirectories;

    public MacOSFontCatalog()
        : this(GetDefaultFontDirectories())
    {
    }

    public MacOSFontCatalog(IReadOnlyList<string> fontDirectories)
    {
        _fontDirectories = fontDirectories ?? throw new ArgumentNullException(nameof(fontDirectories));
    }

    public IReadOnlyList<string> GetInstalledFontFamilies()
    {
        var fonts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var directory in _fontDirectories.Where(Directory.Exists))
        {
            foreach (var filePath in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                         .Where(IsSupportedFontFile))
            {
                var fontName = NormalizeFontFamilyName(Path.GetFileName(filePath));
                if (!string.IsNullOrWhiteSpace(fontName) && !fonts.ContainsKey(fontName))
                {
                    fonts[fontName] = fontName;
                }
            }
        }

        return fonts.Values
            .OrderBy(font => font, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string NormalizeFontFamilyName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName)
            .Replace('-', ' ')
            .Replace('_', ' ')
            .Trim();
        name = Regex.Replace(name, @"\s+", " ");

        foreach (var suffix in StyleSuffixes.OrderByDescending(static suffix => suffix.Length))
        {
            if (name.EndsWith(" " + suffix, StringComparison.OrdinalIgnoreCase))
            {
                name = name[..^(suffix.Length + 1)].Trim();
                break;
            }
        }

        return name;
    }

    private static bool IsSupportedFontFile(string filePath)
    {
        var extension = Path.GetExtension(filePath);
        return SupportedExtensions.Any(supported => string.Equals(supported, extension, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> GetDefaultFontDirectories()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var directories = new List<string>
        {
            "/System/Library/Fonts",
            "/Library/Fonts"
        };

        if (!string.IsNullOrWhiteSpace(home))
        {
            directories.Add(Path.Combine(home, "Library", "Fonts"));
        }

        return directories;
    }
}
