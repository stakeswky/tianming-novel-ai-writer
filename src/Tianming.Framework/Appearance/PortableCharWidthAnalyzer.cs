namespace TM.Framework.Appearance;

public enum PortableWidthCheckResult
{
    Pass,
    Warning,
    Fail
}

public sealed class PortableWidthCheckItem
{
    public string Category { get; set; } = string.Empty;

    public PortableWidthCheckResult Result { get; set; }

    public string Details { get; set; } = string.Empty;

    public double MinWidth { get; set; }

    public double MaxWidth { get; set; }

    public double Variance { get; set; }
}

public sealed class PortableCharWidthReport
{
    public string FontName { get; set; } = string.Empty;

    public double FontSize { get; set; }

    public List<PortableWidthCheckItem> Items { get; set; } = new();

    public PortableWidthCheckResult OverallResult { get; set; }

    public string Summary { get; set; } = string.Empty;

    public DateTime GeneratedAt { get; set; } = DateTime.Now;
}

public sealed class PortableCharWidthAnalyzer
{
    private readonly Func<string, double, string, IReadOnlyDictionary<char, double>> _measureCharacterWidths;

    public PortableCharWidthAnalyzer(
        Func<string, double, string, IReadOnlyDictionary<char, double>> measureCharacterWidths)
    {
        _measureCharacterWidths = measureCharacterWidths
            ?? throw new ArgumentNullException(nameof(measureCharacterWidths));
    }

    public PortableCharWidthReport AnalyzeFont(string fontName, double fontSize)
    {
        var report = new PortableCharWidthReport
        {
            FontName = fontName,
            FontSize = fontSize
        };

        try
        {
            report.Items.Add(CheckDigitWidths(fontName, fontSize));
            report.Items.Add(CheckLetterWidths(fontName, fontSize));
            report.Items.Add(CheckSpecialCharWidths(fontName, fontSize));
            report.Items.Add(CheckCjkWidths(fontName, fontSize));
            report.OverallResult = CalculateOverallResult(report.Items);
            report.Summary = GenerateSummary(report);
        }
        catch (Exception ex)
        {
            report.Items.Clear();
            report.OverallResult = PortableWidthCheckResult.Fail;
            report.Summary = $"分析失败: {ex.Message}";
        }

        return report;
    }

    private PortableWidthCheckItem CheckDigitWidths(string fontName, double fontSize)
    {
        return CheckVariance(
            fontName,
            fontSize,
            "0123456789",
            "数字 (0-9)",
            variancePercent => variancePercent < 1
                ? PortableWidthCheckResult.Pass
                : variancePercent < 5
                    ? PortableWidthCheckResult.Warning
                    : PortableWidthCheckResult.Fail);
    }

    private PortableWidthCheckItem CheckLetterWidths(string fontName, double fontSize)
    {
        return CheckVariance(
            fontName,
            fontSize,
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ",
            "字母 (a-z, A-Z)",
            variancePercent => variancePercent < 5
                ? PortableWidthCheckResult.Pass
                : variancePercent < 30
                    ? PortableWidthCheckResult.Warning
                    : PortableWidthCheckResult.Fail);
    }

    private PortableWidthCheckItem CheckSpecialCharWidths(string fontName, double fontSize)
    {
        return CheckVariance(
            fontName,
            fontSize,
            "[]{}()<>",
            "括号符号 ([]{}()<>)",
            variancePercent => variancePercent < 10
                ? PortableWidthCheckResult.Pass
                : variancePercent < 25
                    ? PortableWidthCheckResult.Warning
                    : PortableWidthCheckResult.Fail);
    }

    private PortableWidthCheckItem CheckCjkWidths(string fontName, double fontSize)
    {
        const string cjkChars = "中文汉字测试";
        var widths = MeasureWidths(fontName, fontSize, cjkChars);
        if (widths.Count == 0 || widths.All(width => width == 0))
        {
            return new PortableWidthCheckItem
            {
                Category = "CJK字符",
                Result = PortableWidthCheckResult.Warning,
                Details = "字体不支持CJK字符",
                MinWidth = 0,
                MaxWidth = 0,
                Variance = 0
            };
        }

        var (minWidth, maxWidth, variancePercent) = CalculateVariance(widths);
        var latinWidth = MeasureWidths(fontName, fontSize, "a").FirstOrDefault();
        var average = widths.Average();
        var isDoubleWidth = latinWidth > 0 && average >= latinWidth * 1.8 && average <= latinWidth * 2.2;
        var result = isDoubleWidth && variancePercent < 5
            ? PortableWidthCheckResult.Pass
            : isDoubleWidth || variancePercent < 10
                ? PortableWidthCheckResult.Warning
                : PortableWidthCheckResult.Fail;

        return new PortableWidthCheckItem
        {
            Category = "CJK字符",
            Result = result,
            Details = $"宽度范围: {minWidth:F2}~{maxWidth:F2}px, 差异率: {variancePercent:F2}%, {(isDoubleWidth ? "双宽正常" : "宽度异常")}",
            MinWidth = minWidth,
            MaxWidth = maxWidth,
            Variance = variancePercent
        };
    }

    private PortableWidthCheckItem CheckVariance(
        string fontName,
        double fontSize,
        string characters,
        string category,
        Func<double, PortableWidthCheckResult> classify)
    {
        var widths = MeasureWidths(fontName, fontSize, characters);
        var (minWidth, maxWidth, variancePercent) = CalculateVariance(widths);
        return new PortableWidthCheckItem
        {
            Category = category,
            Result = classify(variancePercent),
            Details = $"宽度范围: {minWidth:F2}~{maxWidth:F2}px, 差异率: {variancePercent:F2}%",
            MinWidth = minWidth,
            MaxWidth = maxWidth,
            Variance = variancePercent
        };
    }

    private List<double> MeasureWidths(string fontName, double fontSize, string characters)
    {
        var measured = _measureCharacterWidths(fontName, fontSize, characters);
        return characters
            .Where(measured.ContainsKey)
            .Select(ch => measured[ch])
            .ToList();
    }

    private static (double MinWidth, double MaxWidth, double VariancePercent) CalculateVariance(IReadOnlyList<double> widths)
    {
        var minWidth = widths.Min();
        var maxWidth = widths.Max();
        var average = widths.Average();
        var variance = widths.Select(width => Math.Abs(width - average)).Average();
        var variancePercent = average == 0 ? 0 : variance / average * 100;
        return (minWidth, maxWidth, variancePercent);
    }

    private static PortableWidthCheckResult CalculateOverallResult(IReadOnlyList<PortableWidthCheckItem> items)
    {
        if (items.Any(item => item.Result == PortableWidthCheckResult.Fail))
        {
            return PortableWidthCheckResult.Fail;
        }

        if (items.Any(item => item.Result == PortableWidthCheckResult.Warning))
        {
            return PortableWidthCheckResult.Warning;
        }

        return PortableWidthCheckResult.Pass;
    }

    private static string GenerateSummary(PortableCharWidthReport report)
    {
        var passCount = report.Items.Count(item => item.Result == PortableWidthCheckResult.Pass);
        var warnCount = report.Items.Count(item => item.Result == PortableWidthCheckResult.Warning);
        var failCount = report.Items.Count(item => item.Result == PortableWidthCheckResult.Fail);

        return report.OverallResult switch
        {
            PortableWidthCheckResult.Pass => $"字符宽度一致性良好 ({passCount}项通过)",
            PortableWidthCheckResult.Warning => $"字符宽度存在轻微问题 ({passCount}项通过, {warnCount}项警告)",
            PortableWidthCheckResult.Fail => $"字符宽度不一致 ({failCount}项失败, {warnCount}项警告)",
            _ => "未知"
        };
    }
}
