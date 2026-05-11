namespace TM.Services.Framework.AI.SemanticKernel.Conversation.Config;

public sealed class ConversationDisplayPolicy
{
    public bool ShowAnalysis { get; init; } = true;

    public bool AnalysisExpandedByDefault { get; init; }

    public string? DefaultPayloadTarget { get; init; }

    public bool HideRawContentInBubble { get; init; }
}
