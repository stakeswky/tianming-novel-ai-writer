using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using TM.Services.Framework.AI.Monitoring;

namespace Tianming.Desktop.Avalonia.ViewModels.AI;

/// <summary>
/// M4.6.5 用量统计页 ViewModel — 展示今日统计、7 天趋势、按 Model 聚合。
/// </summary>
public partial class UsageStatisticsViewModel : ObservableObject
{
    private readonly FileUsageStatisticsService _statsService;

    [ObservableProperty] private string _todayRequests = "0";
    [ObservableProperty] private string _todayTokens = "0";
    [ObservableProperty] private string _todayCost = "$0.00";
    [ObservableProperty] private string _todaySuccessRate = "N/A";

    [ObservableProperty] private string _weekRequests = "0";
    [ObservableProperty] private string _weekTokens = "0";
    [ObservableProperty] private string _weekCost = "$0.00";
    [ObservableProperty] private string _weekSuccessRate = "N/A";

    public IReadOnlyList<DailyStatsRow> DailyRows { get; private set; } = Array.Empty<DailyStatsRow>();
    public IReadOnlyList<ModelStatsRow> ModelRows { get; private set; } = Array.Empty<ModelStatsRow>();

    public UsageStatisticsViewModel(FileUsageStatisticsService statsService)
    {
        _statsService = statsService;
        Refresh();
    }

    public void Refresh()
    {
        // 今日统计
        var todaySummary = _statsService.GetSummary();
        TodayRequests = todaySummary.TotalCalls.ToString();
        TodayTokens = FormatTokens(todaySummary.TotalInputTokens + todaySummary.TotalOutputTokens);
        TodayCost = EstimateCost(todaySummary.TotalInputTokens, todaySummary.TotalOutputTokens);
        TodaySuccessRate = FormatSuccessRate(todaySummary);

        // 7 天统计
        var dailyStats = _statsService.GetDailyStatistics(7);
        int weekInputTokens = 0;
        int weekOutputTokens = 0;
        int weekTotalCalls = 0;
        int weekSuccessCalls = 0;

        var dailyRows = new List<DailyStatsRow>();
        foreach (var day in dailyStats)
        {
            var inputTokens = (int)(_statsService.GetAllRecords()
                .Where(r => r.Timestamp.Date == day.Date)
                .Sum(r => r.InputTokens));
            var outputTokens = (int)(_statsService.GetAllRecords()
                .Where(r => r.Timestamp.Date == day.Date)
                .Sum(r => r.OutputTokens));
            weekInputTokens += inputTokens;
            weekOutputTokens += outputTokens;
            weekTotalCalls += day.TotalCalls;
            weekSuccessCalls += day.SuccessCalls;

            dailyRows.Add(new DailyStatsRow
            {
                Date = day.Date.ToString("MM-dd"),
                Requests = day.TotalCalls,
                Tokens = FormatTokens(inputTokens + outputTokens),
                Cost = EstimateCost(inputTokens, outputTokens),
                SuccessRate = day.TotalCalls > 0
                    ? $"{(double)day.SuccessCalls / day.TotalCalls * 100:F0}%"
                    : "N/A"
            });
        }

        DailyRows = dailyRows.AsReadOnly();
        WeekRequests = weekTotalCalls.ToString();
        WeekTokens = FormatTokens(weekInputTokens + weekOutputTokens);
        WeekCost = EstimateCost(weekInputTokens, weekOutputTokens);
        WeekSuccessRate = weekTotalCalls > 0
            ? $"{(double)weekSuccessCalls / weekTotalCalls * 100:F0}%"
            : "N/A";

        // 按 Model 聚合
        var modelStats = _statsService.GetStatisticsByModel();
        var modelRows = new List<ModelStatsRow>();
        foreach (var kvp in modelStats.OrderByDescending(k => k.Value.TotalCalls))
        {
            var summary = kvp.Value;
            var inputTokens = summary.TotalInputTokens;
            var outputTokens = summary.TotalOutputTokens;
            modelRows.Add(new ModelStatsRow
            {
                Model = string.IsNullOrEmpty(kvp.Key) ? "(unknown)" : kvp.Key,
                Requests = summary.TotalCalls,
                Tokens = FormatTokens(inputTokens + outputTokens),
                Cost = EstimateCost(inputTokens, outputTokens),
                SuccessRate = FormatSuccessRate(summary)
            });
        }
        ModelRows = modelRows.AsReadOnly();
    }

    private static string FormatTokens(int tokens)
    {
        if (tokens >= 1_000_000)
            return $"{tokens / 1_000_000.0:F1}M";
        if (tokens >= 1_000)
            return $"{tokens / 1_000.0:F1}K";
        return tokens.ToString();
    }

    private static string EstimateCost(int inputTokens, int outputTokens)
    {
        var cost = (inputTokens * 0.001 + outputTokens * 0.003) / 1000;
        return $"${cost:F2}";
    }

    private static string FormatSuccessRate(StatisticsSummary summary)
    {
        return summary.TotalCalls > 0
            ? $"{summary.SuccessRate:F0}%"
            : "N/A";
    }
}

public class DailyStatsRow
{
    public string Date { get; set; } = string.Empty;
    public int Requests { get; set; }
    public string Tokens { get; set; } = string.Empty;
    public string Cost { get; set; } = string.Empty;
    public string SuccessRate { get; set; } = string.Empty;
}

public class ModelStatsRow
{
    public string Model { get; set; } = string.Empty;
    public int Requests { get; set; }
    public string Tokens { get; set; } = string.Empty;
    public string Cost { get; set; } = string.Empty;
    public string SuccessRate { get; set; } = string.Empty;
}
