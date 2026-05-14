using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using System.IO;
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
    public async Task SendAsync_in_ask_mode_uses_provider_endpoint_and_model_name_for_routed_config()
    {
        using var workspace = new TempDirectory();
        var library = Path.Combine(workspace.Path, "Library");
        var configs = Path.Combine(workspace.Path, "Configurations");
        WriteLibrary(library);
        var store = new FileAIConfigurationStore(library, configs);
        var router = new StubRouter();
        router.Map[AITaskPurpose.Chat] = new UserConfiguration
        {
            ProviderId = "openai",
            ModelId = "gpt-id",
            ApiKey = "ask-key",
            CustomEndpoint = null,
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
        var orchestrator = CreateOrchestrator(handler, router, store);
        var session = new ConversationSession { Mode = ChatMode.Ask };

        await DrainAsync(orchestrator.SendAsync(session, "ping"));

        Assert.Equal("https://api.example.test/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        using var document = JsonDocument.Parse(handler.Body);
        Assert.Equal("gpt-real", document.RootElement.GetProperty("model").GetString());
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

    private static ConversationOrchestrator CreateOrchestrator(
        CapturingHandler handler,
        IAIModelRouter? router = null,
        FileAIConfigurationStore? configurationStore = null)
    {
        return new ConversationOrchestrator(
            new OpenAICompatibleChatClient(new HttpClient(handler)),
            new TagBasedThinkingStrategy(),
            new InMemorySessionStore(),
            [],
            new AskModeMapper(),
            new PlanModeMapper(new PlanStepParser()),
            new AgentModeMapper(),
            router: router,
            configurationStore: configurationStore);
    }

    private static void WriteLibrary(string library)
    {
        Directory.CreateDirectory(library);
        File.WriteAllText(Path.Combine(library, "providers.json"), """
        { "Providers": [
          { "Id": "openai", "Name": "OpenAI", "ApiEndpoint": "https://api.example.test", "Order": 1 }
        ] }
        """);
        File.WriteAllText(Path.Combine(library, "models.json"), """
        { "Models": [
          { "Id": "gpt-id", "ProviderId": "openai", "Name": "gpt-real", "Order": 1 }
        ] }
        """);
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

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-orchestrator-routing-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
