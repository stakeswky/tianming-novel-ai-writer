using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Framework.AI.SemanticKernel.Conversation;

namespace TM.Services.Framework.AI.SemanticKernel;

/// <summary>
/// 对话会话持久化接口。M4.5 ConversationOrchestrator 通过此接口保存/恢复会话。
/// 默认实现基于 FileSessionStore 适配。
/// </summary>
public interface IFileSessionStore
{
    /// <summary>保存整个会话（含历史和元数据）。</summary>
    Task SaveSessionAsync(ConversationSession session, CancellationToken ct = default);

    /// <summary>按 ID 加载会话，不存在时返回 null。</summary>
    Task<ConversationSession?> LoadSessionAsync(string sessionId, CancellationToken ct = default);

    /// <summary>列出所有会话摘要，按更新时间倒序。</summary>
    Task<IReadOnlyList<SessionSummary>> ListSessionsAsync(CancellationToken ct = default);

    /// <summary>删除指定会话。</summary>
    Task DeleteSessionAsync(string sessionId, CancellationToken ct = default);
}

/// <summary>会话摘要信息（用于列表展示）。</summary>
public sealed class SessionSummary
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public System.DateTime UpdatedAt { get; set; }
    public int MessageCount { get; set; }
}
