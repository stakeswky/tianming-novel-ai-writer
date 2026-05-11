using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableHolidayLibraryTests
{
    [Theory]
    [InlineData(2024, 1, 1, "元旦")]
    [InlineData(2024, 2, 10, "春节")]
    [InlineData(2024, 10, 7, "国庆节")]
    [InlineData(2025, 1, 29, "春节")]
    [InlineData(2025, 5, 31, "端午节")]
    [InlineData(2025, 10, 6, "中秋节")]
    public void GetHolidayInfo_returns_original_built_in_holidays(int year, int month, int day, string name)
    {
        var library = new PortableHolidayLibrary();
        var holiday = library.GetHolidayInfo(new DateTime(year, month, day));

        Assert.NotNull(holiday);
        Assert.Equal(name, holiday.Name);
        Assert.Equal(PortableHolidayType.National, holiday.Type);
        Assert.False(holiday.IsWorkday);
    }

    [Fact]
    public void IsHoliday_returns_false_for_unsupported_or_normal_dates()
    {
        var library = new PortableHolidayLibrary();

        Assert.False(library.IsHoliday(new DateTime(2025, 3, 1)));
        Assert.False(library.IsHoliday(new DateTime(2031, 1, 1)));
    }

    [Fact]
    public void GetHolidaysByYear_returns_defensive_copy_sorted_by_insertion()
    {
        var library = new PortableHolidayLibrary();

        var holidays = library.GetHolidaysByYear(2025);
        holidays[0].Name = "mutated";

        Assert.Equal(25, holidays.Count);
        Assert.Equal("元旦", library.GetHolidaysByYear(2025)[0].Name);
    }

    [Fact]
    public void GetHolidaysByMonth_filters_by_year_and_month()
    {
        var library = new PortableHolidayLibrary();
        var october = library.GetHolidaysByMonth(2025, 10);

        Assert.Equal(8, october.Count);
        Assert.Contains(october, holiday => holiday.Name == "中秋节" && holiday.Date == new DateTime(2025, 10, 6));
        Assert.All(october, holiday => Assert.Equal(10, holiday.Date.Month));
    }

    [Fact]
    public void GetSupportedYears_returns_sorted_years()
    {
        var library = new PortableHolidayLibrary();

        Assert.Equal([2024, 2025], library.GetSupportedYears());
    }

    [Fact]
    public void Holiday_display_text_matches_original_format()
    {
        var holiday = new PortableHolidayInfo
        {
            Date = new DateTime(2025, 10, 6),
            Name = "中秋节",
            Type = PortableHolidayType.National
        };

        Assert.Equal("2025-10-06 中秋节", holiday.DisplayText);
    }
}
