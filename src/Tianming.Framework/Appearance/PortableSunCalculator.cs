namespace TM.Framework.Appearance;

public static class PortableSunCalculator
{
    public static (TimeSpan Sunrise, TimeSpan Sunset) CalculateSunTimes(
        DateTime date,
        double latitude,
        double longitude)
    {
        try
        {
            var year = date.Year;
            var month = date.Month;
            var day = date.Day;

            if (month <= 2)
            {
                year--;
                month += 12;
            }

            var century = year / 100;
            var correction = 2 - century + century / 4;
            var julianDay = Math.Floor(365.25 * (year + 4716))
                + Math.Floor(30.6001 * (month + 1))
                + day
                + correction
                - 1524.5;

            var t = (julianDay - 2451545.0) / 36525.0;
            var meanLongitude = (280.46646 + 36000.76983 * t + 0.0003032 * t * t) % 360;
            var meanAnomaly = (357.52911 + 35999.05029 * t - 0.0001537 * t * t) % 360;
            var center = (1.914602 - 0.004817 * t - 0.000014 * t * t) * Math.Sin(ToRadians(meanAnomaly))
                + (0.019993 - 0.000101 * t) * Math.Sin(ToRadians(2 * meanAnomaly))
                + 0.000289 * Math.Sin(ToRadians(3 * meanAnomaly));
            var trueLongitude = meanLongitude + center;
            var obliquity = 23.439 - 0.0000004 * t;
            var declination = ToDegrees(Math.Asin(Math.Sin(ToRadians(obliquity)) * Math.Sin(ToRadians(trueLongitude))));
            var hourAngle = ToDegrees(Math.Acos(
                (Math.Cos(ToRadians(90.833)) - Math.Sin(ToRadians(latitude)) * Math.Sin(ToRadians(declination)))
                / (Math.Cos(ToRadians(latitude)) * Math.Cos(ToRadians(declination)))));

            var sunriseHour = 12 - hourAngle / 15 - longitude / 15;
            var sunsetHour = 12 + hourAngle / 15 - longitude / 15;
            var sunrise = TimeSpan.FromHours(sunriseHour);
            var sunset = TimeSpan.FromHours(sunsetHour);

            if (sunrise < TimeSpan.Zero || sunrise > TimeSpan.FromHours(12))
            {
                sunrise = TimeSpan.FromHours(6);
            }

            if (sunset < TimeSpan.FromHours(12) || sunset > TimeSpan.FromHours(24))
            {
                sunset = TimeSpan.FromHours(18);
            }

            return (sunrise, sunset);
        }
        catch
        {
            return (TimeSpan.FromHours(6), TimeSpan.FromHours(18));
        }
    }

    private static double ToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    private static double ToDegrees(double radians)
    {
        return radians * 180.0 / Math.PI;
    }
}
