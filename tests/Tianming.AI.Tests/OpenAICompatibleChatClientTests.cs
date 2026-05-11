using System.Net;
using System.Text;
using System.Text.Json;
using TM.Framework.Common.Helpers.AI;
using TM.Services.Framework.AI.Core;
using Xunit;

namespace Tianming.AI.Tests;

public class OpenAICompatibleChatClientTests
{
    [Fact]
    public async Task CompleteAsync_posts_openai_compatible_request_and_parses_message_content()
    {
        using var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {
              "choices": [
                { "message": { "role": "assistant", "content": "生成结果" } }
              ],
              "usage": { "prompt_tokens": 5, "completion_tokens": 7, "total_tokens": 12 }
            }
            """, Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        var client = new OpenAICompatibleChatClient(httpClient);

        var result = await client.CompleteAsync(new OpenAICompatibleChatRequest
        {
            BaseUrl = "https://api.example.test",
            ApiKey = "sk-test",
            Model = "[免费]qwen-plus-free",
            Messages =
            [
                new OpenAICompatibleChatMessage("system", "开发者提示"),
                new OpenAICompatibleChatMessage("user", "写一章")
            ],
            Temperature = 0.2,
            MaxTokens = 512
        });

        Assert.True(result.Success);
        Assert.Equal("生成结果", result.Content);
        Assert.Equal(5, result.PromptTokens);
        Assert.Equal(7, result.CompletionTokens);
        Assert.Equal(12, result.TotalTokens);
        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.example.test/v1/chat/completions", handler.Request.RequestUri!.ToString());
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal("sk-test", handler.Request.Headers.Authorization.Parameter);
        Assert.Contains(handler.Request.Headers, h => h.Key == "X-Api-Key" && h.Value.Contains("sk-test"));
        Assert.Contains(handler.Request.Headers, h => h.Key == "HTTP-Referer" && h.Value.Contains("https://cherry-ai.com"));
        Assert.Contains(handler.Request.Headers.UserAgent, product => product.Product?.Name == "Mozilla");

        using var document = JsonDocument.Parse(handler.Body);
        var root = document.RootElement;
        Assert.Equal("qwen-plus", root.GetProperty("model").GetString());
        Assert.Equal(0.2, root.GetProperty("temperature").GetDouble(), precision: 3);
        Assert.Equal(512, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal("开发者提示", root.GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.Equal("写一章", root.GetProperty("messages")[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task CompleteAsync_preserves_base_url_that_already_has_api_version()
    {
        using var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            { "choices": [ { "message": { "content": "ok" } } ] }
            """, Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        var client = new OpenAICompatibleChatClient(httpClient);

        var result = await client.CompleteAsync(new OpenAICompatibleChatRequest
        {
            BaseUrl = "https://api.example.test/v1",
            Model = "model",
            Messages = [new OpenAICompatibleChatMessage("user", "ping")]
        });

        Assert.True(result.Success);
        Assert.Equal("https://api.example.test/v1/chat/completions", handler.Request!.RequestUri!.ToString());
    }

    [Fact]
    public async Task CompleteAsync_maps_http_error_payload_to_failure()
    {
        using var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent("""
            { "error": { "message": "rate limit exceeded" } }
            """, Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        var client = new OpenAICompatibleChatClient(httpClient);

        var result = await client.CompleteAsync(new OpenAICompatibleChatRequest
        {
            BaseUrl = "https://api.example.test/v1",
            Model = "model",
            Messages = [new OpenAICompatibleChatMessage("user", "ping")]
        });

        Assert.False(result.Success);
        Assert.Equal("rate limit exceeded", result.ErrorMessage);
        Assert.Equal((int)HttpStatusCode.TooManyRequests, result.StatusCode);
    }

    [Fact]
    public async Task CompleteAsync_rejects_non_openai_response_shape()
    {
        using var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{ "message": "plain" }""", Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        var client = new OpenAICompatibleChatClient(httpClient);

        var result = await client.CompleteAsync(new OpenAICompatibleChatRequest
        {
            BaseUrl = "https://api.example.test/v1",
            Model = "model",
            Messages = [new OpenAICompatibleChatMessage("user", "ping")]
        });

        Assert.False(result.Success);
        Assert.Equal("响应格式不符合 OpenAI 规范", result.ErrorMessage);
    }

    [Fact]
    public async Task StreamAsync_posts_streaming_request_and_yields_delta_content()
    {
        using var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            data: {"choices":[{"delta":{"content":"你"}}]}

            data: {"choices":[{"delta":{"content":"好"}}]}

            data: [DONE]

            """, Encoding.UTF8, "text/event-stream")
        });
        using var httpClient = new HttpClient(handler);
        var client = new OpenAICompatibleChatClient(httpClient);

        var chunks = new List<OpenAICompatibleStreamChunk>();
        await foreach (var chunk in client.StreamAsync(new OpenAICompatibleChatRequest
        {
            BaseUrl = "https://api.example.test",
            Model = "qwen-plus",
            Messages = [new OpenAICompatibleChatMessage("user", "ping")]
        }))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(["你", "好"], chunks.Select(chunk => chunk.Content).ToArray());
        using var document = JsonDocument.Parse(handler.Body);
        Assert.True(document.RootElement.GetProperty("stream").GetBoolean());
    }

    [Fact]
    public async Task StreamAsync_ignores_empty_streaming_delta_and_reads_finish_reason()
    {
        using var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            data: {"choices":[{"delta":{"role":"assistant"}}]}

            data: {"choices":[{"delta":{"content":"正文"},"finish_reason":"stop"}]}

            data: [DONE]

            """, Encoding.UTF8, "text/event-stream")
        });
        using var httpClient = new HttpClient(handler);
        var client = new OpenAICompatibleChatClient(httpClient);

        var chunks = new List<OpenAICompatibleStreamChunk>();
        await foreach (var chunk in client.StreamAsync(new OpenAICompatibleChatRequest
        {
            BaseUrl = "https://api.example.test",
            Model = "qwen-plus",
            Messages = [new OpenAICompatibleChatMessage("user", "ping")]
        }))
        {
            chunks.Add(chunk);
        }

        var singleChunk = Assert.Single(chunks);
        Assert.Equal("正文", singleChunk.Content);
        Assert.Equal("stop", singleChunk.FinishReason);
    }

    [Fact]
    public async Task StreamAsync_throws_when_http_status_is_not_success()
    {
        using var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""
            { "error": { "message": "invalid api key" } }
            """, Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        var client = new OpenAICompatibleChatClient(httpClient);

        var ex = await Assert.ThrowsAsync<OpenAICompatibleChatException>(async () =>
        {
            await foreach (var _ in client.StreamAsync(new OpenAICompatibleChatRequest
            {
                BaseUrl = "https://api.example.test",
                Model = "qwen-plus",
                Messages = [new OpenAICompatibleChatMessage("user", "ping")]
            }))
            {
            }
        });

        Assert.Equal("invalid api key", ex.Message);
        Assert.Equal(401, ex.StatusCode);
    }

    [Fact]
    public async Task PromptTextGenerator_sends_system_and_user_messages_to_chat_client()
    {
        using var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            { "choices": [ { "message": { "content": "提示词正文" } } ] }
            """, Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        var generator = new OpenAICompatiblePromptTextGenerator(httpClient, new OpenAICompatiblePromptTextGeneratorOptions
        {
            BaseUrl = "https://api.example.test/v1",
            ApiKey = "sk-test",
            Model = "qwen-plus",
            SystemPrompt = "你是提示词工程师",
            Temperature = 0.1,
            MaxTokens = 256
        });

        PromptGenerationAiResult result = await generator.GenerateAsync("生成章节提示词");

        Assert.True(result.Success);
        Assert.Equal("提示词正文", result.Content);
        using var document = JsonDocument.Parse(handler.Body);
        var messages = document.RootElement.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("你是提示词工程师", messages[0].GetProperty("content").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("生成章节提示词", messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task PromptTextGenerator_maps_chat_failure_to_prompt_generation_failure()
    {
        using var handler = new CapturingHandler(new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("""
            { "error": { "message": "invalid api key" } }
            """, Encoding.UTF8, "application/json")
        });
        using var httpClient = new HttpClient(handler);
        var generator = new OpenAICompatiblePromptTextGenerator(httpClient, new OpenAICompatiblePromptTextGeneratorOptions
        {
            BaseUrl = "https://api.example.test/v1",
            Model = "qwen-plus"
        });

        PromptGenerationAiResult result = await generator.GenerateAsync("生成章节提示词");

        Assert.False(result.Success);
        Assert.Empty(result.Content);
        Assert.Equal("invalid api key", result.ErrorMessage);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage _response;

        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = string.Empty;

        public CapturingHandler(HttpResponseMessage response)
        {
            _response = response;
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
