using System.Globalization;
using System.Text;

namespace TM.Framework.Appearance;

public sealed class PortableThemeDesignSnapshot
{
    public string ThemeName { get; set; } = "Custom Theme";
    public string TopBarBackground { get; set; } = "#1E1E1E";
    public string TopBarText { get; set; } = "#FFFFFF";
    public string LeftBarBackground { get; set; } = "#191919";
    public string LeftBarIconColor { get; set; } = "#6496FA";
    public string LeftWorkspaceBackground { get; set; } = "#232323";
    public string LeftWorkspaceText { get; set; } = "#FFFFFF";
    public string LeftWorkspaceBorder { get; set; } = "#3C3C3C";
    public string CenterWorkspaceBackground { get; set; } = "#282828";
    public string CenterWorkspaceText { get; set; } = "#FFFFFF";
    public string CenterWorkspaceBorder { get; set; } = "#3C3C3C";
    public string RightWorkspaceBackground { get; set; } = "#232323";
    public string RightWorkspaceText { get; set; } = "#FFFFFF";
    public string RightWorkspaceBorder { get; set; } = "#3C3C3C";
    public string BottomBarBackground { get; set; } = "#141414";
    public string BottomBarText { get; set; } = "#FFFFFF";
    public string PrimaryButtonColor { get; set; } = "#60A5FA";
    public string PrimaryButtonHover { get; set; } = "#3B82F6";
    public string DangerButtonColor { get; set; } = "#F87171";
    public string DangerButtonHover { get; set; } = "#EF4444";

    public static PortableThemeDesignSnapshot CreateDefault()
    {
        return new PortableThemeDesignSnapshot();
    }
}

public sealed class PortableContrastWarning
{
    public string Area { get; init; } = string.Empty;
    public double Ratio { get; init; }
    public string Level { get; init; } = string.Empty;
    public string Recommendation { get; init; } = string.Empty;
}

public sealed class PortableThemeSaveResult
{
    public bool Success { get; init; }
    public string? FileName { get; init; }
    public string? FilePath { get; init; }
    public string? ErrorMessage { get; init; }
}

public static class PortableThemeDesigner
{
    public static string GenerateThemeXaml(PortableThemeDesignSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<ResourceDictionary xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"");
        sb.AppendLine("                    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
        sb.AppendLine("    ");
        sb.AppendLine($"    <!-- {snapshot.ThemeName} -->");
        sb.AppendLine("    ");
        sb.AppendLine("    <!-- Background colors -->");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"UnifiedBackground\" Color=\"{NormalizeColor(snapshot.TopBarBackground)}\"/>");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"ContentBackground\" Color=\"{NormalizeColor(snapshot.CenterWorkspaceBackground)}\"/>");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"Surface\" Color=\"{NormalizeColor(snapshot.LeftWorkspaceBackground)}\"/>");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"ContentHighlight\" Color=\"{NormalizeColor(snapshot.LeftWorkspaceBackground)}\"/>");
        sb.AppendLine("    ");
        sb.AppendLine("    <!-- Border colors -->");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"WindowBorder\" Color=\"{NormalizeColor(snapshot.LeftWorkspaceBorder)}\"/>");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"BorderBrush\" Color=\"{NormalizeColor(snapshot.CenterWorkspaceBorder)}\"/>");
        sb.AppendLine("    ");
        sb.AppendLine("    <!-- Text colors -->");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"TextPrimary\" Color=\"{NormalizeColor(snapshot.CenterWorkspaceText)}\"/>");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"TextSecondary\" Color=\"{NormalizeColor(snapshot.TopBarText)}\"/>");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"TextTertiary\" Color=\"{NormalizeColor(snapshot.BottomBarText)}\"/>");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"TextDisabled\" Color=\"{Darken(snapshot.CenterWorkspaceText, 0.5)}\"/>");
        sb.AppendLine("    ");
        sb.AppendLine("    <!-- Interactive state colors -->");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"HoverBackground\" Color=\"{Lighten(snapshot.LeftBarBackground, 0.2)}\"/>");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"ActiveBackground\" Color=\"{Lighten(snapshot.LeftBarBackground, 0.3)}\"/>");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"SelectedBackground\" Color=\"{NormalizeColor(snapshot.LeftBarIconColor)}\"/>");
        sb.AppendLine("    ");
        sb.AppendLine("    <!-- Theme colors -->");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"PrimaryColor\" Color=\"{NormalizeColor(snapshot.PrimaryButtonColor)}\"/>");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"PrimaryHover\" Color=\"{NormalizeColor(snapshot.PrimaryButtonHover)}\"/>");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"PrimaryActive\" Color=\"{Darken(snapshot.PrimaryButtonHover, 0.2)}\"/>");
        sb.AppendLine("    ");
        sb.AppendLine("    <!-- Semantic colors -->");
        sb.AppendLine("    <SolidColorBrush x:Key=\"SuccessColor\" Color=\"#34D399\"/>");
        sb.AppendLine("    <SolidColorBrush x:Key=\"WarningColor\" Color=\"#FBBF24\"/>");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"DangerColor\" Color=\"{NormalizeColor(snapshot.DangerButtonColor)}\"/>");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"DangerHover\" Color=\"{NormalizeColor(snapshot.DangerButtonHover)}\"/>");
        sb.AppendLine($"    <SolidColorBrush x:Key=\"InfoColor\" Color=\"{NormalizeColor(snapshot.PrimaryButtonColor)}\"/>");
        sb.AppendLine("    ");
        sb.AppendLine("</ResourceDictionary>");

        return sb.ToString();
    }

    public static async Task<PortableThemeSaveResult> SaveThemeAsync(
        string themesDirectory,
        PortableThemeDesignSnapshot snapshot,
        Func<string, bool>? overwriteExisting = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(snapshot.ThemeName))
        {
            return new PortableThemeSaveResult
            {
                Success = false,
                ErrorMessage = "Theme name is required."
            };
        }

        Directory.CreateDirectory(themesDirectory);
        var fileName = SanitizeThemeFileName(snapshot.ThemeName);
        var filePath = Path.Combine(themesDirectory, fileName);

        if (File.Exists(filePath) && overwriteExisting?.Invoke(fileName) != true)
        {
            return new PortableThemeSaveResult
            {
                Success = false,
                FileName = fileName,
                FilePath = filePath,
                ErrorMessage = "Theme already exists."
            };
        }

        var tempPath = filePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, GenerateThemeXaml(snapshot), Encoding.UTF8, cancellationToken)
            .ConfigureAwait(false);
        File.Move(tempPath, filePath, overwrite: true);

        return new PortableThemeSaveResult
        {
            Success = true,
            FileName = fileName,
            FilePath = filePath
        };
    }

    public static string SanitizeThemeFileName(string themeName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var cleanName = string.Join(string.Empty, themeName.Where(ch => !invalidChars.Contains(ch)));
        cleanName = cleanName.Trim();
        if (string.IsNullOrWhiteSpace(cleanName))
        {
            cleanName = "Custom";
        }

        return cleanName + "Theme.xaml";
    }

    public static IReadOnlyList<PortableContrastWarning> AnalyzeContrast(PortableThemeDesignSnapshot snapshot)
    {
        var warnings = new List<PortableContrastWarning>();
        CheckContrast(warnings, "top", snapshot.TopBarText, snapshot.TopBarBackground);
        CheckContrast(warnings, "left", snapshot.LeftWorkspaceText, snapshot.LeftWorkspaceBackground);
        CheckContrast(warnings, "center", snapshot.CenterWorkspaceText, snapshot.CenterWorkspaceBackground);
        CheckContrast(warnings, "right", snapshot.RightWorkspaceText, snapshot.RightWorkspaceBackground);
        CheckContrast(warnings, "bottom", snapshot.BottomBarText, snapshot.BottomBarBackground);
        return warnings;
    }

    public static double CalculateContrastRatio(string foreground, string background)
    {
        var foregroundColor = ParseColor(foreground);
        var backgroundColor = ParseColor(background);
        var foregroundLuminance = GetRelativeLuminance(foregroundColor);
        var backgroundLuminance = GetRelativeLuminance(backgroundColor);
        var lighter = Math.Max(foregroundLuminance, backgroundLuminance);
        var darker = Math.Min(foregroundLuminance, backgroundLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    public static string Lighten(string color, double factor)
    {
        var (r, g, b) = ParseColor(color);
        return ToHex(
            Math.Min(255, (int)(r + (255 - r) * factor)),
            Math.Min(255, (int)(g + (255 - g) * factor)),
            Math.Min(255, (int)(b + (255 - b) * factor)));
    }

    public static string Darken(string color, double factor)
    {
        var (r, g, b) = ParseColor(color);
        return ToHex(
            (int)(r * (1 - factor)),
            (int)(g * (1 - factor)),
            (int)(b * (1 - factor)));
    }

    private static void CheckContrast(
        List<PortableContrastWarning> warnings,
        string area,
        string foreground,
        string background)
    {
        var ratio = CalculateContrastRatio(foreground, background);
        if (ratio >= 4.5)
        {
            return;
        }

        warnings.Add(new PortableContrastWarning
        {
            Area = area,
            Ratio = ratio,
            Level = ratio < 3.0 ? "critical" : "warning",
            Recommendation = ratio < 3.0
                ? "Contrast is too low for readable text."
                : "Contrast is below the recommended threshold."
        });
    }

    private static double GetRelativeLuminance((int R, int G, int B) color)
    {
        var r = GetSrgb(color.R / 255.0);
        var g = GetSrgb(color.G / 255.0);
        var b = GetSrgb(color.B / 255.0);
        return 0.2126 * r + 0.7152 * g + 0.0722 * b;
    }

    private static double GetSrgb(double channel)
    {
        return channel <= 0.03928
            ? channel / 12.92
            : Math.Pow((channel + 0.055) / 1.055, 2.4);
    }

    private static string NormalizeColor(string color)
    {
        var (r, g, b) = ParseColor(color);
        return ToHex(r, g, b);
    }

    private static (int R, int G, int B) ParseColor(string color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            throw new ArgumentException("Color is required.", nameof(color));
        }

        var hex = color.Trim();
        if (hex.StartsWith('#'))
        {
            hex = hex[1..];
        }

        if (hex.Length != 6)
        {
            throw new ArgumentException($"Color must be a 6-digit hex value: {color}", nameof(color));
        }

        return (
            int.Parse(hex[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(hex[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
            int.Parse(hex[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    private static string ToHex(int r, int g, int b)
    {
        return $"#{r:X2}{g:X2}{b:X2}";
    }
}
