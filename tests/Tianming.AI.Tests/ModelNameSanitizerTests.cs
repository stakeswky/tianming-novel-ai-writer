using TM.Services.Framework.AI.Core;
using Xunit;

namespace Tianming.AI.Tests;

public class ModelNameSanitizerTests
{
    [Fact]
    public void Sanitize_replaces_model_names_and_provider_leak_sentences()
    {
        var logs = new List<string>();

        var result = ModelNameSanitizer.Sanitize(
            "我是基于 OpenAI 的 GPT-4o mini。Claude 3.5 也可以参考。",
            logs.Add);

        Assert.Contains("我是「天命」，具体技术细节不便透露。", result);
        Assert.DoesNotContain("OpenAI", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GPT", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Claude", result, StringComparison.OrdinalIgnoreCase);
        Assert.Single(logs);
    }

    [Theory]
    [InlineData("我是 Kimi，准备写作。", "我是「天命」，准备写作。")]
    [InlineData("我叫Qwen，可以开始。", "我是「天命」，可以开始。")]
    [InlineData("基于 Qwen 完成推理。", "我是「天命」 完成推理。")]
    public void Sanitize_replaces_contextual_kimi_and_qwen_claims(string input, string expected)
    {
        Assert.Equal(expected, ModelNameSanitizer.Sanitize(input));
    }

    [Fact]
    public void SanitizeChunk_replaces_model_names_without_logging_or_sentence_rewrite()
    {
        var result = ModelNameSanitizer.SanitizeChunk("来自 Gemini-1.5 Flash 的片段");

        Assert.Equal("来自 天命 的片段", result);
    }

    [Fact]
    public void Sanitize_returns_empty_for_null_and_does_not_log_unchanged_text()
    {
        var logs = new List<string>();

        Assert.Equal(string.Empty, ModelNameSanitizer.Sanitize(null, logs.Add));
        Assert.Equal("普通回答", ModelNameSanitizer.Sanitize("普通回答", logs.Add));
        Assert.Empty(logs);
    }
}
