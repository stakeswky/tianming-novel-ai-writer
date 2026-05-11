namespace TM.Framework.Appearance;

public readonly record struct PortableRgbColor(byte R, byte G, byte B)
{
    public string ToHex()
    {
        return $"#{R:X2}{G:X2}{B:X2}";
    }
}

public sealed class PortableImageAnalysisResult
{
    public double AvgBrightness { get; set; }

    public double DarkRatio { get; set; }

    public double LightRatio { get; set; }

    public bool IsDark { get; set; }

    public string ThemeType { get; set; } = "light";

    public string TextColor { get; set; } = "#212529";

    public string Notes { get; set; } = string.Empty;
}

public sealed class PortableImageThemeColors
{
    public PortableRgbColor Primary { get; set; }

    public PortableRgbColor Secondary { get; set; }

    public bool IsDarkTheme { get; set; }

    public PortableRgbColor TextColor { get; set; }
}

public static class PortableImageColorAnalyzer
{
    private const int QuantizeStep = 30;
    private const int DiversityThreshold = 60;

    public static IReadOnlyList<PortableRgbColor> ReadBgra32Pixels(
        byte[] pixelData,
        int width,
        int height,
        int stride,
        int maxDimension = 200)
    {
        ArgumentNullException.ThrowIfNull(pixelData);
        if (width <= 0 || height <= 0 || maxDimension <= 0)
        {
            return Array.Empty<PortableRgbColor>();
        }

        if (stride < width * 4)
        {
            throw new ArgumentException("Stride must contain at least width * 4 bytes.", nameof(stride));
        }

        var requiredBytes = (height - 1) * stride + width * 4;
        if (pixelData.Length < requiredBytes)
        {
            throw new ArgumentException("Pixel buffer is smaller than the declared dimensions.", nameof(pixelData));
        }

        var sampleStep = Math.Max(1, (int)Math.Ceiling(Math.Max(width, height) / (double)maxDimension));
        var pixels = new List<PortableRgbColor>();
        for (var y = 0; y < height; y += sampleStep)
        {
            for (var x = 0; x < width; x += sampleStep)
            {
                var index = y * stride + x * 4;
                pixels.Add(new PortableRgbColor(
                    pixelData[index + 2],
                    pixelData[index + 1],
                    pixelData[index]));
            }
        }

        return pixels;
    }

    public static IReadOnlyList<PortableRgbColor> ExtractPalette(
        IEnumerable<PortableRgbColor> pixels,
        int count)
    {
        if (count <= 0)
        {
            return Array.Empty<PortableRgbColor>();
        }

        var allPixels = pixels.ToList();
        if (allPixels.Count == 0)
        {
            return new[] { new PortableRgbColor(100, 100, 100) };
        }

        var filteredPixels = allPixels
            .Where(pixel =>
            {
                var brightness = (pixel.R + pixel.G + pixel.B) / 3.0;
                return brightness > 20 && brightness < 235;
            })
            .ToList();

        if (filteredPixels.Count == 0)
        {
            filteredPixels = allPixels;
        }

        var rankedColors = filteredPixels
            .Select(Quantize)
            .GroupBy(color => color)
            .Select(group => new { Color = group.Key, Count = group.Count() })
            .OrderByDescending(entry => entry.Count)
            .Take(count * 2)
            .Select(entry => entry.Color)
            .ToList();

        return SelectDiverseColors(rankedColors, count);
    }

    public static PortableImageAnalysisResult Analyze(IEnumerable<PortableRgbColor> pixels)
    {
        var brightnessValues = pixels.Select(CalculateBrightness).ToList();
        if (brightnessValues.Count == 0)
        {
            return new PortableImageAnalysisResult
            {
                AvgBrightness = 128,
                IsDark = false,
                ThemeType = "light",
                TextColor = "#212529",
                Notes = "图片分析失败，使用默认配置"
            };
        }

        var avgBrightness = brightnessValues.Average();
        var darkRatio = brightnessValues.Count(brightness => brightness < 85) / (double)brightnessValues.Count;
        var lightRatio = brightnessValues.Count(brightness => brightness > 170) / (double)brightnessValues.Count;
        var isDark = avgBrightness < 128 || darkRatio > 0.5;

        return BuildRecommendation(avgBrightness, darkRatio, lightRatio, isDark);
    }

    public static PortableImageThemeColors GenerateThemeColors(
        PortableRgbColor primaryColor,
        IEnumerable<PortableRgbColor> pixels)
    {
        var pixelList = pixels.ToList();
        var palette = ExtractPalette(pixelList, 5);
        var analysis = Analyze(pixelList);
        var secondaryColor = palette
            .OrderByDescending(color => CalculateDistance(primaryColor, color))
            .FirstOrDefault(primaryColor);

        return new PortableImageThemeColors
        {
            Primary = primaryColor,
            Secondary = secondaryColor,
            IsDarkTheme = analysis.IsDark,
            TextColor = analysis.IsDark
                ? new PortableRgbColor(255, 255, 255)
                : new PortableRgbColor(33, 37, 41)
        };
    }

    public static PortableThemeDesignSnapshot CreateThemeSnapshot(
        string themeName,
        PortableImageThemeColors colors)
    {
        ArgumentNullException.ThrowIfNull(colors);

        var primary = colors.Primary.ToHex();
        var secondary = colors.Secondary.ToHex();
        var text = colors.TextColor.ToHex();
        var isDark = colors.IsDarkTheme;

        return new PortableThemeDesignSnapshot
        {
            ThemeName = string.IsNullOrWhiteSpace(themeName) ? "ImageColor" : themeName.Trim(),
            TopBarBackground = isDark ? "#1A1A1A" : "#F8F9FA",
            TopBarText = text,
            LeftBarBackground = isDark ? "#252525" : "#F1F3F5",
            LeftBarIconColor = secondary,
            LeftWorkspaceBackground = isDark ? "#2D2D2D" : "#F1F3F5",
            LeftWorkspaceText = text,
            LeftWorkspaceBorder = isDark ? "#3A3A3A" : "#DEE2E6",
            CenterWorkspaceBackground = isDark ? "#2D2D2D" : "#FFFFFF",
            CenterWorkspaceText = text,
            CenterWorkspaceBorder = isDark ? "#3A3A3A" : "#CED4DA",
            RightWorkspaceBackground = isDark ? "#252525" : "#F1F3F5",
            RightWorkspaceText = text,
            RightWorkspaceBorder = isDark ? "#3A3A3A" : "#DEE2E6",
            BottomBarBackground = isDark ? "#1A1A1A" : "#F8F9FA",
            BottomBarText = text,
            PrimaryButtonColor = primary,
            PrimaryButtonHover = PortableThemeDesigner.Lighten(primary, 0.15),
            DangerButtonColor = "#EF4444",
            DangerButtonHover = "#DC2626"
        };
    }

    private static PortableRgbColor Quantize(PortableRgbColor color)
    {
        return new PortableRgbColor(
            QuantizeChannel(color.R),
            QuantizeChannel(color.G),
            QuantizeChannel(color.B));
    }

    private static byte QuantizeChannel(byte value)
    {
        return (byte)((value / QuantizeStep) * QuantizeStep);
    }

    private static List<PortableRgbColor> SelectDiverseColors(
        IReadOnlyList<PortableRgbColor> colors,
        int count)
    {
        if (colors.Count <= count)
        {
            return colors.ToList();
        }

        var selected = new List<PortableRgbColor> { colors[0] };
        foreach (var color in colors.Skip(1))
        {
            if (selected.Count >= count)
            {
                break;
            }

            if (selected.All(selectedColor => CalculateDistance(color, selectedColor) >= DiversityThreshold))
            {
                selected.Add(color);
            }
        }

        return selected;
    }

    private static double CalculateDistance(PortableRgbColor left, PortableRgbColor right)
    {
        return Math.Sqrt(
            Math.Pow(left.R - right.R, 2) +
            Math.Pow(left.G - right.G, 2) +
            Math.Pow(left.B - right.B, 2));
    }

    private static double CalculateBrightness(PortableRgbColor color)
    {
        return 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;
    }

    private static PortableImageAnalysisResult BuildRecommendation(
        double avgBrightness,
        double darkRatio,
        double lightRatio,
        bool isDark)
    {
        var result = new PortableImageAnalysisResult
        {
            AvgBrightness = avgBrightness,
            DarkRatio = darkRatio,
            LightRatio = lightRatio,
            IsDark = isDark,
            ThemeType = isDark ? "dark" : "light",
            TextColor = isDark ? "#ffffff" : "#212529",
            Notes = isDark
                ? "检测到暗色图片，建议使用深色主题配色，文字使用亮色并添加阴影。"
                : "检测到亮色图片，建议使用浅色主题配色，文字使用深色。"
        };

        if (avgBrightness < 50)
        {
            result.Notes += "\n图片非常暗，建议大幅提高UI不透明度。";
        }
        else if (avgBrightness > 200)
        {
            result.Notes += "\n图片非常亮，建议降低UI不透明度。";
        }

        return result;
    }
}
