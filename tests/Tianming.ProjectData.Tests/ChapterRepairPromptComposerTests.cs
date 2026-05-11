using TM.Services.Modules.ProjectData.Implementations;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ChapterRepairPromptComposerTests
{
    [Fact]
    public void Compose_strips_changes_protocol_truncates_original_and_lists_hints()
    {
        var longBody = new string('甲', 8_050) + "TAIL_AFTER_LIMIT";
        var rawContent = longBody + "\n---CHANGES---\n" + EmptyChangesJson();

        var directive = new ChapterRepairPromptComposer().Compose(
            rawContent,
            ["修复人物动机", "修复地点衔接"]);

        Assert.StartsWith("<repair_directive>", directive);
        Assert.Contains("本次任务是修复已有章节，不是全新创作。", directive);
        Assert.Contains("<章节原文>", directive);
        Assert.Contains("（章节原文过长，已截断）", directive);
        Assert.DoesNotContain("---CHANGES---", directive);
        Assert.DoesNotContain("TAIL_AFTER_LIMIT", directive);
        Assert.Contains("需修复的具体问题：", directive);
        Assert.Contains("1. 修复人物动机", directive);
        Assert.Contains("2. 修复地点衔接", directive);
        Assert.EndsWith("</repair_directive>\n", directive);
    }

    [Fact]
    public void Compose_keeps_original_raw_content_when_protocol_is_not_valid()
    {
        var rawContent = "正文\n---CHANGES---\n{bad json}";

        var directive = new ChapterRepairPromptComposer().Compose(rawContent, []);

        Assert.Contains("正文", directive);
        Assert.Contains("---CHANGES---", directive);
        Assert.Contains("{bad json}", directive);
    }

    private static string EmptyChangesJson() =>
        """
        {
          "CharacterStateChanges": [],
          "ConflictProgress": [],
          "ForeshadowingActions": [],
          "NewPlotPoints": [],
          "LocationStateChanges": [],
          "FactionStateChanges": [],
          "TimeProgression": null,
          "CharacterMovements": [],
          "ItemTransfers": []
        }
        """;
}
