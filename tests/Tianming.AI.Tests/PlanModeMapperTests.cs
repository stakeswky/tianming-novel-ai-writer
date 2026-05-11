using TM.Services.Framework.AI.SemanticKernel.Conversation.Mapping;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Models;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Parsing;
using Xunit;

namespace Tianming.AI.Tests;

public class PlanModeMapperTests
{
    [Fact]
    public void TryBuildPlanWithoutModel_returns_null_when_no_content_guide_directory_is_configured()
    {
        var mapper = new PlanModeMapper(new PlanStepParser());

        Assert.Null(mapper.TryBuildPlanWithoutModel("生成第1章"));
    }

    [Fact]
    public void TryBuildPlanWithoutModel_builds_steps_from_content_guide()
    {
        using var workspace = new TempDirectory();
        var guidesDir = System.IO.Path.Combine(workspace.Path, "guides");
        WriteContentGuide(guidesDir);
        var mapper = new PlanModeMapper(new PlanStepParser(), guidesDir);

        var message = mapper.TryBuildPlanWithoutModel("生成第2章");

        Assert.NotNull(message);
        var payload = Assert.IsType<PlanPayload>(message.Payload);
        var step = Assert.Single(payload.Steps);
        Assert.Equal(1, step.Index);
        Assert.Equal("第2章 · 入城", step.Title);
        Assert.Equal("vol1_ch2", step.ChapterId);
        Assert.Equal(2, step.ChapterNumber);
        Assert.Contains("章节ID: vol1_ch2", step.Detail);
        Assert.Contains("摘要: 林青抵达城门", step.Detail);
        Assert.Contains("场景1: 城门 | 视角: 林青", step.Detail);
        Assert.Equal("[基于打包数据直接生成计划]", payload.RawContent);
        Assert.Contains("1 个步骤", message.Summary);
        Assert.Contains("基于已有的规划", message.AnalysisRaw);
    }

    [Fact]
    public void TryBuildPlanWithoutModel_uses_continue_directive_to_select_next_chapter()
    {
        using var workspace = new TempDirectory();
        var guidesDir = System.IO.Path.Combine(workspace.Path, "guides");
        WriteContentGuide(guidesDir);
        var mapper = new PlanModeMapper(new PlanStepParser(), guidesDir);

        var message = mapper.TryBuildPlanWithoutModel("@续写:vol1_ch1");

        var payload = Assert.IsType<PlanPayload>(message!.Payload);
        var step = Assert.Single(payload.Steps);
        Assert.Equal("vol1_ch2", step.ChapterId);
        Assert.Equal("第2章 · 入城", step.Title);
    }

    [Fact]
    public void TryBuildPlanWithoutModel_reports_no_match_when_requested_chapter_is_missing()
    {
        using var workspace = new TempDirectory();
        var guidesDir = System.IO.Path.Combine(workspace.Path, "guides");
        WriteContentGuide(guidesDir);
        var mapper = new PlanModeMapper(new PlanStepParser(), guidesDir);

        var message = mapper.TryBuildPlanWithoutModel("生成第5章");

        var payload = Assert.IsType<PlanPayload>(message!.Payload);
        Assert.Empty(payload.Steps);
        Assert.Equal("⚠️ 未匹配到章节，请检查@续写/章节号是否存在，或重新打包后再试。", message.Summary);
        Assert.Contains("没有找到与这个请求匹配的章节信息", message.AnalysisRaw);
    }

    [Fact]
    public void MapFromStreamingResult_parses_steps_and_generates_plan_summary()
    {
        var mapper = new PlanModeMapper(new PlanStepParser());
        const string raw = """
        步骤一：起草第一章
        场景：入山
        2. 校验伏笔
        检查账本
        """;

        var message = mapper.MapFromStreamingResult("生成计划", raw, "# 分析\n拆成两步");

        var payload = Assert.IsType<PlanPayload>(message.Payload);
        Assert.Equal(ConversationRole.Assistant, message.Role);
        Assert.Equal("已生成创作计划，共 2 个步骤。\n请在左侧「执行计划」面板查看详细步骤，确认后点击「开始执行」。", message.Summary);
        Assert.Equal(raw, payload.RawContent);
        Assert.Equal(["起草第一章", "校验伏笔"], payload.Steps.Select(step => step.Title).ToArray());
        Assert.Equal("场景：入山", payload.Steps[0].Detail);
        Assert.Equal("拆成两步", Assert.Single(message.AnalysisBlocks).Body);
    }

    [Fact]
    public void MapFromStreamingResult_reports_parse_failed_when_no_steps_are_found()
    {
        var mapper = new PlanModeMapper(new PlanStepParser());

        var message = mapper.MapFromStreamingResult("生成计划", "我会先想一想，但没有列步骤", null);

        var payload = Assert.IsType<PlanPayload>(message.Payload);
        Assert.Empty(payload.Steps);
        Assert.Equal("⚠️ 计划解析失败，请重新描述您的需求。", message.Summary);
        Assert.Empty(message.AnalysisBlocks);
    }

    [Fact]
    public void GenerateSummary_uses_step_count_when_plan_payload_exists()
    {
        var mapper = new PlanModeMapper(new PlanStepParser());
        var message = new ConversationMessage
        {
            Summary = "fallback",
            Payload = new PlanPayload
            {
                Steps =
                [
                    new PlanStep { Index = 1, Title = "A" },
                    new PlanStep { Index = 2, Title = "B" }
                ]
            }
        };

        Assert.Contains("2 个步骤", mapper.GenerateSummary(message));
    }

    private static void WriteContentGuide(string guidesDir)
    {
        Directory.CreateDirectory(guidesDir);
        File.WriteAllText(System.IO.Path.Combine(guidesDir, "content_guide.json"), """
        {
          "Module": "ContentGuide",
          "Chapters": {
            "vol1_ch1": {
              "ChapterId": "vol1_ch1",
              "Title": "启程",
              "Summary": "林青离开山村",
              "ChapterNumber": 1,
              "ContextIds": {
                "ChapterBlueprint": "bp-1",
                "Characters": ["char-linqing"],
                "Locations": ["loc-village"]
              },
              "Scenes": [
                {
                  "SceneNumber": 1,
                  "Title": "山村",
                  "PovCharacter": "林青",
                  "Purpose": "离别"
                }
              ]
            },
            "vol1_ch2": {
              "ChapterId": "vol1_ch2",
              "Title": "入城",
              "Summary": "林青抵达城门",
              "ChapterNumber": 2,
              "MainGoal": "进入主城",
              "ContextIds": {
                "ChapterBlueprint": "bp-2",
                "Characters": ["char-linqing"],
                "Locations": ["loc-city"]
              },
              "Scenes": [
                {
                  "SceneNumber": 1,
                  "Title": "城门",
                  "PovCharacter": "林青",
                  "Purpose": "建立新冲突",
                  "Opening": "抵达",
                  "Development": "盘查"
                }
              ]
            }
          }
        }
        """);
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-plan-mode-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
