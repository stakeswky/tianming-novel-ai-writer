using System.Globalization;
using System.Text;
using System.Text.Json;

namespace TM.Framework.Appearance;

public sealed class PortableAIColorSchemeRequest
{
    public string Keywords { get; set; } = string.Empty;

    public string ColorHarmony { get; set; } = "互补色";

    public string ThemeType { get; set; } = "浅色主题";

    public string Emotion { get; set; } = "无";

    public string Scene { get; set; } = "通用";
}

public sealed class PortableAIColorSchemeCard
{
    public string SchemeName { get; set; } = string.Empty;

    public PortableRgbColor PrimaryColor { get; set; }

    public PortableRgbColor SecondaryColor { get; set; }

    public PortableRgbColor AccentColor { get; set; }

    public PortableRgbColor BackgroundColor { get; set; }

    public PortableRgbColor TextColor { get; set; }

    public string Harmony { get; set; } = string.Empty;

    public string ThemeType { get; set; } = string.Empty;

    public string Emotion { get; set; } = string.Empty;

    public string Scene { get; set; } = string.Empty;

    public int Score { get; set; }
}

public static class PortableAIColorSchemeCore
{
    public static string BuildPrompt(PortableAIColorSchemeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var isDark = request.ThemeType == "暗色主题";
        var bgConstraint = isDark
            ? "BackgroundColor必须为深色（如#1A1A2E、#0D1117等，亮度<30%），TextColor必须为浅色（如#E8E8E8、#F0F0F0等，亮度>70%）"
            : "BackgroundColor必须为浅色（如#F8F9FA、#FFFFFF等，亮度>85%），TextColor必须为深色（如#1F2937、#212529等，亮度<30%）";
        var keywordsText = string.IsNullOrWhiteSpace(request.Keywords) ? "随机（富有美感）" : request.Keywords.Trim();
        var emotionText = request.Emotion == "无" ? "不限" : request.Emotion;

        var sb = new StringBuilder();
        sb.AppendLine("<role>你是专业UI配色设计师。根据以下参数生成3套完整的应用程序主题配色方案。</role>");
        sb.AppendLine();
        sb.AppendLine("<params>");
        sb.AppendLine($"- 关键词/描述：{keywordsText}");
        sb.AppendLine($"- 色彩和谐规则：{request.ColorHarmony}");
        sb.AppendLine($"- 主题类型：{request.ThemeType}");
        sb.AppendLine($"- 情感色彩：{emotionText}");
        sb.AppendLine($"- 使用场景：{request.Scene}");
        sb.AppendLine("</params>");
        sb.AppendLine();
        sb.AppendLine("<output_format note=\"只输出JSON数组，不要任何额外文字、代码块标记或Markdown\">");
        sb.AppendLine("[");
        sb.AppendLine("  {");
        sb.AppendLine("    \"SchemeName\": \"方案名称（简洁中文，不超过8字）\",");
        sb.AppendLine("    \"PrimaryColor\": \"#RRGGBB\",");
        sb.AppendLine("    \"SecondaryColor\": \"#RRGGBB\",");
        sb.AppendLine("    \"AccentColor\": \"#RRGGBB\",");
        sb.AppendLine("    \"BackgroundColor\": \"#RRGGBB\",");
        sb.AppendLine("    \"TextColor\": \"#RRGGBB\"");
        sb.AppendLine("  }");
        sb.AppendLine("]");
        sb.AppendLine("</output_format>");
        sb.AppendLine();
        sb.AppendLine("<hard_constraints>");
        sb.AppendLine("1. 所有色值必须是#RRGGBB格式（6位十六进制）");
        sb.AppendLine($"2. {bgConstraint}");
        sb.AppendLine($"3. 严格遵循「{request.ColorHarmony}」色彩和谐规则，主色/辅色/强调色之间关系符合该规则");
        sb.AppendLine("4. PrimaryColor与BackgroundColor文字对比度必须≥4.5:1");
        sb.AppendLine("5. 3套方案之间颜色要有明显区分度，不能雷同");
        sb.AppendLine("6. 数组长度必须严格等于3");
        sb.AppendLine("</hard_constraints>");
        return sb.ToString();
    }

    public static IReadOnlyList<PortableAIColorSchemeCard> ParseSchemes(
        string content,
        PortableAIColorSchemeRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            var json = ExtractJsonArray(content);
            if (string.IsNullOrWhiteSpace(json))
            {
                return Array.Empty<PortableAIColorSchemeCard>();
            }

            var items = JsonSerializer.Deserialize<List<JsonElement>>(json);
            if (items is null)
            {
                return Array.Empty<PortableAIColorSchemeCard>();
            }

            var result = new List<PortableAIColorSchemeCard>();
            foreach (var item in items)
            {
                var primary = ParseColor(GetString(item, "PrimaryColor"));
                var background = ParseColor(GetString(item, "BackgroundColor"));
                var text = ParseColor(GetString(item, "TextColor"));
                var card = new PortableAIColorSchemeCard
                {
                    SchemeName = GetString(item, "SchemeName"),
                    PrimaryColor = primary,
                    SecondaryColor = ParseColor(GetString(item, "SecondaryColor")),
                    AccentColor = ParseColor(GetString(item, "AccentColor")),
                    BackgroundColor = background,
                    TextColor = text,
                    Harmony = request.ColorHarmony,
                    ThemeType = request.ThemeType,
                    Emotion = request.Emotion,
                    Scene = request.Scene,
                    Score = ComputeScore(primary, background, text)
                };
                if (string.IsNullOrWhiteSpace(card.SchemeName))
                {
                    card.SchemeName = $"配色方案{result.Count + 1}";
                }

                result.Add(card);
            }

            return result;
        }
        catch (JsonException)
        {
            return Array.Empty<PortableAIColorSchemeCard>();
        }
    }

    public static int ComputeScore(
        PortableRgbColor primary,
        PortableRgbColor background,
        PortableRgbColor text)
    {
        var contrast = CalculateContrastRatio(text, background);
        var score = 0;
        if (contrast >= 7.0)
        {
            score += 50;
        }
        else if (contrast >= 4.5)
        {
            score += 35;
        }
        else if (contrast >= 3.0)
        {
            score += 20;
        }

        var primaryContrast = CalculateContrastRatio(primary, background);
        if (primaryContrast >= 4.5)
        {
            score += 30;
        }
        else if (primaryContrast >= 3.0)
        {
            score += 15;
        }

        var saturation = GetSaturation(primary);
        if (saturation > 0.3 && saturation < 0.9)
        {
            score += 20;
        }
        else if (saturation >= 0.1)
        {
            score += 10;
        }

        return Math.Min(score, 100);
    }

    public static PortableThemeDesignSnapshot CreateThemeSnapshot(
        string themeName,
        PortableAIColorSchemeCard card)
    {
        ArgumentNullException.ThrowIfNull(card);

        var isDark = card.ThemeType == "暗色主题";
        var background = card.BackgroundColor.ToHex();
        var text = card.TextColor.ToHex();
        var primary = card.PrimaryColor.ToHex();
        var secondary = card.SecondaryColor.ToHex();

        return new PortableThemeDesignSnapshot
        {
            ThemeName = string.IsNullOrWhiteSpace(themeName) ? "AIColorScheme" : themeName.Trim(),
            TopBarBackground = background,
            TopBarText = text,
            LeftBarBackground = isDark
                ? PortableThemeDesigner.Lighten(background, 0.04)
                : PortableThemeDesigner.Darken(background, 0.03),
            LeftBarIconColor = secondary,
            LeftWorkspaceBackground = isDark
                ? PortableThemeDesigner.Lighten(background, 0.08)
                : PortableThemeDesigner.Darken(background, 0.03),
            LeftWorkspaceText = text,
            LeftWorkspaceBorder = isDark
                ? PortableThemeDesigner.Lighten(background, 0.2)
                : PortableThemeDesigner.Darken(background, 0.12),
            CenterWorkspaceBackground = isDark
                ? PortableThemeDesigner.Lighten(background, 0.08)
                : "#FFFFFF",
            CenterWorkspaceText = text,
            CenterWorkspaceBorder = isDark
                ? PortableThemeDesigner.Lighten(background, 0.25)
                : PortableThemeDesigner.Darken(background, 0.16),
            RightWorkspaceBackground = isDark
                ? PortableThemeDesigner.Lighten(background, 0.04)
                : PortableThemeDesigner.Darken(background, 0.03),
            RightWorkspaceText = text,
            RightWorkspaceBorder = isDark
                ? PortableThemeDesigner.Lighten(background, 0.2)
                : PortableThemeDesigner.Darken(background, 0.12),
            BottomBarBackground = background,
            BottomBarText = text,
            PrimaryButtonColor = primary,
            PrimaryButtonHover = PortableThemeDesigner.Lighten(primary, 0.15),
            DangerButtonColor = "#EF4444",
            DangerButtonHover = "#DC2626"
        };
    }

    private static string ExtractJsonArray(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var start = content.IndexOf('[');
        var end = content.LastIndexOf(']');
        return start < 0 || end <= start ? string.Empty : content[start..(end + 1)];
    }

    private static string GetString(JsonElement element, string key)
    {
        return element.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    private static PortableRgbColor ParseColor(string hex)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(hex))
            {
                return new PortableRgbColor(128, 128, 128);
            }

            var value = hex.Trim();
            if (value.StartsWith('#'))
            {
                value = value[1..];
            }

            if (value.Length != 6)
            {
                return new PortableRgbColor(128, 128, 128);
            }

            return new PortableRgbColor(
                byte.Parse(value[..2], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value[2..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                byte.Parse(value[4..6], NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }
        catch (FormatException)
        {
            return new PortableRgbColor(128, 128, 128);
        }
    }

    private static double CalculateContrastRatio(PortableRgbColor first, PortableRgbColor second)
    {
        var firstLuminance = GetRelativeLuminance(first);
        var secondLuminance = GetRelativeLuminance(second);
        var lighter = Math.Max(firstLuminance, secondLuminance);
        var darker = Math.Min(firstLuminance, secondLuminance);
        return (lighter + 0.05) / (darker + 0.05);
    }

    private static double GetRelativeLuminance(PortableRgbColor color)
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

    private static double GetSaturation(PortableRgbColor color)
    {
        var r = color.R / 255.0;
        var g = color.G / 255.0;
        var b = color.B / 255.0;
        var max = Math.Max(r, Math.Max(g, b));
        if (max == 0)
        {
            return 0;
        }

        var min = Math.Min(r, Math.Min(g, b));
        return (max - min) / max;
    }
}
