using System;
using System.Text.Json;
using TM.Services.Modules.ProjectData.Implementations;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class AIOutputParserTests
{
    [Fact]
    public void ParseAIOutput_accepts_json_wrapped_in_model_text()
    {
        var parser = new AIOutputParser();

        var result = parser.ParseAIOutput("前置说明\n" + ValidPayload() + "\n后置说明", 3);

        Assert.Equal(3, result.TargetVolumeNumber);
        Assert.Equal("第三卷", result.TargetVolumeName);
        Assert.Equal("通过", result.OverallResult);
        Assert.Equal(10, result.ModuleResults.Count);
        Assert.All(result.ModuleResults, module => Assert.Equal("通过", module.Result));
        Assert.StartsWith("D", result.Id);
    }

    [Fact]
    public void ParseAIOutput_rejects_missing_module_results()
    {
        var parser = new AIOutputParser();
        var payload = """
        {
          "Volume": { "VolumeNumber": 1 },
          "OverallResult": "失败"
        }
        """;

        var ex = Assert.Throws<InvalidOperationException>(() => parser.ParseAIOutput(payload, 1));

        Assert.Contains("moduleResults", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ParseAIOutput_rejects_missing_extended_data_fields()
    {
        var parser = new AIOutputParser();
        var payload = ValidPayload().Replace("\"styleHint\":\"节奏快\"", "\"styleHint_missing\":\"节奏快\"");

        var ex = Assert.Throws<JsonException>(() => parser.ParseAIOutput(payload, 1));

        Assert.Contains("extendedData缺少字段", ex.Message);
    }

    private static string ValidPayload()
    {
        return """
        {
          "Volume": {
            "VolumeNumber": 3,
            "VolumeName": "第三卷",
            "SampledChapterCount": 2,
            "SampledChapterIds": ["C1", "C2"]
          },
          "OverallResult": "通过",
          "DependencyModuleVersions": { "Outline": 2 },
          "ModuleResults": [
            { "ModuleName": "StyleConsistency", "Result": "通过", "ExtendedData": { "templateName":"热血", "genre":"玄幻", "overallIdea":"升级", "styleHint":"节奏快" } },
            { "ModuleName": "WorldviewConsistency", "Result": "通过", "ExtendedData": { "worldRuleName":"灵气", "hardRules":"不可复活", "powerSystem":"修炼", "specialLaws":"天罚" } },
            { "ModuleName": "CharacterConsistency", "Result": "通过", "ExtendedData": { "characterName":"林衡", "identity":"弟子", "coreTraits":"谨慎", "arcGoal":"破境" } },
            { "ModuleName": "FactionConsistency", "Result": "通过", "ExtendedData": { "factionName":"青岚宗", "factionType":"宗门", "goal":"守山", "leader":"宗主" } },
            { "ModuleName": "LocationConsistency", "Result": "通过", "ExtendedData": { "locationName":"寒潭", "locationType":"秘境", "description":"深冷", "terrain":"山谷" } },
            { "ModuleName": "PlotConsistency", "Result": "通过", "ExtendedData": { "plotName":"试炼", "storyPhase":"开端", "goal":"取火", "conflict":"争夺", "result":"胜出" } },
            { "ModuleName": "OutlineConsistency", "Result": "通过", "ExtendedData": { "oneLineOutline":"少年入山", "coreConflict":"身份", "theme":"成长", "endingState":"入门" } },
            { "ModuleName": "ChapterPlanConsistency", "Result": "通过", "ExtendedData": { "chapterTitle":"入山", "mainGoal":"拜师", "keyTurn":"被拒", "hook":"异象", "foreshadowing":"玉佩" } },
            { "ModuleName": "BlueprintConsistency", "Result": "通过", "ExtendedData": { "chapterId":"C1", "oneLineStructure":"起承转合", "pacingCurve":"快慢快", "cast":"林衡", "locations":"山门" } },
            { "ModuleName": "VolumeDesignConsistency", "Result": "通过", "ExtendedData": { "volumeTitle":"山门卷", "volumeTheme":"入世", "stageGoal":"立足", "mainConflict":"门规", "keyEvents":"试炼" } }
          ]
        }
        """;
    }
}
