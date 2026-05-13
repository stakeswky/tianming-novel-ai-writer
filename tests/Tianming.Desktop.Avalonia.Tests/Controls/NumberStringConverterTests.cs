using System.Globalization;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class NumberStringConverterTests
{
    [Fact]
    public void Convert_int_to_string_roundtrips()
    {
        var c = NumberStringConverter.Instance;
        Assert.Equal("42", c.Convert(42, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Convert_zero_renders_as_empty_string()
    {
        // 0 默认值显示为空串，避免新建项目时大量 "0" 显眼
        var c = NumberStringConverter.Instance;
        Assert.Equal(string.Empty, c.Convert(0, typeof(string), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_empty_or_whitespace_returns_zero()
    {
        var c = NumberStringConverter.Instance;
        Assert.Equal(0, c.ConvertBack(string.Empty, typeof(int), null, CultureInfo.InvariantCulture));
        Assert.Equal(0, c.ConvertBack("   ",       typeof(int), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_invalid_string_returns_zero_not_throw()
    {
        var c = NumberStringConverter.Instance;
        // 不抛 FormatException——非法输入回退到 0；UI 用户体验比抛异常好
        Assert.Equal(0, c.ConvertBack("abc",  typeof(int), null, CultureInfo.InvariantCulture));
        Assert.Equal(0, c.ConvertBack("12.5", typeof(int), null, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ConvertBack_valid_int_string_parses()
    {
        var c = NumberStringConverter.Instance;
        Assert.Equal(123, c.ConvertBack("123", typeof(int), null, CultureInfo.InvariantCulture));
    }
}
