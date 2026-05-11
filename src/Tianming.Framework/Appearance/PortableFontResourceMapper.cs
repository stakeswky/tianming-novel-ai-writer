namespace TM.Framework.Appearance;

public static class PortableFontResourceMapper
{
    private static readonly HashSet<string> SupportedFontWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        "Thin",
        "ExtraLight",
        "Light",
        "Normal",
        "Medium",
        "SemiBold",
        "Bold",
        "ExtraBold",
        "Black"
    };

    public static IReadOnlyDictionary<string, object> CreateUIResources(PortableFontSettings settings)
    {
        settings = Normalize(settings, defaultFontFamily: "Microsoft YaHei UI");
        return new Dictionary<string, object>
        {
            ["GlobalFontFamily"] = settings.FontFamily,
            ["GlobalFontSize"] = settings.FontSize,
            ["GlobalFontWeight"] = settings.FontWeight,
            ["GlobalLineHeight"] = settings.LineHeight,
            ["GlobalLetterSpacing"] = settings.LetterSpacing,
            ["FontSizeXXL"] = Scale(settings.FontSize, 2.29),
            ["FontSizeXL"] = Scale(settings.FontSize, 1.71),
            ["FontSizeLarge"] = Scale(settings.FontSize, 1.29),
            ["FontSizeMedium"] = Scale(settings.FontSize, 1.14),
            ["FontSizeNormal"] = settings.FontSize,
            ["FontSizeSmall"] = Scale(settings.FontSize, 0.93),
            ["FontSizeXS"] = Scale(settings.FontSize, 0.86),
            ["FontSizeTiny"] = Scale(settings.FontSize, 0.79),
            ["GlobalTextRenderingMode"] = settings.TextRendering.ToString(),
            ["GlobalTextFormattingMode"] = settings.TextFormatting.ToString(),
            ["GlobalTextHintingMode"] = settings.TextHinting.ToString()
        };
    }

    public static IReadOnlyDictionary<string, object> CreateEditorResources(PortableFontSettings settings)
    {
        settings = Normalize(settings, defaultFontFamily: "Consolas");
        return new Dictionary<string, object>
        {
            ["EditorFontFamily"] = settings.FontFamily,
            ["EditorFontSize"] = settings.FontSize,
            ["EditorFontWeight"] = settings.FontWeight,
            ["EditorLineHeight"] = settings.LineHeight,
            ["EditorLetterSpacing"] = settings.LetterSpacing,
            ["EditorFontSizeLarge"] = Scale(settings.FontSize, 1.15),
            ["EditorFontSizeSmall"] = Scale(settings.FontSize, 0.85)
        };
    }

    private static PortableFontSettings Normalize(PortableFontSettings settings, string defaultFontFamily)
    {
        return new PortableFontSettings
        {
            FontFamily = string.IsNullOrWhiteSpace(settings.FontFamily) ? defaultFontFamily : settings.FontFamily,
            FontSize = settings.FontSize > 0 ? settings.FontSize : 14,
            FontWeight = SupportedFontWeights.Contains(settings.FontWeight) ? settings.FontWeight : "Normal",
            LineHeight = settings.LineHeight > 0 ? settings.LineHeight : 1.5,
            LetterSpacing = settings.LetterSpacing >= 0 ? settings.LetterSpacing : 0,
            TextRendering = settings.TextRendering,
            TextFormatting = settings.TextFormatting,
            TextHinting = settings.TextHinting,
            EnableLigatures = settings.EnableLigatures,
            ShowZeroWidthChars = settings.ShowZeroWidthChars,
            VisualizeWhitespace = settings.VisualizeWhitespace,
            TabSymbol = settings.TabSymbol,
            SpaceSymbol = settings.SpaceSymbol
        };
    }

    private static double Scale(double fontSize, double factor)
    {
        return Math.Round(fontSize * factor, 2, MidpointRounding.AwayFromZero);
    }
}
