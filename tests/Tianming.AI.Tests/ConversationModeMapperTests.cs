using TM.Services.Framework.AI.SemanticKernel.Conversation.Mapping;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Models;
using Xunit;

namespace Tianming.AI.Tests;

public class ConversationModeMapperTests
{
    [Fact]
    public void AskModeMapper_maps_streaming_result_to_assistant_message_without_payload()
    {
        var mapper = new AskModeMapper();

        var message = mapper.MapFromStreamingResult(
            userInput: "问题",
            rawContent: "答案正文",
            thinking: "# 分析\n先判断");

        Assert.Equal(ConversationRole.Assistant, message.Role);
        Assert.Equal("答案正文", message.Summary);
        Assert.Equal("# 分析\n先判断", message.AnalysisRaw);
        Assert.Null(message.Payload);
        var block = Assert.Single(message.AnalysisBlocks);
        Assert.Equal("分析", block.Title);
        Assert.Equal("先判断", block.Body);
        Assert.Equal("答案正文", mapper.GenerateSummary(message));
    }

    [Fact]
    public void AgentModeMapper_maps_streaming_result_to_agent_payload()
    {
        var mapper = new AgentModeMapper();

        var message = mapper.MapFromStreamingResult(
            userInput: "执行任务",
            rawContent: "开始执行",
            thinking: "任务拆解：\n调用工具");

        Assert.Equal(ConversationRole.Assistant, message.Role);
        Assert.Equal("开始执行", message.Summary);
        Assert.IsType<AgentPayload>(message.Payload);
        Assert.Equal("任务拆解", Assert.Single(message.AnalysisBlocks).Title);
    }

    [Fact]
    public void ConversationMessage_reports_analysis_and_payload_presence()
    {
        var message = new ConversationMessage
        {
            Summary = "完成",
            AnalysisRaw = "分析",
            Payload = new AgentPayload()
        };

        Assert.True(message.HasAnalysis);
        Assert.True(message.HasPayload);
    }
}
