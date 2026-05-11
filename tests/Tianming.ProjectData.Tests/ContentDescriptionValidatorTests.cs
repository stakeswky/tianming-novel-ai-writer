using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ContentDescriptionValidatorTests
{
    [Fact]
    public void ValidateCharacterDescriptions_reports_hair_color_and_personality_contradictions()
    {
        var validator = new ContentDescriptionValidator();
        var descriptions = new Dictionary<string, CharacterCoreDescription>
        {
            ["C7M3VT2K9P4NA"] = new()
            {
                Name = "林衡",
                HairColor = "黑发",
                PersonalityTags = ["沉默寡言"]
            }
        };

        var issues = validator.ValidateCharacterDescriptions("林衡的金发在风中扬起，他今日滔滔不绝。", descriptions);

        Assert.Contains(issues, issue => issue.Contains("发色矛盾") && issue.Contains("金发"));
        Assert.Contains(issues, issue => issue.Contains("性格矛盾") && issue.Contains("滔滔不绝"));
    }

    [Fact]
    public void ValidateLocationDescriptions_reports_feature_contradictions()
    {
        var validator = new ContentDescriptionValidator();
        var descriptions = new Dictionary<string, LocationCoreDescription>
        {
            ["L7M3VT2K9P4NA"] = new()
            {
                Name = "寒潭",
                Features = ["冷漠"]
            }
        };

        var issues = validator.ValidateLocationDescriptions("寒潭边却显得热情而温暖，像迎客的厅堂。", descriptions);

        Assert.Contains(issues, issue => issue.Contains("地点 寒潭 特征矛盾") && issue.Contains("热情"));
    }
}
