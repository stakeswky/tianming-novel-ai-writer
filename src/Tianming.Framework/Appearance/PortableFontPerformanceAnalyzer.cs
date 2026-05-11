namespace TM.Framework.Appearance;

public enum PortablePerformanceRating
{
    Excellent,
    Good,
    Fair,
    Poor
}

public sealed class PortablePerformanceReport
{
    public string FontName { get; set; } = string.Empty;

    public double FontSize { get; set; }

    public double AverageRenderTime { get; set; }

    public double MinRenderTime { get; set; }

    public double MaxRenderTime { get; set; }

    public int TestIterations { get; set; }

    public PortablePerformanceRating Rating { get; set; }

    public string Summary { get; set; } = string.Empty;

    public Dictionary<string, double> DetailedTimings { get; set; } = new();

    public DateTime GeneratedAt { get; set; } = DateTime.Now;
}

public sealed class PortableFontPerformanceAnalyzer
{
    private readonly Func<string, double, string, int, double> _measureRenderTime;

    public PortableFontPerformanceAnalyzer(Func<string, double, string, int, double> measureRenderTime)
    {
        _measureRenderTime = measureRenderTime ?? throw new ArgumentNullException(nameof(measureRenderTime));
    }

    public PortablePerformanceReport AnalyzePerformance(string fontName, double fontSize, int iterations = 100)
    {
        var report = new PortablePerformanceReport
        {
            FontName = fontName,
            FontSize = fontSize,
            TestIterations = iterations
        };

        try
        {
            report.DetailedTimings["短文本(12字符)"] = _measureRenderTime(fontName, fontSize, "Hello World!", iterations);
            report.DetailedTimings["中长文本(60字符)"] = _measureRenderTime(
                fontName,
                fontSize,
                "The quick brown fox jumps over the lazy dog. 0123456789",
                iterations);
            report.DetailedTimings["代码片段(50字符)"] = _measureRenderTime(
                fontName,
                fontSize,
                "public class Example { private int _value = 42; }",
                iterations);
            report.DetailedTimings["CJK字符(12字符)"] = _measureRenderTime(
                fontName,
                fontSize,
                "中文测试文本字符渲染性能",
                iterations);

            var timings = report.DetailedTimings.Values.ToList();
            report.AverageRenderTime = timings.Average();
            report.MinRenderTime = timings.Min();
            report.MaxRenderTime = timings.Max();
            report.Rating = CalculateRating(report.AverageRenderTime);
            report.Summary = GenerateSummary(report);
        }
        catch (Exception ex)
        {
            report.DetailedTimings.Clear();
            report.Rating = PortablePerformanceRating.Poor;
            report.Summary = $"测试失败: {ex.Message}";
        }

        return report;
    }

    private static PortablePerformanceRating CalculateRating(double averageTime)
    {
        if (averageTime < 0.01)
        {
            return PortablePerformanceRating.Excellent;
        }

        if (averageTime < 0.02)
        {
            return PortablePerformanceRating.Good;
        }

        if (averageTime < 0.05)
        {
            return PortablePerformanceRating.Fair;
        }

        return PortablePerformanceRating.Poor;
    }

    private static string GenerateSummary(PortablePerformanceReport report)
    {
        var ratingText = report.Rating switch
        {
            PortablePerformanceRating.Excellent => "优秀",
            PortablePerformanceRating.Good => "良好",
            PortablePerformanceRating.Fair => "一般",
            PortablePerformanceRating.Poor => "较差",
            _ => "未知"
        };

        return $"渲染性能{ratingText}，平均耗时 {report.AverageRenderTime:F3}ms";
    }
}
