namespace TM.Framework.Appearance;

public sealed record PortableThemeRequest(
    PortableThemeType RequestedTheme,
    int Priority,
    string Source)
{
    public DateTime RequestTime { get; init; } = DateTime.Now;
}

public sealed class PortableThemeConflictInfo
{
    public bool HasConflict { get; set; }

    public PortableThemeType SystemFollowTheme { get; set; }

    public PortableThemeType TimeBasedTheme { get; set; }

    public int SystemFollowPriority { get; set; }

    public int TimeBasedPriority { get; set; }

    public string Winner { get; set; } = string.Empty;

    public PortableThemeType ResolvedTheme { get; set; }

    public string Description =>
        $"冲突：跟随系统({SystemFollowTheme}, 优先级{SystemFollowPriority}) vs 定时切换({TimeBasedTheme}, 优先级{TimeBasedPriority}) -> {Winner}胜出";
}

public static class PortableThemeConflictResolver
{
    public static PortableThemeType ResolveConflict(
        PortableThemeRequest? systemFollowRequest,
        PortableThemeRequest? timeBasedRequest,
        PortableThemeType currentTheme)
    {
        if (systemFollowRequest is null && timeBasedRequest is null)
        {
            return currentTheme;
        }

        if (systemFollowRequest is null)
        {
            return timeBasedRequest!.RequestedTheme;
        }

        if (timeBasedRequest is null)
        {
            return systemFollowRequest.RequestedTheme;
        }

        if (systemFollowRequest.Priority > timeBasedRequest.Priority)
        {
            return systemFollowRequest.RequestedTheme;
        }

        return timeBasedRequest.RequestedTheme;
    }

    public static PortableThemeConflictInfo? DetectConflict(
        bool systemFollowEnabled,
        int systemFollowPriority,
        PortableThemeType systemFollowTheme,
        bool timeBasedEnabled,
        int timeBasedPriority,
        PortableThemeType timeBasedTheme)
    {
        if (!systemFollowEnabled || !timeBasedEnabled)
        {
            return null;
        }

        if (systemFollowTheme == timeBasedTheme)
        {
            return null;
        }

        var systemWins = systemFollowPriority >= timeBasedPriority;
        return new PortableThemeConflictInfo
        {
            HasConflict = true,
            SystemFollowTheme = systemFollowTheme,
            TimeBasedTheme = timeBasedTheme,
            SystemFollowPriority = systemFollowPriority,
            TimeBasedPriority = timeBasedPriority,
            Winner = systemWins ? "跟随系统" : "定时切换",
            ResolvedTheme = systemWins ? systemFollowTheme : timeBasedTheme
        };
    }
}
