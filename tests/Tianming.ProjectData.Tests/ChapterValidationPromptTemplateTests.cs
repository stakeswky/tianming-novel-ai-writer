using System.Text.Json;
using System.Text.RegularExpressions;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ChapterValidationPromptTemplateTests
{
    [Fact]
    public void BuildJsonTemplate_includes_every_rule_with_display_type_and_extended_fields()
    {
        using var doc = JsonDocument.Parse(ChapterValidationPromptTemplate.BuildJsonTemplate());
        var moduleResults = doc.RootElement.GetProperty("moduleResults").EnumerateArray().ToList();

        Assert.Equal(ValidationRules.TotalRuleCount, moduleResults.Count);
        Assert.Equal(ValidationRules.AllModuleNames, moduleResults.Select(module => module.GetProperty("moduleName").GetString()).ToArray());

        var style = moduleResults[0];
        Assert.Equal("文风模板一致性", style.GetProperty("displayName").GetString());
        Assert.Equal("文风", style.GetProperty("verificationType").GetString());
        Assert.True(style.GetProperty("extendedData").TryGetProperty("templateName", out _));
        Assert.True(style.GetProperty("extendedData").TryGetProperty("overallIdea", out _));

        var volume = moduleResults.Single(module => module.GetProperty("moduleName").GetString() == "VolumeDesignConsistency");
        Assert.Equal("分卷设计", volume.GetProperty("verificationType").GetString());
        Assert.True(volume.GetProperty("extendedData").TryGetProperty("volumeTitle", out _));
        Assert.Equal("问题简述", volume.GetProperty("problemItems")[0].GetProperty("summary").GetString());
    }

    [Fact]
    public void BuildRulesDescription_keeps_original_rule_order_and_domain_hints()
    {
        var description = ChapterValidationPromptTemplate.BuildRulesDescription();

        Assert.Contains("1. StyleConsistency（文风模板一致性）：对齐创作模板文风/类型/构思", description);
        Assert.Contains("10. VolumeDesignConsistency（分卷设计一致性）：对齐卷主题/阶段目标/主冲突/关键事件", description);
        Assert.Contains("extendedData: chapterId, oneLineStructure, pacingCurve, cast, locations", description);
    }

    [Theory]
    [InlineData("StyleConsistency", "文风")]
    [InlineData("PlotConsistency", "剧情")]
    [InlineData("VolumeDesignConsistency", "分卷设计")]
    [InlineData("UnknownRule", "通用")]
    public void GetVerificationType_matches_original_labels(string moduleName, string expected)
    {
        Assert.Equal(expected, ChapterValidationPromptTemplate.GetVerificationType(moduleName));
    }

    [Fact]
    public void BuildRulesSignature_is_stable_short_hex_over_rule_names_and_fields()
    {
        var first = ChapterValidationPromptTemplate.BuildRulesSignature();
        var second = ChapterValidationPromptTemplate.BuildRulesSignature();

        Assert.Equal(first, second);
        Assert.Matches(new Regex("^[0-9A-F]{16}$"), first);
    }
}
