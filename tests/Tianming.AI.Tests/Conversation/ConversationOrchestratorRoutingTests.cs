using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using TM.Framework.UI.Workspace.RightPanel.Modes;
using TM.Services.Framework.AI.Core;
using TM.Services.Framework.AI.Core.Routing;
using TM.Services.Framework.AI.SemanticKernel.Conversation;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Mapping;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Parsing;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Thinking;
using Xunit;

namespace Tianming.AI.Tests.Conversation;

public class ConversationOrchestratorRoutingTests
{
    [Fact]
    public async Task SendAsync_in_ask_mode_uses_chat_routing_when_router_present()
    {
        var router = new StubRouter();
        router.Map[AITaskPurpose.Chat] = new UserConfiguration
        {
            CustomEndpoint = "https://chat-router.example.com",
            ApiKey = "ask-key",
            ModelId = "chat-model",
            Temperature = 0.25,
            MaxTokens = 1500,
        };
        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            """
            data: {"choices":[{"delta":{"content":"<answer>ok</answer>"}}]}

            data: [DONE]

            """,
            "text/event-stream");
        var orchestrator = CreateOrchestrator(handler, router);
        var session = new ConversationSession { Mode = ChatMode.Ask };

        await DrainAsync(orchestrator.SendAsync(session, "ping"));

        Assert.Equal("https://chat-router.example.com/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        using var document = JsonDocument.Parse(handler.Body);
        Assert.Equal("chat-model", document.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task SendAsync_in_plan_mode_uses_writing_routing_when_router_present()
    {
        var router = new StubRouter();
        router.Map[AITaskPurpose.Writing] = new UserConfiguration
        {
            CustomEndpoint = "https://writing-router.example.com/v1",
            ApiKey = "plan-key",
            ModelId = "writing-model",
            Temperature = 0.55,
            MaxTokens = 9000,
        };
        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            """
            data: {"choices":[{"delta":{"content":"<answer>1. Outline\n2. Draft</answer>"}}]}

            data: [DONE]

            """,
            "text/event-stream");
        var orchestrator = CreateOrchestrator(handler, router);
        var session = new ConversationSession { Mode = ChatMode.Plan };

        await DrainAsync(orchestrator.SendAsync(session, "plan chapter"));

        Assert.Equal("https://writing-router.example.com/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        using var document = JsonDocument.Parse(handler.Body);
        Assert.Equal("writing-model", document.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task SendAsync_in_agent_mode_uses_chat_routing_when_router_present()
    {
        var router = new StubRouter();
        router.Map[AITaskPurpose.Chat] = new UserConfiguration
        {
            CustomEndpoint = "https://agent-router.example.com",
            ApiKey = "agent-key",
            ModelId = "agent-chat-model",
            Temperature = 0.15,
            MaxTokens = 4096,
        };
        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            """
            data: {"choices":[{"delta":{"content":"<answer>agent ok</answer>"}}]}

            data: [DONE]

            """,
            "text/event-stream");
        var orchestrator = CreateOrchestrator(handler, router);
        var session = new ConversationSession { Mode = ChatMode.Agent };

        await DrainAsync(orchestrator.SendAsync(session, "agent run"));

        Assert.Equal("https://agent-router.example.com/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        using var document = JsonDocument.Parse(handler.Body);
        Assert.Equal("agent-chat-model", document.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task SendAsync_falls_back_to_app_config_when_router_absent()
    {
        ConversationOrchestrator.AppConfig.BaseUrl = "https://fallback.example.com";
        ConversationOrchestrator.AppConfig.ApiKey = "fallback-key";
        ConversationOrchestrator.AppConfig.Model = "fallback-model";
        ConversationOrchestrator.AppConfig.Temperature = 0.4;
        ConversationOrchestrator.AppConfig.MaxTokens = 2048;

        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            """
            data: {"choices":[{"delta":{"content":"<answer>ok</answer>"}}]}

            data: [DONE]

            """,
            "text/event-stream");
        var orchestrator = CreateOrchestrator(handler);
        var session = new ConversationSession { Mode = ChatMode.Ask };

        await DrainAsync(orchestrator.SendAsync(session, "fallback"));

        Assert.Equal("https://fallback.example.com/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        using var document = JsonDocument.Parse(handler.Body);
        var root = document.RootElement;
        Assert.Equal("fallback-model", root.GetProperty("model").GetString());
        Assert.Equal(0.4, root.GetProperty("temperature").GetDouble(), precision: 3);
        Assert.Equal(2048, root.GetProperty("max_tokens").GetInt32());
    }

    private static ConversationOrchestrator CreateOrchestrator(CapturingHandler handler, IAIModelRouter? router = null)
    {
        return new ConversationOrchestrator(
            new OpenAICompatibleChatClient(new HttpClient(handler)),
            new TagBasedThinkingStrategy(),
            new InMemorySessionStore(),
            [],
            new AskModeMapper(),
            new PlanModeMapper(new PlanStepParser()),
            new AgentModeMapper(),
            router: router);
    }

    private static async Task DrainAsync(IAsyncEnumerable<ChatStreamDelta> stream)
    {
        await foreach (var _ in stream)
        {
        }
    }

    private sealed class StubRouter : IAIModelRouter
    {
        public Dictionary<AITaskPurpose, UserConfiguration> Map { get; } = new();

        public UserConfiguration Resolve(AITaskPurpose purpose) => Map[purpose];
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = string.Empty;

        public CapturingHandler(HttpStatusCode statusCode, string body, string mediaType)
        {
            _response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, mediaType)
            };
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return _response;
        }
    }
}
