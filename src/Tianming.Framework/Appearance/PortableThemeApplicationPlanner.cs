namespace TM.Framework.Appearance;

public enum PortableThemeColorMode
{
    Light,
    Dark
}

public sealed record PortableThemeApplicationPlan(
    PortableThemeType ThemeType,
    string DisplayName,
    string ResourceFileName,
    string ResourcePath,
    PortableThemeColorMode ColorMode,
    string PrimaryColor,
    string BackgroundColor,
    bool IsCustom);

public static class PortableThemeApplicationPlanner
{
    private const string ThemeResourceRoot = "Appearance/ThemeManagement/Themes";

    private static readonly IReadOnlyDictionary<PortableThemeType, PortableThemeMetadata> Metadata =
        new Dictionary<PortableThemeType, PortableThemeMetadata>
        {
            [PortableThemeType.Light] = new("浅色主题", "LightTheme.xaml", PortableThemeColorMode.Light, "#3B82F6", "#FFFFFF"),
            [PortableThemeType.Dark] = new("深色主题", "DarkTheme.xaml", PortableThemeColorMode.Dark, "#60A5FA", "#1E293B"),
            [PortableThemeType.Green] = new("护眼色", "GreenTheme.xaml", PortableThemeColorMode.Light, "#8B6914", "#F5EDDC"),
            [PortableThemeType.Business] = new("商务灰", "BusinessTheme.xaml", PortableThemeColorMode.Light, "#4A6FA5", "#F7F7F7"),
            [PortableThemeType.ModernBlue] = new("现代深蓝", "ModernBlueTheme.xaml", PortableThemeColorMode.Dark, "#1890FF", "#112240"),
            [PortableThemeType.Violet] = new("紫罗兰", "VioletTheme.xaml", PortableThemeColorMode.Light, "#7C3AED", "#F8F0FF"),
            [PortableThemeType.WarmOrange] = new("暖阳橙", "WarmOrangeTheme.xaml", PortableThemeColorMode.Light, "#E8780A", "#FFF7E6"),
            [PortableThemeType.Pink] = new("樱花粉", "PinkTheme.xaml", PortableThemeColorMode.Light, "#EB2F96", "#FFF0F6"),
            [PortableThemeType.TechCyan] = new("科技青", "TechCyanTheme.xaml", PortableThemeColorMode.Dark, "#13C2C2", "#0D2137"),
            [PortableThemeType.MinimalBlack] = new("极简黑", "MinimalBlackTheme.xaml", PortableThemeColorMode.Dark, "#6CB6FF", "#1A1A1A"),
            [PortableThemeType.Arctic] = new("北极蓝", "ArcticTheme.xaml", PortableThemeColorMode.Light, "#0284C7", "#F0F7FF"),
            [PortableThemeType.Forest] = new("森林绿", "ForestTheme.xaml", PortableThemeColorMode.Light, "#2E7D32", "#F1F8F2"),
            [PortableThemeType.Sunset] = new("日落橙", "SunsetTheme.xaml", PortableThemeColorMode.Light, "#E85D26", "#FFF4EC"),
            [PortableThemeType.Morandi] = new("莫兰迪", "MorandiTheme.xaml", PortableThemeColorMode.Light, "#7C9299", "#F5F4F2"),
            [PortableThemeType.HighContrast] = new("高对比度", "HighContrastTheme.xaml", PortableThemeColorMode.Dark, "#FFFF00", "#000000")
        };

    public static PortableThemeApplicationPlan CreateBuiltInPlan(
        PortableThemeType theme,
        PortableSystemThemeSnapshot? systemSnapshot = null)
    {
        var resolvedTheme = theme == PortableThemeType.Auto
            ? systemSnapshot?.IsLightTheme == false ? PortableThemeType.Dark : PortableThemeType.Light
            : theme;

        if (resolvedTheme == PortableThemeType.Custom)
        {
            throw new ArgumentException("Use CreateCustomPlan for custom themes.", nameof(theme));
        }

        if (!Metadata.TryGetValue(resolvedTheme, out var metadata))
        {
            metadata = Metadata[PortableThemeType.Light];
            resolvedTheme = PortableThemeType.Light;
        }

        return CreatePlan(resolvedTheme, metadata, isCustom: false);
    }

    public static PortableThemeApplicationPlan CreateCustomPlan(string themeFileName)
    {
        var normalized = NormalizeThemeFileName(themeFileName);
        var metadata = new PortableThemeMetadata(
            "自定义主题",
            normalized,
            PortableThemeColorMode.Light,
            "#3B82F6",
            "#FFFFFF");

        return CreatePlan(PortableThemeType.Custom, metadata, isCustom: true);
    }

    public static IReadOnlyList<PortableThemeApplicationPlan> GetAllBuiltInPlans()
    {
        return Metadata
            .Select(pair => CreatePlan(pair.Key, pair.Value, isCustom: false))
            .ToList();
    }

    public static bool TryParseTheme(string? value, out PortableThemeType theme)
    {
        theme = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = NormalizeThemeToken(value);
        foreach (var (candidate, metadata) in Metadata)
        {
            if (normalized == NormalizeThemeToken(candidate.ToString())
                || normalized == NormalizeThemeToken(metadata.ResourceFileName)
                || normalized == NormalizeThemeToken(metadata.DisplayName))
            {
                theme = candidate;
                return true;
            }
        }

        if (normalized == NormalizeThemeToken("Auto") || normalized == NormalizeThemeToken("跟随系统"))
        {
            theme = PortableThemeType.Auto;
            return true;
        }

        if (normalized == NormalizeThemeToken("Custom") || normalized == NormalizeThemeToken("自定义主题"))
        {
            theme = PortableThemeType.Custom;
            return true;
        }

        return false;
    }

    public static string NormalizeThemeFileName(string themeFileName)
    {
        if (string.IsNullOrWhiteSpace(themeFileName))
        {
            return "LightTheme.xaml";
        }

        var fileName = Path.GetFileName(themeFileName.Trim().Replace('\\', Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "LightTheme.xaml";
        }

        return fileName.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase)
            ? fileName
            : fileName + ".xaml";
    }

    private static PortableThemeApplicationPlan CreatePlan(
        PortableThemeType theme,
        PortableThemeMetadata metadata,
        bool isCustom)
    {
        return new PortableThemeApplicationPlan(
            theme,
            metadata.DisplayName,
            metadata.ResourceFileName,
            $"{ThemeResourceRoot}/{metadata.ResourceFileName}",
            metadata.ColorMode,
            metadata.PrimaryColor,
            metadata.BackgroundColor,
            isCustom);
    }

    private static string NormalizeThemeToken(string value)
    {
        return value
            .Trim()
            .Replace(".xaml", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("Theme", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("主题", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase)
            .ToUpperInvariant();
    }

    private sealed record PortableThemeMetadata(
        string DisplayName,
        string ResourceFileName,
        PortableThemeColorMode ColorMode,
        string PrimaryColor,
        string BackgroundColor);
}
