using System;
using System.Collections.Generic;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation.Models;

public enum ConversationRole
{
    User,
    Assistant,
    System
}

public sealed class ConversationMessage
{
    public Guid RunId { get; init; }

    public ConversationRole Role { get; init; } = ConversationRole.Assistant;

    public DateTime Timestamp { get; init; } = DateTime.Now;

    public string Summary { get; set; } = string.Empty;

    public string AnalysisRaw { get; set; } = string.Empty;

    public IReadOnlyList<ThinkingBlock> AnalysisBlocks { get; set; } = Array.Empty<ThinkingBlock>();

    public bool HasAnalysis => !string.IsNullOrEmpty(AnalysisRaw);

    public object? Payload { get; set; }

    public bool HasPayload => Payload != null;
}
