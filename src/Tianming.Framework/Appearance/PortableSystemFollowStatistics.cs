namespace TM.Framework.Appearance;

public sealed class PortableSystemFollowStatisticsAnalyzer
{
    private readonly List<PortableSystemFollowSwitchRecord> _switchHistory = [];

    public void AddSwitchRecord(
        PortableThemeType fromTheme,
        PortableThemeType toTheme,
        TimeSpan duration)
    {
        AddSwitchRecord(fromTheme, toTheme, duration, DateTime.Now, DateTime.Now);
    }

    public void AddSwitchRecord(
        PortableThemeType fromTheme,
        PortableThemeType toTheme,
        TimeSpan duration,
        DateTime timestamp)
    {
        AddSwitchRecord(fromTheme, toTheme, duration, timestamp, DateTime.Now);
    }

    public void AddSwitchRecord(
        PortableThemeType fromTheme,
        PortableThemeType toTheme,
        TimeSpan duration,
        DateTime timestamp,
        DateTime retentionNow)
    {
        _switchHistory.Add(new PortableSystemFollowSwitchRecord
        {
            Timestamp = timestamp,
            FromTheme = fromTheme,
            ToTheme = toTheme,
            Duration = duration
        });

        var cutoffDate = retentionNow.AddDays(-30);
        _switchHistory.RemoveAll(record => record.Timestamp < cutoffDate);
    }

    public int GetDailySwitchCount(DateTime now)
    {
        return _switchHistory.Count(record => record.Timestamp.Date == now.Date);
    }

    public int GetWeeklySwitchCount(DateTime now)
    {
        var startOfWeek = now.Date.AddDays(-(int)now.DayOfWeek);
        return _switchHistory.Count(record => record.Timestamp >= startOfWeek);
    }

    public int GetMonthlySwitchCount(DateTime now)
    {
        var startOfMonth = new DateTime(now.Year, now.Month, 1);
        return _switchHistory.Count(record => record.Timestamp >= startOfMonth);
    }

    public Dictionary<PortableThemeType, double> GetThemeUsagePercentage(DateTime now)
    {
        var sevenDaysAgo = now.AddDays(-7);
        var recentRecords = _switchHistory
            .Where(record => record.Timestamp >= sevenDaysAgo)
            .ToList();

        if (recentRecords.Count == 0)
        {
            return [];
        }

        return recentRecords
            .GroupBy(record => record.ToTheme)
            .ToDictionary(
                group => group.Key,
                group => (double)group.Count() / recentRecords.Count * 100);
    }

    public TimeSpan GetAverageSwitchDuration()
    {
        if (_switchHistory.Count == 0)
        {
            return TimeSpan.Zero;
        }

        var totalMilliseconds = _switchHistory.Sum(record => record.Duration.TotalMilliseconds);
        return TimeSpan.FromMilliseconds(totalMilliseconds / _switchHistory.Count);
    }

    public TimeSpan GetLastSwitchDuration()
    {
        return _switchHistory.LastOrDefault()?.Duration ?? TimeSpan.Zero;
    }

    public Dictionary<int, int> GetHourlySwitchCount(DateTime now)
    {
        var oneDayAgo = now.AddHours(-24);
        var hourlyCounts = Enumerable.Range(0, 24).ToDictionary(hour => hour, _ => 0);

        foreach (var record in _switchHistory.Where(record => record.Timestamp >= oneDayAgo))
        {
            hourlyCounts[record.Timestamp.Hour]++;
        }

        return hourlyCounts;
    }

    public PortableThemeType GetMostUsedTheme()
    {
        if (_switchHistory.Count == 0)
        {
            return PortableThemeType.Light;
        }

        return _switchHistory
            .GroupBy(record => record.ToTheme)
            .OrderByDescending(group => group.Count())
            .First()
            .Key;
    }

    public string GetPeakSwitchPeriod(DateTime now)
    {
        var hourlyCounts = GetHourlySwitchCount(now);
        if (hourlyCounts.Values.Sum() == 0)
        {
            return "暂无数据";
        }

        var peakHour = hourlyCounts.OrderByDescending(pair => pair.Value).First().Key;
        return $"{peakHour:D2}:00 - {(peakHour + 1) % 24:D2}:00";
    }

    public string GetPerformanceRating()
    {
        var averageMilliseconds = GetAverageSwitchDuration().TotalMilliseconds;

        if (averageMilliseconds < 100)
        {
            return "优秀";
        }

        if (averageMilliseconds < 300)
        {
            return "良好";
        }

        return averageMilliseconds < 500 ? "一般" : "需优化";
    }

    public void ClearStatistics()
    {
        _switchHistory.Clear();
    }

    public List<PortableSystemFollowSwitchRecord> GetAllRecords()
    {
        return _switchHistory
            .Select(record => record.Clone())
            .ToList();
    }
}

public sealed class PortableSystemFollowSwitchRecord
{
    public DateTime Timestamp { get; set; }

    public PortableThemeType FromTheme { get; set; }

    public PortableThemeType ToTheme { get; set; }

    public TimeSpan Duration { get; set; }

    public PortableSystemFollowSwitchRecord Clone()
    {
        return new PortableSystemFollowSwitchRecord
        {
            Timestamp = Timestamp,
            FromTheme = FromTheme,
            ToTheme = ToTheme,
            Duration = Duration
        };
    }
}
