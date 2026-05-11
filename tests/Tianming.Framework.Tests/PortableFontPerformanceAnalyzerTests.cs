using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableFontPerformanceAnalyzerTests
{
    [Fact]
    public void AnalyzePerformance_records_original_text_cases_and_aggregates_timings()
    {
        var analyzer = new PortableFontPerformanceAnalyzer((_, _, text, iterations) =>
        {
            Assert.Equal(25, iterations);
            return text switch
            {
                "Hello World!" => 0.005,
                "The quick brown fox jumps over the lazy dog. 0123456789" => 0.010,
                "public class Example { private int _value = 42; }" => 0.015,
                "中文测试文本字符渲染性能" => 0.020,
                _ => throw new InvalidOperationException("unexpected text")
            };
        });

        var report = analyzer.AnalyzePerformance("Menlo", 13, iterations: 25);

        Assert.Equal("Menlo", report.FontName);
        Assert.Equal(13, report.FontSize);
        Assert.Equal(25, report.TestIterations);
        Assert.Equal(0.0125, report.AverageRenderTime, 4);
        Assert.Equal(0.005, report.MinRenderTime);
        Assert.Equal(0.020, report.MaxRenderTime);
        Assert.Equal(PortablePerformanceRating.Good, report.Rating);
        Assert.Equal("渲染性能良好，平均耗时 0.013ms", report.Summary);
        Assert.Equal(["短文本(12字符)", "中长文本(60字符)", "代码片段(50字符)", "CJK字符(12字符)"], report.DetailedTimings.Keys);
    }

    [Theory]
    [InlineData(0.009, PortablePerformanceRating.Excellent, "渲染性能优秀，平均耗时 0.009ms")]
    [InlineData(0.019, PortablePerformanceRating.Good, "渲染性能良好，平均耗时 0.019ms")]
    [InlineData(0.049, PortablePerformanceRating.Fair, "渲染性能一般，平均耗时 0.049ms")]
    [InlineData(0.050, PortablePerformanceRating.Poor, "渲染性能较差，平均耗时 0.050ms")]
    public void AnalyzePerformance_uses_original_rating_thresholds(
        double timing,
        PortablePerformanceRating expectedRating,
        string expectedSummary)
    {
        var analyzer = new PortableFontPerformanceAnalyzer((_, _, _, _) => timing);

        var report = analyzer.AnalyzePerformance("Any Font", 12);

        Assert.Equal(expectedRating, report.Rating);
        Assert.Equal(expectedSummary, report.Summary);
    }

    [Fact]
    public void AnalyzePerformance_returns_poor_report_when_measurement_fails()
    {
        var analyzer = new PortableFontPerformanceAnalyzer((_, _, _, _) => throw new InvalidOperationException("render unavailable"));

        var report = analyzer.AnalyzePerformance("Broken Font", 12);

        Assert.Equal(PortablePerformanceRating.Poor, report.Rating);
        Assert.Equal("测试失败: render unavailable", report.Summary);
        Assert.Empty(report.DetailedTimings);
    }
}
