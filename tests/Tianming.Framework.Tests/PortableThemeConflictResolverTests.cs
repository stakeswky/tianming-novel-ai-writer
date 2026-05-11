using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableThemeConflictResolverTests
{
    [Fact]
    public void ResolveConflict_returns_current_theme_when_no_requests_exist()
    {
        var result = PortableThemeConflictResolver.ResolveConflict(
            systemFollowRequest: null,
            timeBasedRequest: null,
            currentTheme: PortableThemeType.Morandi);

        Assert.Equal(PortableThemeType.Morandi, result);
    }

    [Fact]
    public void ResolveConflict_uses_single_available_request()
    {
        var systemOnly = PortableThemeConflictResolver.ResolveConflict(
            new PortableThemeRequest(PortableThemeType.Dark, Priority: 5, Source: "system"),
            timeBasedRequest: null,
            currentTheme: PortableThemeType.Light);
        var timeOnly = PortableThemeConflictResolver.ResolveConflict(
            systemFollowRequest: null,
            new PortableThemeRequest(PortableThemeType.Green, Priority: 9, Source: "time"),
            currentTheme: PortableThemeType.Light);

        Assert.Equal(PortableThemeType.Dark, systemOnly);
        Assert.Equal(PortableThemeType.Green, timeOnly);
    }

    [Fact]
    public void ResolveConflict_uses_higher_priority_request_and_time_based_wins_ties()
    {
        var systemWins = PortableThemeConflictResolver.ResolveConflict(
            new PortableThemeRequest(PortableThemeType.Dark, Priority: 8, Source: "system"),
            new PortableThemeRequest(PortableThemeType.Light, Priority: 5, Source: "time"),
            currentTheme: PortableThemeType.Green);
        var timeWins = PortableThemeConflictResolver.ResolveConflict(
            new PortableThemeRequest(PortableThemeType.Dark, Priority: 3, Source: "system"),
            new PortableThemeRequest(PortableThemeType.Light, Priority: 7, Source: "time"),
            currentTheme: PortableThemeType.Green);
        var tie = PortableThemeConflictResolver.ResolveConflict(
            new PortableThemeRequest(PortableThemeType.Dark, Priority: 5, Source: "system"),
            new PortableThemeRequest(PortableThemeType.Light, Priority: 5, Source: "time"),
            currentTheme: PortableThemeType.Green);

        Assert.Equal(PortableThemeType.Dark, systemWins);
        Assert.Equal(PortableThemeType.Light, timeWins);
        Assert.Equal(PortableThemeType.Light, tie);
    }

    [Fact]
    public void DetectConflict_returns_null_when_either_source_disabled_or_themes_match()
    {
        Assert.Null(PortableThemeConflictResolver.DetectConflict(
            systemFollowEnabled: false,
            systemFollowPriority: 5,
            systemFollowTheme: PortableThemeType.Dark,
            timeBasedEnabled: true,
            timeBasedPriority: 6,
            timeBasedTheme: PortableThemeType.Light));

        Assert.Null(PortableThemeConflictResolver.DetectConflict(
            systemFollowEnabled: true,
            systemFollowPriority: 5,
            systemFollowTheme: PortableThemeType.Dark,
            timeBasedEnabled: true,
            timeBasedPriority: 6,
            timeBasedTheme: PortableThemeType.Dark));
    }

    [Fact]
    public void DetectConflict_reports_winner_resolved_theme_and_description()
    {
        var conflict = PortableThemeConflictResolver.DetectConflict(
            systemFollowEnabled: true,
            systemFollowPriority: 5,
            systemFollowTheme: PortableThemeType.Dark,
            timeBasedEnabled: true,
            timeBasedPriority: 9,
            timeBasedTheme: PortableThemeType.Light);

        Assert.NotNull(conflict);
        Assert.True(conflict.HasConflict);
        Assert.Equal("定时切换", conflict.Winner);
        Assert.Equal(PortableThemeType.Light, conflict.ResolvedTheme);
        Assert.Equal("冲突：跟随系统(Dark, 优先级5) vs 定时切换(Light, 优先级9) -> 定时切换胜出", conflict.Description);
    }

    [Fact]
    public void DetectConflict_preserves_original_system_follow_winner_on_priority_tie()
    {
        var conflict = PortableThemeConflictResolver.DetectConflict(
            systemFollowEnabled: true,
            systemFollowPriority: 5,
            systemFollowTheme: PortableThemeType.Dark,
            timeBasedEnabled: true,
            timeBasedPriority: 5,
            timeBasedTheme: PortableThemeType.Light);

        Assert.NotNull(conflict);
        Assert.Equal("跟随系统", conflict.Winner);
        Assert.Equal(PortableThemeType.Dark, conflict.ResolvedTheme);
    }
}
