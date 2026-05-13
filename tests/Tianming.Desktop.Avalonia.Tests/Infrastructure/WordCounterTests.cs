using Tianming.Desktop.Avalonia.Infrastructure;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class WordCounterTests
{
    [Fact]
    public void Empty_string_returns_zero()
    {
        Assert.Equal(0, WordCounter.Count(""));
        Assert.Equal(0, WordCounter.Count(null));
    }

    [Fact]
    public void Chinese_chars_count_each_as_one()
    {
        Assert.Equal(4, WordCounter.Count("天命之书"));
    }

    [Fact]
    public void English_words_count_as_one_each()
    {
        Assert.Equal(3, WordCounter.Count("hello world test"));
    }

    [Fact]
    public void Mixed_chinese_english_count_combined()
    {
        Assert.Equal(4, WordCounter.Count("hello 世界 test")); // 1 + 2 + 1 = 4
    }

    [Fact]
    public void Whitespace_and_punctuation_not_counted()
    {
        Assert.Equal(2, WordCounter.Count("天，命。"));
    }
}
