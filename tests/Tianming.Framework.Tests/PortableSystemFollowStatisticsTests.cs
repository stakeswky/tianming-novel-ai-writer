using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableSystemFollowStatisticsTests
{
    [Fact]
    public void Empty_statistics_match_original_fallbacks()
    {
        var analyzer = new PortableSystemFollowStatisticsAnalyzer();
        var now = new DateTime(2026, 5, 10, 12, 0, 0);

        Assert.Equal(0, analyzer.GetDailySwitchCount(now));
        Assert.Equal(0, analyzer.GetWeeklySwitchCount(now));
        Assert.Equal(0, analyzer.GetMonthlySwitchCount(now));
        Assert.Empty(analyzer.GetThemeUsagePercentage(now));
        Assert.Equal(TimeSpan.Zero, analyzer.GetAverageSwitchDuration());
        Assert.Equal(TimeSpan.Zero, analyzer.GetLastSwitchDuration());
        Assert.Equal(PortableThemeType.Light, analyzer.GetMostUsedTheme());
        Assert.Equal("暂无数据", analyzer.GetPeakSwitchPeriod(now));
        Assert.Equal("优秀", analyzer.GetPerformanceRating());
        Assert.Empty(analyzer.GetAllRecords());
    }

    [Fact]
    public void AddSwitchRecord_records_counts_and_durations()
    {
        var analyzer = new PortableSystemFollowStatisticsAnalyzer();
        var now = new DateTime(2026, 5, 10, 12, 0, 0);

        analyzer.AddSwitchRecord(PortableThemeType.Light, PortableThemeType.Dark, TimeSpan.FromMilliseconds(80), now.AddHours(-1));
        analyzer.AddSwitchRecord(PortableThemeType.Dark, PortableThemeType.Green, TimeSpan.FromMilliseconds(220), now);
        analyzer.AddSwitchRecord(PortableThemeType.Green, PortableThemeType.Dark, TimeSpan.FromMilliseconds(500), now.AddDays(-3));

        Assert.Equal(2, analyzer.GetDailySwitchCount(now));
        Assert.Equal(2, analyzer.GetWeeklySwitchCount(now));
        Assert.Equal(3, analyzer.GetMonthlySwitchCount(now));
        Assert.Equal(TimeSpan.FromMilliseconds((80 + 220 + 500) / 3.0), analyzer.GetAverageSwitchDuration());
        Assert.Equal(TimeSpan.FromMilliseconds(500), analyzer.GetLastSwitchDuration());
        Assert.Equal(PortableThemeType.Dark, analyzer.GetMostUsedTheme());
    }

    [Fact]
    public void Theme_usage_percentage_uses_recent_seven_days()
    {
        var analyzer = new PortableSystemFollowStatisticsAnalyzer();
        var now = new DateTime(2026, 5, 10, 12, 0, 0);

        analyzer.AddSwitchRecord(PortableThemeType.Light, PortableThemeType.Dark, TimeSpan.FromMilliseconds(50), now.AddDays(-1));
        analyzer.AddSwitchRecord(PortableThemeType.Dark, PortableThemeType.Dark, TimeSpan.FromMilliseconds(60), now.AddDays(-2));
        analyzer.AddSwitchRecord(PortableThemeType.Dark, PortableThemeType.Green, TimeSpan.FromMilliseconds(70), now.AddDays(-3));
        analyzer.AddSwitchRecord(PortableThemeType.Green, PortableThemeType.Light, TimeSpan.FromMilliseconds(80), now.AddDays(-8));

        var percentages = analyzer.GetThemeUsagePercentage(now);

        Assert.Equal(2, percentages.Count);
        Assert.Equal(66.667, percentages[PortableThemeType.Dark], precision: 3);
        Assert.Equal(33.333, percentages[PortableThemeType.Green], precision: 3);
    }

    [Fact]
    public void Hourly_count_and_peak_period_use_recent_twenty_four_hours()
    {
        var analyzer = new PortableSystemFollowStatisticsAnalyzer();
        var now = new DateTime(2026, 5, 10, 12, 0, 0);

        analyzer.AddSwitchRecord(PortableThemeType.Light, PortableThemeType.Dark, TimeSpan.FromMilliseconds(100), new DateTime(2026, 5, 10, 8, 10, 0));
        analyzer.AddSwitchRecord(PortableThemeType.Dark, PortableThemeType.Light, TimeSpan.FromMilliseconds(100), new DateTime(2026, 5, 10, 8, 55, 0));
        analyzer.AddSwitchRecord(PortableThemeType.Light, PortableThemeType.Green, TimeSpan.FromMilliseconds(100), new DateTime(2026, 5, 10, 9, 0, 0));
        analyzer.AddSwitchRecord(PortableThemeType.Green, PortableThemeType.Dark, TimeSpan.FromMilliseconds(100), now.AddHours(-25));

        var hourly = analyzer.GetHourlySwitchCount(now);

        Assert.Equal(24, hourly.Count);
        Assert.Equal(2, hourly[8]);
        Assert.Equal(1, hourly[9]);
        Assert.Equal(0, hourly[11]);
        Assert.Equal("08:00 - 09:00", analyzer.GetPeakSwitchPeriod(now));
    }

    [Theory]
    [InlineData(99, "优秀")]
    [InlineData(100, "良好")]
    [InlineData(299, "良好")]
    [InlineData(300, "一般")]
    [InlineData(499, "一般")]
    [InlineData(500, "需优化")]
    public void Performance_rating_matches_original_thresholds(int milliseconds, string expected)
    {
        var analyzer = new PortableSystemFollowStatisticsAnalyzer();
        analyzer.AddSwitchRecord(
            PortableThemeType.Light,
            PortableThemeType.Dark,
            TimeSpan.FromMilliseconds(milliseconds),
            new DateTime(2026, 5, 10, 12, 0, 0));

        Assert.Equal(expected, analyzer.GetPerformanceRating());
    }

    [Fact]
    public void AddSwitchRecord_prunes_records_older_than_thirty_days()
    {
        var analyzer = new PortableSystemFollowStatisticsAnalyzer();
        var now = new DateTime(2026, 5, 10, 12, 0, 0);

        analyzer.AddSwitchRecord(PortableThemeType.Light, PortableThemeType.Dark, TimeSpan.FromMilliseconds(100), now.AddDays(-31), now);
        analyzer.AddSwitchRecord(PortableThemeType.Dark, PortableThemeType.Light, TimeSpan.FromMilliseconds(100), now.AddDays(-30), now);
        analyzer.AddSwitchRecord(PortableThemeType.Light, PortableThemeType.Green, TimeSpan.FromMilliseconds(100), now, now);

        var records = analyzer.GetAllRecords();

        Assert.Equal(2, records.Count);
        Assert.DoesNotContain(records, record => record.ToTheme == PortableThemeType.Dark);
    }

    [Fact]
    public void ClearStatistics_removes_records()
    {
        var analyzer = new PortableSystemFollowStatisticsAnalyzer();
        analyzer.AddSwitchRecord(
            PortableThemeType.Light,
            PortableThemeType.Dark,
            TimeSpan.FromMilliseconds(100),
            new DateTime(2026, 5, 10, 12, 0, 0));

        analyzer.ClearStatistics();

        Assert.Empty(analyzer.GetAllRecords());
    }
}
