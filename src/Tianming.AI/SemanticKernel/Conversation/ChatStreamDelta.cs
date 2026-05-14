using TM.Services.Framework.AI.SemanticKernel.Conversation.Models;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation;

/// <summary>
/// 流式输出增量单元，由 ConversationOrchestrator.SendAsync 产出，
/// 经由 BulkEmitter 16ms 批量 flush 到 ObservableCollection&lt;ChatMessageViewModel&gt;。
/// </summary>
public abstract record ChatStreamDelta;

/// <summary>模型思考过程中的文本片段（对应 think/analysis 标签内内容）。</summary>
public sealed record ThinkingDelta(string Text) : ChatStreamDelta;

/// <summary>模型回答正文的文本片段。</summary>
public sealed record AnswerDelta(string Text) : ChatStreamDelta;

/// <summary>工具调用增量：从流中解析出的 tool_call 信息。</summary>
public sealed record ToolCallDelta(string ToolCallId, string ToolName, string ArgumentsJson) : ChatStreamDelta;

/// <summary>工具执行结果：工具被调用后返回的结果文本。</summary>
public sealed record ToolResultDelta(string ToolCallId, string ResultText, string? StagedId = null) : ChatStreamDelta;

/// <summary>计划模式：AI 回答解析出的 PlanStep 列表。</summary>
public sealed record PlanStepDelta(PlanStep Step) : ChatStreamDelta;
