using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Text.Json;
using TM.Services.Framework.AI.Core;
using TM.Services.Framework.AI.Core.Routing;
using Xunit;

namespace Tianming.AI.Tests.Core.Routing;

public class RoutedChatClientTests
{
    private sealed class StubRouter : IAIModelRouter
    {
        public Dictionary<AITaskPurpose, UserConfiguration> Map { get; } = new();

        public UserConfiguration Resolve(AITaskPurpose purpose) => Map[purpose];
    }

    [Fact]
    public void BuildRequestFor_uses_routed_configuration()
    {
        var router = new StubRouter();
        router.Map[AITaskPurpose.Writing] = new UserConfiguration
        {
            CustomEndpoint = "https://writing.example.com",
            ModelId = "writing-opus",
            ApiKey = "key-w",
            Temperature = 0.8,
            MaxTokens = 8192,
        };
        var inner = new OpenAICompatibleChatClient(new HttpClient(new CapturingHandler(HttpStatusCode.OK, "{}")));
        var client = new RoutedChatClient(inner, router);

        var request = client.BuildRequestFor(
            AITaskPurpose.Writing,
            [new OpenAICompatibleChatMessage("user", "test")]);

        Assert.Equal("https://writing.example.com", request.BaseUrl);
        Assert.Equal("writing-opus", request.Model);
        Assert.Equal("key-w", request.ApiKey);
        Assert.Equal(0.8, request.Temperature);
        Assert.Equal(8192, request.MaxTokens);
    }

    [Fact]
    public async Task CompleteAsync_uses_router_selected_request()
    {
        var router = new StubRouter();
        router.Map[AITaskPurpose.Chat] = new UserConfiguration
        {
            CustomEndpoint = "https://chat.example.com/v1",
            ModelId = "chat-haiku",
            ApiKey = "key-chat",
            Temperature = 0.2,
            MaxTokens = 1024,
        };
        using var handler = new CapturingHandler(HttpStatusCode.OK, """
        { "choices": [ { "message": { "content": "ok" } } ] }
        """);
        var client = new RoutedChatClient(new OpenAICompatibleChatClient(new HttpClient(handler)), router);

        var result = await client.CompleteAsync(
            AITaskPurpose.Chat,
            [new OpenAICompatibleChatMessage("user", "ping")]);

        Assert.True(result.Success);
        Assert.Equal("https://chat.example.com/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        Assert.Equal("key-chat", handler.Request.Headers.Authorization!.Parameter);
        using var document = JsonDocument.Parse(handler.Body);
        Assert.Equal("chat-haiku", document.RootElement.GetProperty("model").GetString());
        Assert.Equal(0.2, document.RootElement.GetProperty("temperature").GetDouble(), precision: 3);
        Assert.Equal(1024, document.RootElement.GetProperty("max_tokens").GetInt32());
    }

    [Fact]
    public async Task StreamAsync_passes_tools_with_routed_configuration()
    {
        var router = new StubRouter();
        router.Map[AITaskPurpose.Validation] = new UserConfiguration
        {
            CustomEndpoint = "https://validation.example.com",
            ModelId = "validator-mini",
            ApiKey = "key-v",
            Temperature = 0.1,
            MaxTokens = 512,
        };
        using var handler = new CapturingHandler(
            HttpStatusCode.OK,
            """
            data: {"choices":[{"delta":{"content":"v"}}]}

            data: [DONE]

            """,
            "text/event-stream");
        var client = new RoutedChatClient(new OpenAICompatibleChatClient(new HttpClient(handler)), router);
        var tools = new List<OpenAICompatibleToolDefinition>
        {
            new()
            {
                Function = new OpenAICompatibleFunctionDefinition
                {
                    Name = "check_consistency",
                    Description = "validate",
                    Parameters = "{}"
                }
            }
        };

        var chunks = new List<OpenAICompatibleStreamChunk>();
        await foreach (var chunk in client.StreamAsync(
            AITaskPurpose.Validation,
            [new OpenAICompatibleChatMessage("user", "validate this")],
            tools))
        {
            chunks.Add(chunk);
        }

        Assert.Equal("v", Assert.Single(chunks).Content);
        Assert.Equal("https://validation.example.com/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        using var document = JsonDocument.Parse(handler.Body);
        var root = document.RootElement;
        Assert.True(root.GetProperty("stream").GetBoolean());
        Assert.Equal("validator-mini", root.GetProperty("model").GetString());
        Assert.Equal("check_consistency", root.GetProperty("tools")[0].GetProperty("function").GetProperty("name").GetString());
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = string.Empty;

        public CapturingHandler(HttpStatusCode statusCode, string body, string mediaType = "application/json")
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
