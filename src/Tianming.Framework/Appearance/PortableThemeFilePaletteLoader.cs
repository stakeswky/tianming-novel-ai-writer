using System.Globalization;
using System.Xml.Linq;

namespace TM.Framework.Appearance;

public sealed class PortableThemeFilePaletteLoadResult
{
    public bool Success { get; init; }

    public string FileName { get; init; } = string.Empty;

    public IReadOnlyDictionary<string, string> Brushes { get; init; } = new Dictionary<string, string>();

    public string? ErrorMessage { get; init; }
}

public static class PortableThemeFilePaletteLoader
{
    private static readonly HashSet<string> RequiredKeys = ["PrimaryColor", "ContentBackground", "TextPrimary"];

    public static async Task<PortableThemeFilePaletteLoadResult> LoadAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            return Failure(filePath, "Theme file does not exist.");
        }

        try
        {
            var content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            var document = XDocument.Parse(content);
            if (!string.Equals(document.Root?.Name.LocalName, "ResourceDictionary", StringComparison.Ordinal))
            {
                return Failure(filePath, "Theme file must contain a ResourceDictionary root.");
            }

            var allowedKeys = PortableThemeResourcePalette.RequiredBrushKeys.ToHashSet(StringComparer.Ordinal);
            var brushes = document
                .Descendants()
                .Where(element => string.Equals(element.Name.LocalName, "SolidColorBrush", StringComparison.Ordinal))
                .Select(element => new
                {
                    Key = element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "Key")?.Value,
                    Color = element.Attribute("Color")?.Value
                })
                .Where(entry => !string.IsNullOrWhiteSpace(entry.Key)
                    && !string.IsNullOrWhiteSpace(entry.Color)
                    && allowedKeys.Contains(entry.Key))
                .ToDictionary(
                    entry => entry.Key!,
                    entry => NormalizeColor(entry.Color!),
                    StringComparer.Ordinal);

            var missingRequired = RequiredKeys
                .Where(requiredKey => !brushes.ContainsKey(requiredKey))
                .ToList();
            if (missingRequired.Count > 0)
            {
                return Failure(filePath, $"Theme file is missing required brush keys: {string.Join(", ", missingRequired)}.");
            }

            return new PortableThemeFilePaletteLoadResult
            {
                Success = true,
                FileName = Path.GetFileName(filePath),
                Brushes = brushes
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException or FormatException)
        {
            return Failure(filePath, ex.Message);
        }
    }

    private static PortableThemeFilePaletteLoadResult Failure(string filePath, string message)
    {
        return new PortableThemeFilePaletteLoadResult
        {
            Success = false,
            FileName = Path.GetFileName(filePath),
            ErrorMessage = message,
            Brushes = new Dictionary<string, string>()
        };
    }

    private static string NormalizeColor(string color)
    {
        var text = color.Trim();
        if (text.StartsWith('#'))
        {
            text = text[1..];
        }

        if (text.Length != 6)
        {
            throw new FormatException($"Color must be a 6-digit hex value: {color}");
        }

        _ = int.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        return "#" + text.ToUpperInvariant();
    }
}
