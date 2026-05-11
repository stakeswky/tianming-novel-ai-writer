using TM.Framework.Appearance;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableAIColorSchemeCoreTests
{
    [Fact]
    public void BuildPrompt_uses_defaults_and_dark_theme_constraints()
    {
        var request = new PortableAIColorSchemeRequest
        {
            Keywords = "  赛博雨夜  ",
            ColorHarmony = "互补色",
            ThemeType = "暗色主题",
            Emotion = "无",
            Scene = "写作创作"
        };

        var prompt = PortableAIColorSchemeCore.BuildPrompt(request);

        Assert.Contains("- 关键词/描述：赛博雨夜", prompt);
        Assert.Contains("- 色彩和谐规则：互补色", prompt);
        Assert.Contains("- 情感色彩：不限", prompt);
        Assert.Contains("BackgroundColor必须为深色", prompt);
        Assert.Contains("数组长度必须严格等于3", prompt);
    }

    [Fact]
    public void ParseSchemes_extracts_json_array_and_scores_cards()
    {
        var response = """
            这里是说明
            [
              {
                "SchemeName": "夜航",
                "PrimaryColor": "#3366CC",
                "SecondaryColor": "#CC6633",
                "AccentColor": "#66CC99",
                "BackgroundColor": "#0D1117",
                "TextColor": "#F0F0F0"
              }
            ]
            """;

        var schemes = PortableAIColorSchemeCore.ParseSchemes(
            response,
            new PortableAIColorSchemeRequest
            {
                ColorHarmony = "互补色",
                ThemeType = "暗色主题",
                Emotion = "神秘",
                Scene = "科技感"
            });

        var scheme = Assert.Single(schemes);
        Assert.Equal("夜航", scheme.SchemeName);
        Assert.Equal("#3366CC", scheme.PrimaryColor.ToHex());
        Assert.Equal("互补色", scheme.Harmony);
        Assert.Equal("暗色主题", scheme.ThemeType);
        Assert.InRange(scheme.Score, 65, 100);
    }

    [Fact]
    public void ParseSchemes_defaults_missing_name_and_gray_invalid_colors()
    {
        var schemes = PortableAIColorSchemeCore.ParseSchemes(
            """
            [
              {
                "PrimaryColor": "bad",
                "SecondaryColor": "#112233",
                "AccentColor": "#445566",
                "BackgroundColor": "#FFFFFF",
                "TextColor": "#212529"
              }
            ]
            """,
            new PortableAIColorSchemeRequest());

        var scheme = Assert.Single(schemes);
        Assert.Equal("配色方案1", scheme.SchemeName);
        Assert.Equal(new PortableRgbColor(128, 128, 128), scheme.PrimaryColor);
    }

    [Fact]
    public void ParseSchemes_returns_empty_for_missing_or_malformed_json_array()
    {
        Assert.Empty(PortableAIColorSchemeCore.ParseSchemes("no json", new PortableAIColorSchemeRequest()));
        Assert.Empty(PortableAIColorSchemeCore.ParseSchemes("[{ invalid json }]", new PortableAIColorSchemeRequest()));
    }

    [Fact]
    public void ComputeScore_rewards_accessible_contrast_and_balanced_saturation()
    {
        var strong = PortableAIColorSchemeCore.ComputeScore(
            new PortableRgbColor(51, 102, 204),
            new PortableRgbColor(255, 255, 255),
            new PortableRgbColor(33, 37, 41));
        var weak = PortableAIColorSchemeCore.ComputeScore(
            new PortableRgbColor(245, 245, 245),
            new PortableRgbColor(255, 255, 255),
            new PortableRgbColor(220, 220, 220));

        Assert.True(strong > weak);
        Assert.InRange(strong, 65, 100);
        Assert.InRange(weak, 0, 35);
    }

    [Fact]
    public void CreateThemeSnapshot_maps_scheme_to_theme_designer_colors()
    {
        var scheme = new PortableAIColorSchemeCard
        {
            SchemeName = "晨光",
            ThemeType = "浅色主题",
            PrimaryColor = new PortableRgbColor(80, 120, 200),
            SecondaryColor = new PortableRgbColor(30, 160, 180),
            BackgroundColor = new PortableRgbColor(248, 249, 250),
            TextColor = new PortableRgbColor(33, 37, 41)
        };

        var snapshot = PortableAIColorSchemeCore.CreateThemeSnapshot("AI_晨光", scheme);
        var xaml = PortableThemeDesigner.GenerateThemeXaml(snapshot);

        Assert.Equal("AI_晨光", snapshot.ThemeName);
        Assert.Equal("#F8F9FA", snapshot.TopBarBackground);
        Assert.Equal("#FFFFFF", snapshot.CenterWorkspaceBackground);
        Assert.Equal("#212529", snapshot.CenterWorkspaceText);
        Assert.Equal("#5078C8", snapshot.PrimaryButtonColor);
        Assert.Equal("#1EA0B4", snapshot.LeftBarIconColor);
        Assert.Contains("<SolidColorBrush x:Key=\"PrimaryColor\" Color=\"#5078C8\"/>", xaml);
    }
}
