using TM.Framework.Proxy;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableProxyChainAnalyzerTests
{
    [Fact]
    public void AnalyzePerformance_summarizes_history_for_each_chain()
    {
        var settings = CreateSettings();

        var performance = PortableProxyChainAnalyzer.AnalyzePerformance(settings);

        var primary = Assert.Single(performance, item => item.ChainId == "primary");
        Assert.Equal("Primary", primary.ChainName);
        Assert.Equal(3, primary.TotalUses);
        Assert.Equal(2, primary.SuccessfulUses);
        Assert.Equal(1, primary.FailedUses);
        Assert.Equal(200.0 / 3, primary.SuccessRate, precision: 6);
        Assert.Equal(350, primary.AverageTotalLatency);
        Assert.Equal(new DateTime(2026, 5, 10, 8, 0, 0), primary.FirstUsed);
        Assert.Equal(new DateTime(2026, 5, 10, 10, 0, 0), primary.LastUsed);

        var secondary = Assert.Single(performance, item => item.ChainId == "secondary");
        Assert.Equal(100, secondary.SuccessRate);
        Assert.Equal(120, secondary.AverageTotalLatency);
    }

    [Fact]
    public void Compare_ranks_by_success_rate_and_assigns_original_grades()
    {
        var performance = PortableProxyChainAnalyzer.AnalyzePerformance(CreateSettings());

        var comparison = PortableProxyChainAnalyzer.Compare(performance, new DateTime(2026, 5, 10, 12, 0, 0));

        Assert.Equal(new DateTime(2026, 5, 10, 12, 0, 0), comparison.ComparisonTime);
        Assert.Equal("secondary", comparison.BestChainId);
        Assert.Equal("Secondary", comparison.BestChainName);
        Assert.Equal("最佳代理链: Secondary (评级: A)", comparison.Summary);
        Assert.Equal(["secondary", "primary", "unstable"], comparison.Items.Select(item => item.ChainId));
        Assert.Equal([1, 2, 3], comparison.Items.Select(item => item.Rank));
        Assert.Equal(["A", "C", "D"], comparison.Items.Select(item => item.PerformanceGrade));
    }

    [Fact]
    public void Optimize_returns_original_success_and_latency_suggestions()
    {
        var performance = PortableProxyChainAnalyzer.AnalyzePerformance(CreateSettings());
        var primary = CreateSettings().Chains.Single(chain => chain.Id == "primary");

        var optimization = PortableProxyChainAnalyzer.Optimize(primary, performance.Single(item => item.ChainId == "primary"));

        Assert.Equal("primary", optimization.ChainId);
        Assert.Equal(PortableProxyChainOptimizationPriority.High, optimization.Priority);
        Assert.Equal(
            ["成功率较低，建议检查节点配置"],
            optimization.Suggestions);
        Assert.Equal("成功率较低，建议检查节点配置", optimization.Summary);

        var unstable = CreateSettings().Chains.Single(chain => chain.Id == "unstable");
        var unstableOptimization = PortableProxyChainAnalyzer.Optimize(
            unstable,
            performance.Single(item => item.ChainId == "unstable"));

        Assert.Equal(PortableProxyChainOptimizationPriority.Medium, unstableOptimization.Priority);
        Assert.Equal(
            ["成功率较低，建议检查节点配置", "延迟较高，建议优化节点顺序或更换节点"],
            unstableOptimization.Suggestions);
    }

    [Fact]
    public void BuildReport_includes_top_poor_comparison_and_health_score()
    {
        var settings = CreateSettings();

        var report = PortableProxyChainAnalyzer.BuildReport(settings, generatedTime: new DateTime(2026, 5, 10, 13, 0, 0));

        Assert.Equal(new DateTime(2026, 5, 10, 13, 0, 0), report.GeneratedTime);
        Assert.Equal(3, report.TotalChains);
        Assert.Equal(2, report.ActiveChains);
        Assert.Equal(["secondary", "primary", "unstable"], report.TopPerformers.Select(item => item.ChainId));
        Assert.Equal(["unstable", "primary", "secondary"], report.PoorPerformers.Select(item => item.ChainId));
        Assert.Equal("secondary", report.Comparison.BestChainId);
        Assert.Equal(23, report.HealthScore);
        Assert.Equal("代理链报告 - 生成于 2026-05-10 13:00:00", report.Summary);
    }

    private static PortableProxyChainSettings CreateSettings()
    {
        return new PortableProxyChainSettings
        {
            ActiveChainId = "primary",
            Chains =
            [
                Chain("primary", "Primary", enabled: true),
                Chain("secondary", "Secondary", enabled: true),
                Chain("unstable", "Unstable", enabled: false)
            ],
            History =
            [
                History("primary", "Primary", success: true, latency: 200, start: new DateTime(2026, 5, 10, 8, 0, 0)),
                History("primary", "Primary", success: false, latency: 700, start: new DateTime(2026, 5, 10, 9, 0, 0)),
                History("primary", "Primary", success: true, latency: 150, start: new DateTime(2026, 5, 10, 10, 0, 0)),
                History("secondary", "Secondary", success: true, latency: 120, start: new DateTime(2026, 5, 10, 8, 30, 0)),
                History("unstable", "Unstable", success: false, latency: 900, start: new DateTime(2026, 5, 10, 11, 0, 0))
            ]
        };
    }

    private static PortableProxyChainConfig Chain(string id, string name, bool enabled)
    {
        return new PortableProxyChainConfig
        {
            Id = id,
            Name = name,
            Enabled = enabled
        };
    }

    private static PortableProxyChainHistory History(
        string chainId,
        string chainName,
        bool success,
        double latency,
        DateTime start)
    {
        return new PortableProxyChainHistory
        {
            ChainId = chainId,
            ChainName = chainName,
            StartTime = start,
            EndTime = start.AddSeconds(5),
            Success = success,
            TotalNodes = 2,
            SuccessfulNodes = success ? 2 : 0,
            AverageLatency = latency
        };
    }
}
