using System.Text.Json;
using System.Text.Json.Serialization;

namespace TM.Framework.Appearance;

public enum PortableFontCategory
{
    All,
    Monospace,
    Serif,
    SansSerif,
    Script,
    Decorative,
    CJK,
    System
}

public sealed class PortableFontRecommendation
{
    public string FontName { get; init; } = string.Empty;

    public string Reason { get; init; } = string.Empty;

    public double Score { get; init; }
}

public sealed class PortableFontUsageData
{
    [JsonPropertyName("FavoriteFonts")] public List<string> FavoriteFonts { get; set; } = new();

    [JsonPropertyName("RecentFonts")] public List<PortableFontUsageEntry> RecentFonts { get; set; } = new();
}

public sealed class PortableFontUsageEntry
{
    [JsonPropertyName("FontName")] public string FontName { get; set; } = string.Empty;

    [JsonPropertyName("LastUsed")] public DateTime LastUsed { get; set; }

    [JsonPropertyName("UsageCount")] public int UsageCount { get; set; }
}

public static class PortableFontCatalog
{
    private static readonly Dictionary<string, PortableFontCategory> FontCategoryMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Consolas"] = PortableFontCategory.Monospace,
        ["Courier"] = PortableFontCategory.Monospace,
        ["Courier New"] = PortableFontCategory.Monospace,
        ["Lucida Console"] = PortableFontCategory.Monospace,
        ["Monaco"] = PortableFontCategory.Monospace,
        ["Menlo"] = PortableFontCategory.Monospace,
        ["Source Code Pro"] = PortableFontCategory.Monospace,
        ["Fira Code"] = PortableFontCategory.Monospace,
        ["JetBrains Mono"] = PortableFontCategory.Monospace,
        ["Cascadia Code"] = PortableFontCategory.Monospace,
        ["Cascadia Mono"] = PortableFontCategory.Monospace,
        ["Inconsolata"] = PortableFontCategory.Monospace,
        ["DejaVu Sans Mono"] = PortableFontCategory.Monospace,
        ["Roboto Mono"] = PortableFontCategory.Monospace,
        ["IBM Plex Mono"] = PortableFontCategory.Monospace,
        ["Times New Roman"] = PortableFontCategory.Serif,
        ["Georgia"] = PortableFontCategory.Serif,
        ["Palatino"] = PortableFontCategory.Serif,
        ["Garamond"] = PortableFontCategory.Serif,
        ["Baskerville"] = PortableFontCategory.Serif,
        ["宋体"] = PortableFontCategory.Serif,
        ["SimSun"] = PortableFontCategory.Serif,
        ["NSimSun"] = PortableFontCategory.Serif,
        ["FangSong"] = PortableFontCategory.Serif,
        ["仿宋"] = PortableFontCategory.Serif,
        ["Arial"] = PortableFontCategory.SansSerif,
        ["Helvetica"] = PortableFontCategory.SansSerif,
        ["Helvetica Neue"] = PortableFontCategory.SansSerif,
        ["Verdana"] = PortableFontCategory.SansSerif,
        ["Tahoma"] = PortableFontCategory.SansSerif,
        ["Trebuchet MS"] = PortableFontCategory.SansSerif,
        ["Segoe UI"] = PortableFontCategory.SansSerif,
        ["Calibri"] = PortableFontCategory.SansSerif,
        ["Roboto"] = PortableFontCategory.SansSerif,
        ["Open Sans"] = PortableFontCategory.SansSerif,
        ["Lato"] = PortableFontCategory.SansSerif,
        ["Microsoft YaHei"] = PortableFontCategory.SansSerif,
        ["Microsoft YaHei UI"] = PortableFontCategory.SansSerif,
        ["微软雅黑"] = PortableFontCategory.SansSerif,
        ["黑体"] = PortableFontCategory.SansSerif,
        ["SimHei"] = PortableFontCategory.SansSerif,
        ["Comic Sans MS"] = PortableFontCategory.Script,
        ["Brush Script MT"] = PortableFontCategory.Script,
        ["Lucida Handwriting"] = PortableFontCategory.Script,
        ["楷体"] = PortableFontCategory.Script,
        ["KaiTi"] = PortableFontCategory.Script,
        ["行楷"] = PortableFontCategory.Script,
        ["华文行楷"] = PortableFontCategory.Script,
        ["Impact"] = PortableFontCategory.Decorative,
        ["Papyrus"] = PortableFontCategory.Decorative,
        ["Curlz MT"] = PortableFontCategory.Decorative,
        ["Jokerman"] = PortableFontCategory.Decorative
    };

    private static readonly HashSet<string> MonospaceFonts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Consolas", "Courier", "Courier New", "Lucida Console", "Monaco",
        "Menlo", "Source Code Pro", "Fira Code", "JetBrains Mono",
        "Cascadia Code", "Cascadia Mono", "Inconsolata", "DejaVu Sans Mono",
        "Roboto Mono", "IBM Plex Mono", "Noto Sans Mono", "Ubuntu Mono"
    };

    private static readonly HashSet<string> CjkFonts = new(StringComparer.OrdinalIgnoreCase)
    {
        "Microsoft YaHei", "Microsoft YaHei UI", "微软雅黑", "SimSun", "宋体",
        "SimHei", "黑体", "KaiTi", "楷体", "FangSong", "仿宋", "NSimSun",
        "Microsoft JhengHei", "微软正黑体", "PMingLiU", "新细明体", "DFKai-SB",
        "标楷体", "Hiragino Sans", "ヒラギノ角ゴシック", "Hiragino Kaku Gothic Pro",
        "Yu Gothic", "游ゴシック", "Meiryo", "メイリオ", "MS Gothic", "MS UI Gothic",
        "SimSun-ExtB", "MingLiU", "MingLiU_HKSCS", "Apple LiGothic", "LiHei Pro",
        "Noto Sans CJK", "Source Han Sans", "思源黑体", "Noto Serif CJK",
        "Source Han Serif", "思源宋体", "华文黑体", "华文宋体", "华文仿宋",
        "华文楷体", "华文细黑", "华文中宋", "华文新魏", "方正", "文泉驿",
        "PingFang SC", "苹方"
    };

    private static readonly Dictionary<PortableThemeType, List<string>> ThemeToFontMap = new()
    {
        [PortableThemeType.Light] =
        [
            "Segoe UI",
            "Microsoft YaHei UI",
            "微软雅黑",
            "Calibri",
            "Arial",
            "Noto Sans"
        ],
        [PortableThemeType.Dark] =
        [
            "SF Pro Display",
            "苹方",
            "PingFang SC",
            "Roboto",
            "Helvetica Neue",
            "Microsoft YaHei UI"
        ],
        [PortableThemeType.Green] =
        [
            "Microsoft YaHei UI",
            "Segoe UI",
            "Calibri"
        ],
        [PortableThemeType.Business] =
        [
            "Calibri",
            "Arial",
            "Microsoft YaHei UI"
        ]
    };

    public static PortableFontCategory ClassifyFont(string fontName)
    {
        if (string.IsNullOrWhiteSpace(fontName))
        {
            return PortableFontCategory.System;
        }

        if (FontCategoryMap.TryGetValue(fontName, out var category))
        {
            return category;
        }

        if (CjkFonts.Any(cjk => fontName.Contains(cjk, StringComparison.OrdinalIgnoreCase)))
        {
            return PortableFontCategory.CJK;
        }

        if (MonospaceFonts.Contains(fontName)
            || fontName.Contains("Mono", StringComparison.OrdinalIgnoreCase)
            || fontName.Contains("Code", StringComparison.OrdinalIgnoreCase)
            || fontName.Contains("Console", StringComparison.OrdinalIgnoreCase))
        {
            return PortableFontCategory.Monospace;
        }

        return PortableFontCategory.SansSerif;
    }

    public static IReadOnlyList<PortableFontRecommendation> GetThemeRecommendations(
        PortableThemeType themeType,
        IReadOnlyList<string> availableFonts)
    {
        if (!ThemeToFontMap.TryGetValue(themeType, out var recommendedFonts))
        {
            recommendedFonts = ThemeToFontMap[PortableThemeType.Light];
        }

        var available = new HashSet<string>(availableFonts, StringComparer.OrdinalIgnoreCase);
        return recommendedFonts
            .Where(available.Contains)
            .Select(fontName => new PortableFontRecommendation
            {
                FontName = fontName,
                Reason = GetRecommendationReason(fontName, themeType),
                Score = CalculateScore(fontName, themeType)
            })
            .OrderByDescending(recommendation => recommendation.Score)
            .Take(3)
            .ToList();
    }

    public static IReadOnlyList<string> GenerateTags(string fontName, PortableFontCategory category)
    {
        var tags = new List<string>();
        if (IsMonospaceByName(fontName))
        {
            tags.Add("等宽");
        }

        switch (category)
        {
            case PortableFontCategory.CJK:
                tags.Add("CJK");
                break;
            case PortableFontCategory.Serif:
                tags.Add("衬线");
                break;
            case PortableFontCategory.SansSerif:
                tags.Add("非衬线");
                break;
            case PortableFontCategory.Script:
                tags.Add("手写");
                break;
            case PortableFontCategory.Decorative:
                tags.Add("装饰");
                break;
        }

        return tags;
    }

    private static bool IsMonospaceByName(string fontName)
    {
        return !string.IsNullOrWhiteSpace(fontName)
            && (MonospaceFonts.Contains(fontName)
                || fontName.Contains("Mono", StringComparison.OrdinalIgnoreCase)
                || fontName.Contains("Code", StringComparison.OrdinalIgnoreCase)
                || fontName.Contains("Console", StringComparison.OrdinalIgnoreCase));
    }

    private static string GetRecommendationReason(string fontName, PortableThemeType themeType)
    {
        var category = ClassifyFont(fontName);
        return (fontName, themeType) switch
        {
            ("Segoe UI", PortableThemeType.Light) => "现代简洁,阅读友好,适合浅色界面",
            ("Microsoft YaHei UI", PortableThemeType.Light) => "中文优化,清晰易读,Windows标准字体",
            ("微软雅黑", PortableThemeType.Light) => "中文优化,清晰易读,Windows标准字体",
            ("Calibri", PortableThemeType.Light) => "专业办公,易读性高,Office默认字体",
            ("Arial", PortableThemeType.Light) => "经典通用,广泛兼容,跨平台标准",
            ("Noto Sans", PortableThemeType.Light) => "Google设计,多语言支持,开源优选",
            ("SF Pro Display", PortableThemeType.Dark) => "Apple设计,深色优化,现代美观",
            ("苹方", PortableThemeType.Dark) => "Apple中文字体,深色界面清晰,专业设计",
            ("PingFang SC", PortableThemeType.Dark) => "Apple中文字体,深色界面清晰,专业设计",
            ("Roboto", PortableThemeType.Dark) => "Material Design,深色优化,现代简洁",
            ("Helvetica Neue", PortableThemeType.Dark) => "经典无衬线,深色界面友好,专业设计",
            _ when category == PortableFontCategory.SansSerif => "无衬线字体,界面友好,易读性好",
            _ when category == PortableFontCategory.Serif => "衬线字体,专业正式,适合长文本",
            _ when category == PortableFontCategory.Monospace => "等宽字体,代码友好,数字对齐",
            _ when category == PortableFontCategory.CJK => "中日韩优化,本地化支持,字符完整",
            _ => "通用字体,兼容性好,适用多数场景"
        };
    }

    private static double CalculateScore(string fontName, PortableThemeType themeType)
    {
        double score = 50;
        var category = ClassifyFont(fontName);
        if (category is PortableFontCategory.SansSerif or PortableFontCategory.CJK)
        {
            score += 20;
        }

        if (ThemeToFontMap.TryGetValue(themeType, out var recommendedFonts))
        {
            var index = recommendedFonts.FindIndex(font => string.Equals(font, fontName, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
            {
                score += 30 - index * 5;
            }
        }

        if (fontName.Contains("Microsoft", StringComparison.OrdinalIgnoreCase)
            || fontName.Contains("Segoe", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }

        return Math.Min(score, 100);
    }
}

public sealed class FileFontUsageStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string _filePath;
    private readonly Func<DateTime> _clock;

    public FileFontUsageStore(string filePath, Func<DateTime>? clock = null)
    {
        _filePath = string.IsNullOrWhiteSpace(filePath)
            ? throw new ArgumentException("Font usage file path is required.", nameof(filePath))
            : filePath;
        _clock = clock ?? (() => DateTime.Now);
    }

    public async Task<PortableFontUsageData> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return new PortableFontUsageData();
        }

        try
        {
            await using var stream = File.OpenRead(_filePath);
            var data = await JsonSerializer.DeserializeAsync<PortableFontUsageData>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
            return Normalize(data);
        }
        catch (JsonException)
        {
            return new PortableFontUsageData();
        }
        catch (IOException)
        {
            return new PortableFontUsageData();
        }
    }

    public async Task<bool> ToggleFavoriteAsync(string fontName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fontName))
        {
            return false;
        }

        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var added = !data.FavoriteFonts.Contains(fontName, StringComparer.OrdinalIgnoreCase);
        if (added)
        {
            data.FavoriteFonts.Add(fontName);
        }
        else
        {
            data.FavoriteFonts.RemoveAll(font => string.Equals(font, fontName, StringComparison.OrdinalIgnoreCase));
        }

        await SaveAsync(data, cancellationToken).ConfigureAwait(false);
        return added;
    }

    public async Task RecordUsageAsync(string fontName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fontName))
        {
            return;
        }

        var data = await LoadAsync(cancellationToken).ConfigureAwait(false);
        var existing = data.RecentFonts.FirstOrDefault(
            entry => string.Equals(entry.FontName, fontName, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.LastUsed = _clock();
            existing.UsageCount++;
        }
        else
        {
            data.RecentFonts.Add(new PortableFontUsageEntry
            {
                FontName = fontName,
                LastUsed = _clock(),
                UsageCount = 1
            });
        }

        data.RecentFonts = data.RecentFonts
            .OrderByDescending(entry => entry.LastUsed)
            .Take(20)
            .ToList();

        await SaveAsync(data, cancellationToken).ConfigureAwait(false);
    }

    private async Task SaveAsync(PortableFontUsageData data, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _filePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(
                stream,
                Normalize(data),
                JsonOptions,
                cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, _filePath, overwrite: true);
    }

    private static PortableFontUsageData Normalize(PortableFontUsageData? data)
    {
        return new PortableFontUsageData
        {
            FavoriteFonts = data?.FavoriteFonts ?? new List<string>(),
            RecentFonts = data?.RecentFonts?
                .OrderByDescending(entry => entry.LastUsed)
                .Take(20)
                .ToList() ?? new List<PortableFontUsageEntry>()
        };
    }
}
