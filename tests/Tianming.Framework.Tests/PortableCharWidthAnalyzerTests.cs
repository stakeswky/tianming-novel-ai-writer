using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableCharWidthAnalyzerTests
{
    [Fact]
    public void AnalyzeFont_returns_pass_report_when_all_groups_have_consistent_widths()
    {
        var analyzer = new PortableCharWidthAnalyzer((_, _, characters) =>
            characters.ToDictionary(ch => ch, ch => ch >= 0x4e00 && ch <= 0x9fff ? 20d : 10d));

        var report = analyzer.AnalyzeFont("Measured Mono", 12);

        Assert.Equal("Measured Mono", report.FontName);
        Assert.Equal(12, report.FontSize);
        Assert.Equal(4, report.Items.Count);
        Assert.All(report.Items, item => Assert.Equal(PortableWidthCheckResult.Pass, item.Result));
        Assert.Equal(PortableWidthCheckResult.Pass, report.OverallResult);
        Assert.Equal("字符宽度一致性良好 (4项通过)", report.Summary);
    }

    [Fact]
    public void AnalyzeFont_applies_original_thresholds_for_warning_and_fail_results()
    {
        var analyzer = new PortableCharWidthAnalyzer((_, _, characters) =>
        {
            if (characters == "0123456789")
            {
                return characters.ToDictionary(ch => ch, ch => ch == '0' ? 11d : 10d);
            }

            if (characters.StartsWith("abcdefghijklmnopqrstuvwxyz", StringComparison.Ordinal))
            {
                return characters.ToDictionary(ch => ch, ch => ch is >= 'a' and <= 'z' ? 2d : 20d);
            }

            if (characters == "[]{}()<>")
            {
                return characters.ToDictionary(ch => ch, ch => ch is '[' or ']' ? 8d : 12d);
            }

            if (characters == "中文汉字测试")
            {
                return new Dictionary<char, double>
                {
                    ['中'] = 5,
                    ['文'] = 15,
                    ['汉'] = 5,
                    ['字'] = 15,
                    ['测'] = 5,
                    ['试'] = 15
                };
            }

            return characters.ToDictionary(ch => ch, _ => 10d);
        });

        var report = analyzer.AnalyzeFont("Variable Font", 14);

        Assert.Equal(PortableWidthCheckResult.Fail, report.OverallResult);
        Assert.Equal(PortableWidthCheckResult.Warning, report.Items.Single(item => item.Category == "数字 (0-9)").Result);
        Assert.Equal(PortableWidthCheckResult.Fail, report.Items.Single(item => item.Category == "字母 (a-z, A-Z)").Result);
        Assert.Equal(PortableWidthCheckResult.Warning, report.Items.Single(item => item.Category == "括号符号 ([]{}()<>)").Result);
        Assert.Equal(PortableWidthCheckResult.Fail, report.Items.Single(item => item.Category == "CJK字符").Result);
        Assert.Equal("字符宽度不一致 (2项失败, 2项警告)", report.Summary);
    }

    [Fact]
    public void AnalyzeFont_marks_cjk_warning_when_font_does_not_support_cjk()
    {
        var analyzer = new PortableCharWidthAnalyzer((_, _, characters) =>
            characters.ToDictionary(ch => ch, ch => ch >= 0x4e00 && ch <= 0x9fff ? 0d : 10d));

        var report = analyzer.AnalyzeFont("No CJK Font", 12);
        var cjk = report.Items.Single(item => item.Category == "CJK字符");

        Assert.Equal(PortableWidthCheckResult.Warning, cjk.Result);
        Assert.Equal("字体不支持CJK字符", cjk.Details);
        Assert.Equal(PortableWidthCheckResult.Warning, report.OverallResult);
        Assert.Equal("字符宽度存在轻微问题 (3项通过, 1项警告)", report.Summary);
    }

    [Fact]
    public void AnalyzeFont_returns_fail_summary_when_measurement_throws()
    {
        var analyzer = new PortableCharWidthAnalyzer((_, _, _) => throw new InvalidOperationException("measure unavailable"));

        var report = analyzer.AnalyzeFont("Broken Font", 12);

        Assert.Equal(PortableWidthCheckResult.Fail, report.OverallResult);
        Assert.Equal("分析失败: measure unavailable", report.Summary);
        Assert.Empty(report.Items);
    }
}
