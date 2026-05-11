namespace TM.Framework.Appearance;

public sealed class PortableHolidayLibrary
{
    private readonly Dictionary<int, List<PortableHolidayInfo>> _holidaysByYear = new();

    public PortableHolidayLibrary()
    {
        LoadBuiltInHolidays();
    }

    public bool IsHoliday(DateTime date)
    {
        return GetHolidayInfo(date) is not null;
    }

    public PortableHolidayInfo? GetHolidayInfo(DateTime date)
    {
        return _holidaysByYear.TryGetValue(date.Year, out var holidays)
            ? holidays.FirstOrDefault(holiday => holiday.Date.Date == date.Date)?.Clone()
            : null;
    }

    public List<PortableHolidayInfo> GetHolidaysByYear(int year)
    {
        return _holidaysByYear.TryGetValue(year, out var holidays)
            ? holidays.Select(holiday => holiday.Clone()).ToList()
            : [];
    }

    public List<PortableHolidayInfo> GetHolidaysByMonth(int year, int month)
    {
        return _holidaysByYear.TryGetValue(year, out var holidays)
            ? holidays
                .Where(holiday => holiday.Date.Month == month)
                .Select(holiday => holiday.Clone())
                .ToList()
            : [];
    }

    public List<int> GetSupportedYears()
    {
        return _holidaysByYear.Keys.OrderBy(year => year).ToList();
    }

    private void LoadBuiltInHolidays()
    {
        _holidaysByYear[2024] =
        [
            National(2024, 1, 1, "元旦"),
            National(2024, 2, 10, "春节"),
            National(2024, 2, 11, "春节"),
            National(2024, 2, 12, "春节"),
            National(2024, 2, 13, "春节"),
            National(2024, 2, 14, "春节"),
            National(2024, 2, 15, "春节"),
            National(2024, 2, 16, "春节"),
            National(2024, 2, 17, "春节"),
            National(2024, 4, 4, "清明节"),
            National(2024, 4, 5, "清明节"),
            National(2024, 4, 6, "清明节"),
            National(2024, 5, 1, "劳动节"),
            National(2024, 5, 2, "劳动节"),
            National(2024, 5, 3, "劳动节"),
            National(2024, 5, 4, "劳动节"),
            National(2024, 5, 5, "劳动节"),
            National(2024, 6, 10, "端午节"),
            National(2024, 9, 15, "中秋节"),
            National(2024, 9, 16, "中秋节"),
            National(2024, 9, 17, "中秋节"),
            National(2024, 10, 1, "国庆节"),
            National(2024, 10, 2, "国庆节"),
            National(2024, 10, 3, "国庆节"),
            National(2024, 10, 4, "国庆节"),
            National(2024, 10, 5, "国庆节"),
            National(2024, 10, 6, "国庆节"),
            National(2024, 10, 7, "国庆节")
        ];

        _holidaysByYear[2025] =
        [
            National(2025, 1, 1, "元旦"),
            National(2025, 1, 28, "春节"),
            National(2025, 1, 29, "春节"),
            National(2025, 1, 30, "春节"),
            National(2025, 1, 31, "春节"),
            National(2025, 2, 1, "春节"),
            National(2025, 2, 2, "春节"),
            National(2025, 2, 3, "春节"),
            National(2025, 4, 4, "清明节"),
            National(2025, 4, 5, "清明节"),
            National(2025, 4, 6, "清明节"),
            National(2025, 5, 1, "劳动节"),
            National(2025, 5, 2, "劳动节"),
            National(2025, 5, 3, "劳动节"),
            National(2025, 5, 31, "端午节"),
            National(2025, 6, 1, "端午节"),
            National(2025, 6, 2, "端午节"),
            National(2025, 10, 1, "国庆节"),
            National(2025, 10, 2, "国庆节"),
            National(2025, 10, 3, "国庆节"),
            National(2025, 10, 4, "国庆节"),
            National(2025, 10, 5, "国庆节"),
            National(2025, 10, 6, "中秋节"),
            National(2025, 10, 7, "国庆节"),
            National(2025, 10, 8, "国庆节")
        ];
    }

    private static PortableHolidayInfo National(int year, int month, int day, string name)
    {
        return new PortableHolidayInfo
        {
            Date = new DateTime(year, month, day),
            Name = name,
            Type = PortableHolidayType.National,
            IsWorkday = false
        };
    }
}

public sealed class PortableHolidayInfo
{
    public DateTime Date { get; set; }

    public string Name { get; set; } = string.Empty;

    public PortableHolidayType Type { get; set; }

    public bool IsWorkday { get; set; }

    public string DisplayText => $"{Date:yyyy-MM-dd} {Name}";

    public PortableHolidayInfo Clone()
    {
        return new PortableHolidayInfo
        {
            Date = Date,
            Name = Name,
            Type = Type,
            IsWorkday = IsWorkday
        };
    }
}

public enum PortableHolidayType
{
    National,
    Custom
}
