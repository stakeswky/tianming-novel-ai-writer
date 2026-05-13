using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TM.Framework.UI.Workspace.RightPanel.Modes;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation;

public interface IConversationOrchestrator
{
    Task<ConversationSession> StartSessionAsync(ChatMode mode, string? sessionId = null, CancellationToken ct = default);
    IAsyncEnumerable<ChatStreamDelta> SendAsync(ConversationSession session, string userInput, CancellationToken ct = default);
    Task PersistAsync(ConversationSession session, CancellationToken ct = default);
}
