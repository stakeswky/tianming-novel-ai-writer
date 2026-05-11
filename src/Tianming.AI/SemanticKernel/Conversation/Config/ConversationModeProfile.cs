using TM.Framework.UI.Workspace.RightPanel.Modes;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation.Config;

public sealed class ConversationModeProfile
{
    public ChatMode Mode { get; init; }

    public ConversationDisplayPolicy DisplayPolicy { get; init; } = new();

    public bool RequiresExecutionEngine { get; init; }

    public string Description { get; init; } = string.Empty;
}
