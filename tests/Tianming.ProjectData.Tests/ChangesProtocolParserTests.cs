using System.Linq;
using TM.Services.Modules.ProjectData.Implementations;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ChangesProtocolParserTests
{
    [Fact]
    public void ValidateChangesProtocol_accepts_separator_json_and_strips_chapter_content()
    {
        var parser = new ChangesProtocolParser();

        var result = parser.ValidateChangesProtocol("正文第一段\n---CHANGES---\n" + CompleteChangesJson());

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Equal("正文第一段", result.ContentWithoutChanges);
        Assert.NotNull(result.Changes);
        Assert.Single(result.Changes!.CharacterStateChanges);
        Assert.Equal("C7M3VT2K9P4NA", result.Changes.CharacterStateChanges[0].CharacterId);
        Assert.Equal("第三天黄昏", result.Changes.TimeProgression!.TimePeriod);
    }

    [Fact]
    public void ValidateChangesProtocol_rejects_markdown_changes_sections()
    {
        var parser = new ChangesProtocolParser();

        var result = parser.ValidateChangesProtocol("正文\n### CHANGES\n- 角色变化：林衡破境");

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("CHANGES只允许JSON格式"));
    }

    [Fact]
    public void ValidateChangesProtocol_requires_all_nine_top_level_fields()
    {
        var parser = new ChangesProtocolParser();

        var result = parser.ValidateChangesProtocol("正文\n---CHANGES---\n" + MissingItemTransfersJson());

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("CHANGES缺失必需字段: ItemTransfers"));
    }

    [Fact]
    public void ValidateChangesProtocol_rejects_entity_names_where_short_ids_are_required()
    {
        var parser = new ChangesProtocolParser();
        var namedCharacter = CompleteChangesJson().Replace("C7M3VT2K9P4NA", "林衡");

        var result = parser.ValidateChangesProtocol("正文\n---CHANGES---\n" + namedCharacter);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, error => error.Contains("CharacterStateChanges[].CharacterId"));
        Assert.Contains(result.Errors, error => error.Contains("收到非法值'林衡'"));
    }

    [Fact]
    public void ValidateChangesProtocol_repairs_chinese_field_names_and_full_width_punctuation()
    {
        var parser = new ChangesProtocolParser();

        var result = parser.ValidateChangesProtocol("""
        正文
        ---CHANGES---
        {
          '角色状态变化'：[],
          '冲突进度'：[],
          '伏笔动作'：[],
          '新增剧情'：[],
          '地点状态变化'：[],
          '势力状态变化'：[],
          '时间推进'：{ '时间段'：'第三天黄昏', '经过时间'：'半日', '关键时间事件'：'抵达寒潭' },
          '角色移动'：[],
          '物品流转'：[],
        }
        """);

        Assert.True(result.Success, string.Join("; ", result.Errors));
        Assert.Equal("第三天黄昏", result.Changes!.TimeProgression!.TimePeriod);
    }

    private static string CompleteChangesJson()
    {
        return """
        {
          "CharacterStateChanges": [
            {
              "CharacterId": "C7M3VT2K9P4NA",
              "NewLevel": "筑基",
              "NewAbilities": ["御风"],
              "LostAbilities": [],
              "RelationshipChanges": {},
              "NewMentalState": "坚定",
              "KeyEvent": "通过试炼",
              "Importance": "important"
            }
          ],
          "ConflictProgress": [
            { "ConflictId": "C7M3VT2K9P4QA", "NewStatus": "推进", "Event": "试炼开始", "Importance": "normal" }
          ],
          "ForeshadowingActions": [
            { "ForeshadowId": "F7M3VT2K9P4RA", "Action": "setup" }
          ],
          "NewPlotPoints": [
            { "Keywords": ["寒潭"], "Context": "林衡抵达寒潭", "InvolvedCharacters": ["C7M3VT2K9P4NA"], "Importance": "normal", "Storyline": "main" }
          ],
          "LocationStateChanges": [
            { "LocationId": "L7M3VT2K9P4SA", "NewStatus": "开启", "Event": "寒潭显现", "Importance": "normal" }
          ],
          "FactionStateChanges": [
            { "FactionId": "F7M3VT2K9P4TA", "NewStatus": "戒备", "Event": "弟子入山", "Importance": "normal" }
          ],
          "TimeProgression": { "TimePeriod": "第三天黄昏", "ElapsedTime": "半日", "KeyTimeEvent": "抵达寒潭", "Importance": "normal" },
          "CharacterMovements": [
            { "CharacterId": "C7M3VT2K9P4NA", "FromLocation": "L7M3VT2K9P4UA", "ToLocation": "L7M3VT2K9P4SA", "Importance": "normal" }
          ],
          "ItemTransfers": [
            { "ItemId": "I7M3VT2K9P4VA", "ItemName": "玉佩", "FromHolder": "C7M3VT2K9P4NA", "ToHolder": "C7M3VT2K9P4WA", "NewStatus": "active", "Event": "交付", "Importance": "normal" }
          ]
        }
        """;
    }

    private static string MissingItemTransfersJson()
    {
        return """
        {
          "CharacterStateChanges": [],
          "ConflictProgress": [],
          "ForeshadowingActions": [],
          "NewPlotPoints": [],
          "LocationStateChanges": [],
          "FactionStateChanges": [],
          "TimeProgression": { "TimePeriod": "第三天黄昏", "ElapsedTime": "半日", "KeyTimeEvent": "抵达寒潭", "Importance": "normal" },
          "CharacterMovements": []
        }
        """;
    }
}
