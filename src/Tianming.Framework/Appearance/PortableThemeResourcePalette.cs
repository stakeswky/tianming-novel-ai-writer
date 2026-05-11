namespace TM.Framework.Appearance;

public sealed record PortableThemeBrushPalette(
    PortableThemeType ThemeType,
    IReadOnlyDictionary<string, string> Brushes);

public static class PortableThemeResourcePalette
{
    public static IReadOnlyList<string> RequiredBrushKeys { get; } =
    [
        "UnifiedBackground",
        "ContentBackground",
        "Surface",
        "ContentHighlight",
        "WindowBorder",
        "BorderBrush",
        "TextPrimary",
        "TextSecondary",
        "TextTertiary",
        "TextDisabled",
        "HoverBackground",
        "ActiveBackground",
        "SelectedBackground",
        "PrimaryColor",
        "PrimaryHover",
        "PrimaryActive",
        "SuccessColor",
        "WarningColor",
        "DangerColor",
        "DangerHover",
        "InfoColor"
    ];

    private static readonly IReadOnlyDictionary<PortableThemeType, string[]> PaletteValues =
        new Dictionary<PortableThemeType, string[]>
        {
            [PortableThemeType.Light] =
            [
                "#F1F5F9", "#FFFFFF", "#FFFFFF", "#FFFFFF", "#CBD5E1", "#E2E8F0", "#1E293B",
                "#64748B", "#94A3B8", "#CBD5E1", "#E2E8F0", "#CBD5E1", "#E0E7FF", "#3B82F6",
                "#2563EB", "#1D4ED8", "#10B981", "#F59E0B", "#EF4444", "#DC2626", "#3B82F6"
            ],
            [PortableThemeType.Green] =
            [
                "#E8DCC8", "#F5EDDC", "#F5EDDC", "#F5EDDC", "#C9BEA3", "#D8CCBA", "#4A3728",
                "#6B5744", "#998B78", "#BEB0A0", "#E2D5C2", "#D8CCBA", "#DDD0BC", "#8B6914",
                "#7A5C0F", "#69500A", "#6B8E5A", "#C89030", "#C0543C", "#A8462F", "#7B8FA8"
            ],
            [PortableThemeType.Dark] =
            [
                "#0F172A", "#1E293B", "#1E293B", "#1E293B", "#334155", "#334155", "#F1F5F9",
                "#94A3B8", "#64748B", "#475569", "#334155", "#475569", "#1E3A5F", "#60A5FA",
                "#3B82F6", "#2563EB", "#34D399", "#FBBF24", "#F87171", "#EF4444", "#60A5FA"
            ],
            [PortableThemeType.Arctic] =
            [
                "#E8F0FE", "#F0F7FF", "#F0F7FF", "#F0F7FF", "#B0C4DE", "#C8D8E8", "#1A365D",
                "#2D5087", "#6889AB", "#A0B4C8", "#D6E4F0", "#C8D8E8", "#BDD0E7", "#0284C7",
                "#0369A1", "#075985", "#059669", "#D97706", "#DC2626", "#B91C1C", "#0284C7"
            ],
            [PortableThemeType.Forest] =
            [
                "#E8F5E9", "#F1F8F2", "#F1F8F2", "#F1F8F2", "#A5C8A8", "#C3DBC5", "#1B3A1D",
                "#3E6B42", "#6B9E6E", "#A5C8A8", "#D4E8D5", "#C3DBC5", "#B8D4BA", "#2E7D32",
                "#1B5E20", "#134B17", "#43A047", "#F9A825", "#E53935", "#C62828", "#1976D2"
            ],
            [PortableThemeType.Violet] =
            [
                "#F0E8F5", "#F8F0FF", "#F8F0FF", "#F8F0FF", "#C8A8E0", "#DCC8EC", "#2D1B4E",
                "#5B3E8A", "#8B6DB8", "#B8A0D0", "#E8D8F2", "#DCC8EC", "#D0BBE5", "#7C3AED",
                "#6D28D9", "#5B21B6", "#10B981", "#F59E0B", "#EF4444", "#DC2626", "#8B5CF6"
            ],
            [PortableThemeType.Business] =
            [
                "#EDEDED", "#F7F7F7", "#F7F7F7", "#F7F7F7", "#BFBFBF", "#D4D4D4", "#262626",
                "#595959", "#8C8C8C", "#BFBFBF", "#E0E0E0", "#D4D4D4", "#D0D8E0", "#4A6FA5",
                "#3D5D8C", "#304B73", "#52C41A", "#FAAD14", "#FF4D4F", "#D9363E", "#4A6FA5"
            ],
            [PortableThemeType.MinimalBlack] =
            [
                "#121212", "#1A1A1A", "#1A1A1A", "#1A1A1A", "#333333", "#2A2A2A", "#E8E8E8",
                "#A0A0A0", "#707070", "#4A4A4A", "#2A2A2A", "#333333", "#2D2D2D", "#6CB6FF",
                "#539BF5", "#4184E4", "#3FB950", "#D29922", "#F85149", "#DA3633", "#6CB6FF"
            ],
            [PortableThemeType.ModernBlue] =
            [
                "#0A1628", "#112240", "#112240", "#112240", "#1E3A5F", "#1E3A5F", "#E2E8F0",
                "#8892B0", "#606D80", "#3D4F65", "#1A2D4A", "#1E3A5F", "#172E4F", "#1890FF",
                "#40A9FF", "#096DD9", "#52C41A", "#FAAD14", "#FF4D4F", "#FF7875", "#1890FF"
            ],
            [PortableThemeType.WarmOrange] =
            [
                "#FFF0E0", "#FFF7E6", "#FFF7E6", "#FFF7E6", "#F0C8A0", "#F5D8B8", "#5C3A18",
                "#8C6540", "#B08060", "#D4B8A0", "#FFEDD0", "#F5D8B8", "#FFE4C0", "#E8780A",
                "#D06A05", "#B85C00", "#52C41A", "#FA8C16", "#F5222D", "#CF1322", "#1890FF"
            ],
            [PortableThemeType.Pink] =
            [
                "#FDE8EF", "#FFF0F6", "#FFF0F6", "#FFF0F6", "#F0B0C8", "#F5C8D8", "#4A1030",
                "#7A3055", "#A86080", "#D0A0B0", "#FFE0EB", "#F5C8D8", "#FFD6E5", "#EB2F96",
                "#C41D7F", "#9E1068", "#52C41A", "#FAAD14", "#FF4D4F", "#CF1322", "#1890FF"
            ],
            [PortableThemeType.TechCyan] =
            [
                "#0A1929", "#0D2137", "#0D2137", "#0D2137", "#1A3A50", "#1A3A50", "#E0F0F0",
                "#88B0B8", "#608088", "#384850", "#122E42", "#1A3A50", "#153348", "#13C2C2",
                "#36CFC9", "#08979C", "#52C41A", "#FAAD14", "#FF4D4F", "#FF7875", "#13C2C2"
            ],
            [PortableThemeType.Sunset] =
            [
                "#FDE8D8", "#FFF4EC", "#FFF4EC", "#FFF4EC", "#E0B8A0", "#F0D0C0", "#5C2E18",
                "#8C5A3C", "#B08060", "#D0A890", "#FFE8D5", "#F0D0C0", "#FFDFC8", "#E85D26",
                "#D04E1A", "#B84010", "#52C41A", "#FA8C16", "#F5222D", "#CF1322", "#1890FF"
            ],
            [PortableThemeType.Morandi] =
            [
                "#E8E4E0", "#F5F4F2", "#F5F4F2", "#F5F4F2", "#C0BAB5", "#D0CBC5", "#4A4845",
                "#6B6865", "#908D88", "#B5B0AB", "#E0DCD8", "#D0CBC5", "#D5D0CA", "#7C9299",
                "#6A8088", "#586E75", "#7BA67D", "#C4A35A", "#C07070", "#A85D5D", "#7C9299"
            ],
            [PortableThemeType.HighContrast] =
            [
                "#000000", "#000000", "#000000", "#000000", "#FFFFFF", "#FFFFFF", "#FFFFFF",
                "#FFFF00", "#00FFFF", "#808080", "#1A1A1A", "#333333", "#003366", "#FFFF00",
                "#FFFFFF", "#FFD700", "#00FF00", "#FFFF00", "#FF0000", "#FF6666", "#00FFFF"
            ]
        };

    public static IReadOnlyDictionary<string, string> GetBrushes(
        PortableThemeType theme,
        PortableSystemThemeSnapshot? systemSnapshot = null)
    {
        var resolvedTheme = ResolveTheme(theme, systemSnapshot);
        if (resolvedTheme == PortableThemeType.Custom)
        {
            throw new ArgumentException("Custom themes must be loaded from a theme resource file.", nameof(theme));
        }

        if (!PaletteValues.TryGetValue(resolvedTheme, out var values))
        {
            values = PaletteValues[PortableThemeType.Light];
        }

        return RequiredBrushKeys
            .Select((key, index) => new KeyValuePair<string, string>(key, values[index]))
            .ToDictionary(pair => pair.Key, pair => pair.Value);
    }

    public static IReadOnlyList<PortableThemeBrushPalette> GetAllBuiltInPalettes()
    {
        return PaletteValues
            .Select(pair => new PortableThemeBrushPalette(pair.Key, GetBrushes(pair.Key)))
            .ToList();
    }

    private static PortableThemeType ResolveTheme(
        PortableThemeType theme,
        PortableSystemThemeSnapshot? systemSnapshot)
    {
        if (theme != PortableThemeType.Auto)
        {
            return theme;
        }

        return systemSnapshot?.IsLightTheme == false
            ? PortableThemeType.Dark
            : PortableThemeType.Light;
    }
}
