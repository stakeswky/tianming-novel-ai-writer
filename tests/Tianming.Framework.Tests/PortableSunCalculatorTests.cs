using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableSunCalculatorTests
{
    [Fact]
    public void CalculateSunTimes_matches_original_algorithm_for_summer_solstice()
    {
        var (sunrise, sunset) = PortableSunCalculator.CalculateSunTimes(
            new DateTime(2026, 6, 21),
            latitude: 51.5,
            longitude: 0);

        Assert.InRange(sunrise, new TimeSpan(3, 40, 0), new TimeSpan(3, 42, 0));
        Assert.InRange(sunset, new TimeSpan(20, 18, 0), new TimeSpan(20, 20, 0));
    }

    [Fact]
    public void CalculateSunTimes_matches_original_algorithm_for_winter_solstice()
    {
        var (sunrise, sunset) = PortableSunCalculator.CalculateSunTimes(
            new DateTime(2026, 12, 21),
            latitude: 51.5,
            longitude: 0);

        Assert.InRange(sunrise, new TimeSpan(8, 5, 0), new TimeSpan(8, 6, 0));
        Assert.InRange(sunset, new TimeSpan(15, 54, 0), new TimeSpan(15, 55, 0));
    }

    [Fact]
    public void CalculateSunTimes_uses_original_six_to_eighteen_fallback_for_invalid_coordinates()
    {
        var (sunrise, sunset) = PortableSunCalculator.CalculateSunTimes(
            new DateTime(2026, 6, 21),
            latitude: double.NaN,
            longitude: double.NaN);

        Assert.Equal(TimeSpan.FromHours(6), sunrise);
        Assert.Equal(TimeSpan.FromHours(18), sunset);
    }
}
