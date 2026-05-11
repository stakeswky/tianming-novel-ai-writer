using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableCodeSampleProviderTests
{
    [Fact]
    public void GetSupportedLanguages_returns_original_initialized_sample_keys_only()
    {
        var provider = new PortableCodeSampleProvider();

        var languages = provider.GetSupportedLanguages();

        Assert.Equal(
            [
                PortableCodeLanguage.CSharp,
                PortableCodeLanguage.Python,
                PortableCodeLanguage.JavaScript,
                PortableCodeLanguage.TypeScript,
                PortableCodeLanguage.JSON,
                PortableCodeLanguage.Markdown
            ],
            languages);
    }

    [Fact]
    public void GetSample_returns_original_csharp_modern_syntax_sample()
    {
        var provider = new PortableCodeSampleProvider();

        var sample = provider.GetSample(PortableCodeLanguage.CSharp);

        Assert.Equal(PortableCodeLanguage.CSharp, sample.Language);
        Assert.Equal("C#", sample.DisplayName);
        Assert.Equal("C# 现代语法特性演示", sample.Description);
        Assert.Contains("public record Person(string Name, int Age);", sample.Code);
        Assert.Contains("// != == >= <= => -> ?? ??= ||= &&", sample.Code);
    }

    [Fact]
    public void GetSample_preserves_python_javascript_json_and_markdown_examples()
    {
        var provider = new PortableCodeSampleProvider();

        Assert.Contains("def check_length(text: str) -> bool:", provider.GetSample(PortableCodeLanguage.Python).Code);
        Assert.Contains("const userName = user?.profile?.name ?? 'Anonymous';", provider.GetSample(PortableCodeLanguage.JavaScript).Code);
        Assert.Contains(""" "enableLigatures": true """.Trim(), provider.GetSample(PortableCodeLanguage.JSON).Code);
        Assert.Contains("- [x] 已完成任务", provider.GetSample(PortableCodeLanguage.Markdown).Code);
    }

    [Fact]
    public void GetSample_falls_back_to_csharp_for_enum_values_without_original_sample()
    {
        var provider = new PortableCodeSampleProvider();

        var sample = provider.GetSample(PortableCodeLanguage.Java);

        Assert.Equal(PortableCodeLanguage.CSharp, sample.Language);
        Assert.Equal("C#", sample.DisplayName);
    }
}
