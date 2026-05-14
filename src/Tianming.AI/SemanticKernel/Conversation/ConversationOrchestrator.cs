using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TM.Framework.UI.Workspace.RightPanel.Modes;
using TM.Services.Framework.AI.Core;
using TM.Services.Framework.AI.Core.Routing;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Config;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Mapping;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Models;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Parsing;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Thinking;

namespace TM.Services.Framework.AI.SemanticKernel.Conversation;

/// <summary>
/// 对话核心编排器。负责：
/// - 创建/恢复会话
/// - 按模式（Ask/Plan/Agent）调用 AI 并产出流式增量
/// - Agent 模式的工具调用循环
/// - 会话持久化
/// </summary>
public sealed class ConversationOrchestrator : IConversationOrchestrator
{
    private readonly OpenAICompatibleChatClient _chat;
    private readonly TagBasedThinkingStrategy _thinking;
    private readonly IFileSessionStore _sessions;
    private readonly IReadOnlyList<IConversationTool> _tools;
    private readonly AskModeMapper _askMapper;
    private readonly PlanModeMapper _planMapper;
    private readonly AgentModeMapper _agentMapper;
    private readonly string? _projectRoot;
    private readonly IAIModelRouter? _router;

    public ConversationOrchestrator(
        OpenAICompatibleChatClient chat,
        TagBasedThinkingStrategy thinking,
        IFileSessionStore sessions,
        IEnumerable<IConversationTool> tools,
        AskModeMapper askMapper,
        PlanModeMapper planMapper,
        AgentModeMapper agentMapper,
        string? projectRoot = null,
        IAIModelRouter? router = null)
    {
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
        _thinking = thinking ?? throw new ArgumentNullException(nameof(thinking));
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _tools = tools?.ToList() ?? new List<IConversationTool>();
        _askMapper = askMapper ?? throw new ArgumentNullException(nameof(askMapper));
        _planMapper = planMapper ?? throw new ArgumentNullException(nameof(planMapper));
        _agentMapper = agentMapper ?? throw new ArgumentNullException(nameof(agentMapper));
        _projectRoot = projectRoot;
        _router = router;
    }

    /// <summary>创建新会话或恢复已有会话。</summary>
    public async Task<ConversationSession> StartSessionAsync(
        ChatMode mode, string? sessionId = null, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(sessionId))
        {
            // Try to restore existing session from store
            var session = await RestoreSessionAsync(sessionId, mode, ct);
            if (session != null)
                return session;
        }

        // Create new session
        return new ConversationSession { Mode = mode };
    }

    /// <summary>发送用户消息并流式产出响应增量。</summary>
    public async IAsyncEnumerable<ChatStreamDelta> SendAsync(
        ConversationSession session, string userInput,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            yield break;

        session.ClearStreamingState();

        // Build system message from mode profile
        var profile = ConversationModeProfileCatalog.GetProfile(session.Mode);
        var messages = new List<OpenAICompatibleChatMessage>();

        var systemPrompt = BuildSystemPrompt(profile);
        if (!string.IsNullOrEmpty(systemPrompt))
        {
            messages.Add(new OpenAICompatibleChatMessage("system", systemPrompt));
        }

        // Add history
        foreach (var msg in session.History)
        {
            messages.Add(new OpenAICompatibleChatMessage(
                msg.Role == ConversationRole.User ? "user" : "assistant",
                msg.Summary));
        }

        // Add user input
        messages.Add(new OpenAICompatibleChatMessage("user", userInput));

        switch (session.Mode)
        {
            case ChatMode.Ask:
                await foreach (var delta in StreamAskAsync(messages, session, ct))
                    yield return delta;
                break;

            case ChatMode.Plan:
                await foreach (var delta in StreamPlanAsync(messages, userInput, session, ct))
                    yield return delta;
                break;

            case ChatMode.Agent:
                await foreach (var delta in StreamAgentAsync(messages, session, ct))
                    yield return delta;
                break;
        }

        // Persist after response
        await PersistAsync(session, ct);
    }

    /// <summary>持久化会话到文件系统。</summary>
    public async Task PersistAsync(ConversationSession session, CancellationToken ct = default)
    {
        // Build title from first user message if not set
        if (string.IsNullOrEmpty(session.Title) && session.History.Count > 0)
        {
            var firstUser = session.History.FirstOrDefault(m => m.Role == ConversationRole.User);
            if (firstUser != null)
            {
                session.Title = firstUser.Summary.Length > 50
                    ? firstUser.Summary[..50] + "..."
                    : firstUser.Summary;
            }
        }

        await _sessions.SaveSessionAsync(session, ct);
    }

    // ---- Private helpers ----

    private async Task<ConversationSession?> RestoreSessionAsync(string sessionId, ChatMode mode, CancellationToken ct)
    {
        try
        {
            var restored = await _sessions.LoadSessionAsync(sessionId, ct);
            if (restored != null)
            {
                restored.Mode = mode;
                return restored;
            }
        }
        catch
        {
            // If restore fails, create a new session
        }
        return null;
    }

    private static string BuildSystemPrompt(ConversationModeProfile profile)
    {
        return profile.Description;
    }

    private async IAsyncEnumerable<ChatStreamDelta> StreamAskAsync(
        List<OpenAICompatibleChatMessage> messages,
        ConversationSession session,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var request = BuildRequest(AITaskPurpose.Chat, messages);
        var fullContent = "";

        await foreach (var chunk in _chat.StreamAsync(request, ct))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                fullContent += chunk.Content;
                var route = _thinking.Extract(chunk.Content);

                if (route.ThinkingContent != null)
                {
                    session.CurrentThinking = (session.CurrentThinking ?? "") + route.ThinkingContent;
                    yield return new ThinkingDelta(route.ThinkingContent);
                }
                if (route.AnswerContent != null)
                {
                    session.CurrentAnswer = (session.CurrentAnswer ?? "") + route.AnswerContent;
                    yield return new AnswerDelta(route.AnswerContent);
                }
            }
        }

        // Flush any remaining buffered content
        var flush = _thinking.Flush();
        if (flush.ThinkingContent != null)
        {
            session.CurrentThinking = (session.CurrentThinking ?? "") + flush.ThinkingContent;
            yield return new ThinkingDelta(flush.ThinkingContent);
        }
        if (flush.AnswerContent != null)
        {
            session.CurrentAnswer = (session.CurrentAnswer ?? "") + flush.AnswerContent;
            yield return new AnswerDelta(flush.AnswerContent);
        }

        // Add assistant message to history
        session.History.Add(new ConversationMessage
        {
            Role = ConversationRole.Assistant,
            Timestamp = DateTime.Now,
            Summary = session.CurrentAnswer ?? fullContent,
            AnalysisRaw = session.CurrentThinking ?? string.Empty,
        });

        // Add user message to history
        session.History.Add(new ConversationMessage
        {
            Role = ConversationRole.User,
            Timestamp = DateTime.Now,
            Summary = messages.Last().Content,
        });
    }

    private async IAsyncEnumerable<ChatStreamDelta> StreamPlanAsync(
        List<OpenAICompatibleChatMessage> messages,
        string userInput,
        ConversationSession session,
        [EnumeratorCancellation] CancellationToken ct)
    {
        var request = BuildRequest(AITaskPurpose.Writing, messages);
        var fullContent = "";

        await foreach (var chunk in _chat.StreamAsync(request, ct))
        {
            if (!string.IsNullOrEmpty(chunk.Content))
            {
                fullContent += chunk.Content;
                var route = _thinking.Extract(chunk.Content);

                if (route.ThinkingContent != null)
                {
                    session.CurrentThinking = (session.CurrentThinking ?? "") + route.ThinkingContent;
                    yield return new ThinkingDelta(route.ThinkingContent);
                }
                if (route.AnswerContent != null)
                {
                    session.CurrentAnswer = (session.CurrentAnswer ?? "") + route.AnswerContent;
                    yield return new AnswerDelta(route.AnswerContent);
                }
            }
        }

        // Flush remaining
        var flush = _thinking.Flush();
        if (flush.ThinkingContent != null)
        {
            session.CurrentThinking = (session.CurrentThinking ?? "") + flush.ThinkingContent;
            yield return new ThinkingDelta(flush.ThinkingContent);
        }
        if (flush.AnswerContent != null)
        {
            session.CurrentAnswer = (session.CurrentAnswer ?? "") + flush.AnswerContent;
            yield return new AnswerDelta(flush.AnswerContent);
        }

        // Parse plan steps from full answer
        var parser = new PlanStepParser();
        var steps = parser.Parse(session.CurrentAnswer ?? fullContent);
        foreach (var step in steps)
        {
            session.CurrentPlanSteps.Add(step);
            yield return new PlanStepDelta(step);
        }

        session.History.Add(new ConversationMessage
        {
            Role = ConversationRole.Assistant,
            Timestamp = DateTime.Now,
            Summary = session.CurrentAnswer ?? fullContent,
            AnalysisRaw = session.CurrentThinking ?? string.Empty,
            Payload = session.CurrentPlanSteps.Count > 0
                ? new PlanPayload { Steps = session.CurrentPlanSteps.ToArray(), RawContent = session.CurrentAnswer ?? fullContent }
                : null,
        });

        session.History.Add(new ConversationMessage
        {
            Role = ConversationRole.User,
            Timestamp = DateTime.Now,
            Summary = messages.Last().Content,
        });
    }

    private async IAsyncEnumerable<ChatStreamDelta> StreamAgentAsync(
        List<OpenAICompatibleChatMessage> messages,
        ConversationSession session,
        [EnumeratorCancellation] CancellationToken ct)
    {
        // Build tool definitions
        var toolDefinitions = _tools.Count > 0
            ? _tools.Select(t => new OpenAICompatibleToolDefinition
            {
                Function = new OpenAICompatibleFunctionDefinition
                {
                    Name = t.Name,
                    Description = t.Description,
                    Parameters = t.ParameterSchemaJson
                }
            }).ToList()
            : null;

        var maxToolRounds = 5;
        var round = 0;
        var fullContent = "";

        while (round < maxToolRounds)
        {
            round++;
            var request = BuildRequest(AITaskPurpose.Chat, messages, toolDefinitions);
            var assistantContent = "";
            List<OpenAICompatibleToolCall>? collectedToolCalls = null;

            await foreach (var chunk in _chat.StreamAsync(request, ct))
            {
                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    assistantContent += chunk.Content;
                    var route = _thinking.Extract(chunk.Content);

                    if (route.ThinkingContent != null)
                    {
                        session.CurrentThinking = (session.CurrentThinking ?? "") + route.ThinkingContent;
                        yield return new ThinkingDelta(route.ThinkingContent);
                    }
                    if (route.AnswerContent != null)
                    {
                        session.CurrentAnswer = (session.CurrentAnswer ?? "") + route.AnswerContent;
                        yield return new AnswerDelta(route.AnswerContent);
                    }
                }

                if (chunk.ToolCalls is { Count: > 0 })
                {
                    collectedToolCalls ??= new List<OpenAICompatibleToolCall>();
                    collectedToolCalls.AddRange(chunk.ToolCalls);
                }
            }

            // Flush remaining thinking
            var flush = _thinking.Flush();
            if (flush.ThinkingContent != null)
            {
                session.CurrentThinking = (session.CurrentThinking ?? "") + flush.ThinkingContent;
                yield return new ThinkingDelta(flush.ThinkingContent);
            }
            if (flush.AnswerContent != null)
            {
                session.CurrentAnswer = (session.CurrentAnswer ?? "") + flush.AnswerContent;
                yield return new AnswerDelta(flush.AnswerContent);
            }

            // Merge accumulated content
            fullContent += assistantContent;

            // If no tool calls, we're done
            if (collectedToolCalls is not { Count: > 0 })
                break;

            // Process tool calls
            // Merge partial tool_calls (streaming may send them in chunks)
            var mergedToolCalls = MergeToolCalls(collectedToolCalls);

            foreach (var toolCall in mergedToolCalls)
            {
                var tool = _tools.FirstOrDefault(t => t.Name == toolCall.FunctionName);
                if (tool == null)
                {
                    yield return new ToolResultDelta(toolCall.Id, $"未知工具: {toolCall.FunctionName}");
                    messages.Add(new OpenAICompatibleChatMessage("tool",
                        $"Error: tool '{toolCall.FunctionName}' not found"));
                    continue;
                }

                // Yield tool call delta
                yield return new ToolCallDelta(toolCall.Id, tool.Name, toolCall.Arguments);

                // Invoke tool
                Dictionary<string, object?> args;
                try
                {
                    args = string.IsNullOrEmpty(toolCall.Arguments)
                        ? new Dictionary<string, object?>()
                        : JsonSerializer.Deserialize<Dictionary<string, object?>>(toolCall.Arguments)
                          ?? new Dictionary<string, object?>();
                }
                catch (JsonException)
                {
                    args = new Dictionary<string, object?>();
                }

                var toolResult = await tool.InvokeAsync(args, ct);

                // Yield tool result
                yield return new ToolResultDelta(toolCall.Id, toolResult);

                // Add tool result as a "tool" message
                messages.Add(new OpenAICompatibleChatMessage("tool", toolResult)
                {
                    // We need to track tool_call_id for OpenAI API, but for simplicity
                    // we pass it via the content prefix
                });

                // Record in session
                session.CurrentToolCalls.Add(new ToolCallRecord
                {
                    FunctionName = tool.Name,
                    Arguments = toolCall.Arguments,
                    Result = toolResult,
                    Status = ToolCallStatus.Completed,
                    StartTime = DateTime.Now,
                    EndTime = DateTime.Now,
                });
            }

            // Continue the loop — the assistant will get tool results and continue
        }

        // Add assistant message to history
        session.History.Add(new ConversationMessage
        {
            Role = ConversationRole.Assistant,
            Timestamp = DateTime.Now,
            Summary = session.CurrentAnswer ?? fullContent,
            AnalysisRaw = session.CurrentThinking ?? string.Empty,
        });

        session.History.Add(new ConversationMessage
        {
            Role = ConversationRole.User,
            Timestamp = DateTime.Now,
            Summary = messages.Last(m => m.Role == "user").Content,
        });
    }

    private OpenAICompatibleChatRequest BuildRequest(
        AITaskPurpose purpose,
        List<OpenAICompatibleChatMessage> messages,
        List<OpenAICompatibleToolDefinition>? tools = null)
    {
        var config = _router?.Resolve(purpose) ?? FallbackConfig();
        return new OpenAICompatibleChatRequest
        {
            BaseUrl = config.CustomEndpoint ?? string.Empty,
            ApiKey = config.ApiKey,
            Model = config.ModelId,
            Messages = messages,
            Temperature = config.Temperature,
            MaxTokens = config.MaxTokens,
            Tools = tools
        };
    }

    private static UserConfiguration FallbackConfig()
    {
        return new UserConfiguration
        {
            CustomEndpoint = AppConfig.BaseUrl,
            ApiKey = AppConfig.ApiKey,
            ModelId = AppConfig.Model,
            Temperature = AppConfig.Temperature ?? 0.7,
            MaxTokens = AppConfig.MaxTokens ?? 4096,
        };
    }

    /// <summary>Merge streamed tool_call chunks into complete calls (accumulate arguments).</summary>
    private static List<OpenAICompatibleToolCall> MergeToolCalls(List<OpenAICompatibleToolCall> chunks)
    {
        var merged = new Dictionary<string, OpenAICompatibleToolCall>();
        foreach (var chunk in chunks)
        {
            if (string.IsNullOrEmpty(chunk.Id))
                continue;
            if (merged.TryGetValue(chunk.Id, out var existing))
            {
                // Accumulate arguments and function name (partial chunks)
                existing.Arguments += chunk.Arguments;
                if (!string.IsNullOrEmpty(chunk.FunctionName))
                    existing.FunctionName = chunk.FunctionName;
                if (!string.IsNullOrEmpty(chunk.Id) && string.IsNullOrEmpty(existing.Id))
                    existing.Id = chunk.Id;
            }
            else
            {
                merged[chunk.Id] = new OpenAICompatibleToolCall
                {
                    Id = chunk.Id,
                    Type = chunk.Type,
                    FunctionName = chunk.FunctionName,
                    Arguments = chunk.Arguments
                };
            }
        }
        return merged.Values.ToList();
    }

    /// <summary>Global AI config holder — set before use.</summary>
    public static class AppConfig
    {
        public static string BaseUrl { get; set; } = string.Empty;
        public static string ApiKey { get; set; } = string.Empty;
        public static string Model { get; set; } = string.Empty;
        public static double? Temperature { get; set; } = 0.7;
        public static int? MaxTokens { get; set; } = 4096;
    }
}
