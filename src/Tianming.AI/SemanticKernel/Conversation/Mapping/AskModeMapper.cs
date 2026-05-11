using System;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Helpers;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Models;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation.Mapping;

public sealed class AskModeMapper : IConversationMessageMapper
{
    public ConversationMessage MapFromStreamingResult(string userInput, string rawContent, string? thinking)
    {
        return new ConversationMessage
        {
            Role = ConversationRole.Assistant,
            Timestamp = DateTime.Now,
            Summary = rawContent,
            AnalysisRaw = thinking ?? string.Empty,
            AnalysisBlocks = ThinkingBlockParser.Parse(thinking),
            Payload = null
        };
    }

    public string GenerateSummary(ConversationMessage message)
    {
        return message.Summary;
    }
}
