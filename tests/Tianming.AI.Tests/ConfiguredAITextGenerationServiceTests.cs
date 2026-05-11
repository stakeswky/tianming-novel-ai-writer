using System.Net;
using System.Text;
using System.Text.Json;
using TM.Framework.Common.Helpers.AI;
using TM.Services.Framework.AI.Core;
using TM.Services.Framework.AI.Monitoring;
using Xunit;

namespace Tianming.AI.Tests;

public class ConfiguredAITextGenerationServiceTests
{
    [Fact]
    public async Task GenerateAsync_returns_configuration_error_when_no_active_model_exists()
    {
        using var workspace = new TempDirectory();
        var store = new FileAIConfigurationStore(
            System.IO.Path.Combine(workspace.Path, "Library"),
            System.IO.Path.Combine(workspace.Path, "Configurations"));
        using var httpClient = new HttpClient(new CapturingHandler(HttpStatusCode.OK, "{}"));
        var service = new ConfiguredAITextGenerationService(store, () => httpClient);

        PromptGenerationAiResult result = await service.GenerateAsync("写一章");

        Assert.False(result.Success);
        Assert.Equal("当前没有激活的AI模型，请前往“智能助手 > 模型管理”完成配置后重试。", result.ErrorMessage);
    }

    [Fact]
    public async Task GenerateAsync_uses_active_configuration_provider_model_and_api_key()
    {
        using var workspace = new TempDirectory();
        var library = System.IO.Path.Combine(workspace.Path, "Library");
        var configs = System.IO.Path.Combine(workspace.Path, "Configurations");
        WriteLibrary(library);
        var secretStore = new InMemorySecretStore();
        var store = new FileAIConfigurationStore(library, configs, secretStore);
        store.AddConfiguration(new UserConfiguration
        {
            Name = "Main",
            ProviderId = "openai",
            ModelId = "gpt-id",
            ApiKey = "sk-test",
            DeveloperMessage = "你是小说助手",
            Temperature = 0.3,
            MaxTokens = 600,
            IsActive = true
        });
        var handler = new CapturingHandler(HttpStatusCode.OK, """
        { "choices": [ { "message": { "content": "生成结果" } } ] }
        """);
        using var httpClient = new HttpClient(handler);
        var service = new ConfiguredAITextGenerationService(store, () => httpClient);

        PromptGenerationAiResult result = await service.GenerateAsync("写一章");

        Assert.True(result.Success);
        Assert.Equal("生成结果", result.Content);
        Assert.Equal("https://api.example.test/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        Assert.Equal("sk-test", handler.Request.Headers.Authorization!.Parameter);
        using var document = JsonDocument.Parse(handler.Body);
        var root = document.RootElement;
        Assert.Equal("gpt-real", root.GetProperty("model").GetString());
        Assert.Equal(0.3, root.GetProperty("temperature").GetDouble(), precision: 3);
        Assert.Equal(600, root.GetProperty("max_tokens").GetInt32());
        Assert.Equal("system", root.GetProperty("messages")[0].GetProperty("role").GetString());
        Assert.Equal("你是小说助手", root.GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.Equal("user", root.GetProperty("messages")[1].GetProperty("role").GetString());
        Assert.Equal("写一章", root.GetProperty("messages")[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task GenerateAsync_prefers_custom_endpoint_and_maps_http_failure()
    {
        using var workspace = new TempDirectory();
        var library = System.IO.Path.Combine(workspace.Path, "Library");
        var configs = System.IO.Path.Combine(workspace.Path, "Configurations");
        WriteLibrary(library);
        var store = new FileAIConfigurationStore(library, configs);
        store.AddConfiguration(new UserConfiguration
        {
            Name = "Main",
            ProviderId = "openai",
            ModelId = "missing-model",
            CustomEndpoint = "https://custom.example.test/v1",
            ApiKey = "sk-test",
            IsActive = true
        });
        var handler = new CapturingHandler(HttpStatusCode.Unauthorized, """
        { "error": { "message": "invalid api key" } }
        """);
        using var httpClient = new HttpClient(handler);
        var service = new ConfiguredAITextGenerationService(store, () => httpClient);

        PromptGenerationAiResult result = await service.GenerateAsync("写一章");

        Assert.False(result.Success);
        Assert.Equal("invalid api key", result.ErrorMessage);
        Assert.Equal("https://custom.example.test/v1/chat/completions", handler.Request!.RequestUri!.ToString());
        using var document = JsonDocument.Parse(handler.Body);
        Assert.Equal("missing-model", document.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task GenerateAsync_records_usage_statistics_on_success()
    {
        using var workspace = new TempDirectory();
        var library = System.IO.Path.Combine(workspace.Path, "Library");
        var configs = System.IO.Path.Combine(workspace.Path, "Configurations");
        WriteLibrary(library);
        var store = new FileAIConfigurationStore(library, configs);
        store.AddConfiguration(new UserConfiguration
        {
            Name = "Main",
            ProviderId = "openai",
            ModelId = "gpt-id",
            ApiKey = "sk-test",
            IsActive = true
        });
        var handler = new CapturingHandler(HttpStatusCode.OK, """
        {
          "choices": [ { "message": { "content": "生成结果" } } ],
          "usage": { "prompt_tokens": 5, "completion_tokens": 7, "total_tokens": 12 }
        }
        """);
        using var httpClient = new HttpClient(handler);
        var statistics = new FileUsageStatisticsService(System.IO.Path.Combine(workspace.Path, "api_statistics.json"));
        var service = new ConfiguredAITextGenerationService(store, () => httpClient, usageStatistics: statistics);

        PromptGenerationAiResult result = await service.GenerateAsync("写一章");

        Assert.True(result.Success);
        var record = Assert.Single(statistics.GetAllRecords());
        Assert.Equal("gpt-real", record.ModelName);
        Assert.Equal("openai", record.Provider);
        Assert.True(record.Success);
        Assert.Equal(5, record.InputTokens);
        Assert.Equal(7, record.OutputTokens);
        Assert.True(record.ResponseTimeMs >= 0);
    }

    [Fact]
    public async Task GenerateAsync_records_usage_statistics_on_failure()
    {
        using var workspace = new TempDirectory();
        var library = System.IO.Path.Combine(workspace.Path, "Library");
        var configs = System.IO.Path.Combine(workspace.Path, "Configurations");
        WriteLibrary(library);
        var store = new FileAIConfigurationStore(library, configs);
        store.AddConfiguration(new UserConfiguration
        {
            Name = "Main",
            ProviderId = "openai",
            ModelId = "gpt-id",
            ApiKey = "sk-test",
            IsActive = true
        });
        var handler = new CapturingHandler(HttpStatusCode.InternalServerError, """
        { "error": { "message": "server exploded" } }
        """);
        using var httpClient = new HttpClient(handler);
        var statistics = new FileUsageStatisticsService(System.IO.Path.Combine(workspace.Path, "api_statistics.json"));
        var service = new ConfiguredAITextGenerationService(store, () => httpClient, usageStatistics: statistics);

        PromptGenerationAiResult result = await service.GenerateAsync("写一章");

        Assert.False(result.Success);
        var record = Assert.Single(statistics.GetAllRecords());
        Assert.Equal("gpt-real", record.ModelName);
        Assert.Equal("openai", record.Provider);
        Assert.False(record.Success);
        Assert.Equal("server exploded", record.ErrorMessage);
        Assert.True(record.ResponseTimeMs >= 0);
    }

    [Fact]
    public async Task GenerateAsync_uses_rotation_key_and_reports_success()
    {
        using var workspace = new TempDirectory();
        var library = System.IO.Path.Combine(workspace.Path, "Library");
        var configs = System.IO.Path.Combine(workspace.Path, "Configurations");
        WriteLibrary(library);
        var store = new FileAIConfigurationStore(library, configs);
        store.AddConfiguration(new UserConfiguration
        {
            Name = "Main",
            ProviderId = "openai",
            ModelId = "gpt-id",
            ApiKey = "config-key",
            IsActive = true
        });
        var rotation = new ApiKeyRotationService();
        rotation.UpdateKeyPool("openai",
        [
            new ApiKeyEntry { Id = "K1", Key = "key-1" },
            new ApiKeyEntry { Id = "K2", Key = "key-2" }
        ]);
        var handler = new SequenceHandler(
            new ResponseSpec(HttpStatusCode.OK, """{ "choices": [ { "message": { "content": "ok" } } ] }"""));
        using var httpClient = new HttpClient(handler);
        var service = new ConfiguredAITextGenerationService(store, () => httpClient, rotation);

        PromptGenerationAiResult result = await service.GenerateAsync("写一章");

        Assert.True(result.Success);
        Assert.Equal("key-2", handler.Requests[0].Headers.Authorization!.Parameter);
        var status = rotation.GetPoolStatus("openai");
        var used = Assert.Single(status!.Entries, entry => entry.KeyId == "K2");
        Assert.Equal(1, used.TotalRequests);
        Assert.Equal(0, used.TotalFailures);
    }

    [Fact]
    public async Task GenerateAsync_retries_next_rotation_key_after_rate_limit()
    {
        using var workspace = new TempDirectory();
        var library = System.IO.Path.Combine(workspace.Path, "Library");
        var configs = System.IO.Path.Combine(workspace.Path, "Configurations");
        WriteLibrary(library);
        var store = new FileAIConfigurationStore(library, configs);
        store.AddConfiguration(new UserConfiguration
        {
            Name = "Main",
            ProviderId = "openai",
            ModelId = "gpt-id",
            ApiKey = "config-key",
            IsActive = true
        });
        var rotation = new ApiKeyRotationService();
        rotation.UpdateKeyPool("openai",
        [
            new ApiKeyEntry { Id = "K1", Key = "key-1" },
            new ApiKeyEntry { Id = "K2", Key = "key-2" }
        ]);
        var handler = new SequenceHandler(
            new ResponseSpec(HttpStatusCode.TooManyRequests, """{ "error": { "message": "rate limited" } }"""),
            new ResponseSpec(HttpStatusCode.OK, """{ "choices": [ { "message": { "content": "retry ok" } } ] }"""));
        using var httpClient = new HttpClient(handler);
        var service = new ConfiguredAITextGenerationService(store, () => httpClient, rotation);

        PromptGenerationAiResult result = await service.GenerateAsync("写一章");

        Assert.True(result.Success);
        Assert.Equal("retry ok", result.Content);
        Assert.Equal(["key-2", "key-1"], handler.Requests.Select(request => request.Headers.Authorization!.Parameter!).ToArray());
        var status = rotation.GetPoolStatus("openai");
        Assert.Equal(KeyEntryStatus.TemporarilyDisabled, Assert.Single(status!.Entries, entry => entry.KeyId == "K2").Status);
        Assert.Equal(1, Assert.Single(status.Entries, entry => entry.KeyId == "K1").TotalRequests);
    }


    [Fact]
    public async Task StreamAsync_uses_active_configuration_and_yields_chunks()
    {
        using var workspace = new TempDirectory();
        var library = System.IO.Path.Combine(workspace.Path, "Library");
        var configs = System.IO.Path.Combine(workspace.Path, "Configurations");
        WriteLibrary(library);
        var store = new FileAIConfigurationStore(library, configs);
        store.AddConfiguration(new UserConfiguration
        {
            Name = "Main",
            ProviderId = "openai",
            ModelId = "gpt-id",
            ApiKey = "sk-test",
            DeveloperMessage = "你是小说助手",
            IsActive = true
        });
        var handler = new CapturingHandler(HttpStatusCode.OK, """
        data: {"choices":[{"delta":{"content":"你"}}]}

        data: {"choices":[{"delta":{"content":"好"}}]}

        data: [DONE]

        """, "text/event-stream");
        using var httpClient = new HttpClient(handler);
        var service = new ConfiguredAITextGenerationService(store, () => httpClient);

        var chunks = new List<OpenAICompatibleStreamChunk>();
        await foreach (var chunk in service.StreamAsync("写一章"))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(["你", "好"], chunks.Select(chunk => chunk.Content).ToArray());
        using var document = JsonDocument.Parse(handler.Body);
        var root = document.RootElement;
        Assert.True(root.GetProperty("stream").GetBoolean());
        Assert.Equal("gpt-real", root.GetProperty("model").GetString());
        Assert.Equal("你是小说助手", root.GetProperty("messages")[0].GetProperty("content").GetString());
        Assert.Equal("写一章", root.GetProperty("messages")[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task StreamAsync_uses_rotation_key_and_reports_success()
    {
        using var workspace = new TempDirectory();
        var library = System.IO.Path.Combine(workspace.Path, "Library");
        var configs = System.IO.Path.Combine(workspace.Path, "Configurations");
        WriteLibrary(library);
        var store = new FileAIConfigurationStore(library, configs);
        store.AddConfiguration(new UserConfiguration
        {
            Name = "Main",
            ProviderId = "openai",
            ModelId = "gpt-id",
            ApiKey = "config-key",
            IsActive = true
        });
        var rotation = new ApiKeyRotationService();
        rotation.UpdateKeyPool("openai",
        [
            new ApiKeyEntry { Id = "K1", Key = "key-1" },
            new ApiKeyEntry { Id = "K2", Key = "key-2" }
        ]);
        var handler = new SequenceHandler(
            new ResponseSpec(HttpStatusCode.OK, """
            data: {"choices":[{"delta":{"content":"你"}}]}

            data: [DONE]

            """, "text/event-stream"));
        using var httpClient = new HttpClient(handler);
        var service = new ConfiguredAITextGenerationService(store, () => httpClient, rotation);

        var chunks = new List<OpenAICompatibleStreamChunk>();
        await foreach (var chunk in service.StreamAsync("写一章"))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(["你"], chunks.Select(chunk => chunk.Content).ToArray());
        Assert.Equal("key-2", handler.Requests[0].Headers.Authorization!.Parameter);
        var status = rotation.GetPoolStatus("openai");
        var used = Assert.Single(status!.Entries, entry => entry.KeyId == "K2");
        Assert.Equal(1, used.TotalRequests);
        Assert.Equal(0, used.TotalFailures);
    }

    [Fact]
    public async Task StreamAsync_retries_next_rotation_key_after_rate_limit()
    {
        using var workspace = new TempDirectory();
        var library = System.IO.Path.Combine(workspace.Path, "Library");
        var configs = System.IO.Path.Combine(workspace.Path, "Configurations");
        WriteLibrary(library);
        var store = new FileAIConfigurationStore(library, configs);
        store.AddConfiguration(new UserConfiguration
        {
            Name = "Main",
            ProviderId = "openai",
            ModelId = "gpt-id",
            ApiKey = "config-key",
            IsActive = true
        });
        var rotation = new ApiKeyRotationService();
        rotation.UpdateKeyPool("openai",
        [
            new ApiKeyEntry { Id = "K1", Key = "key-1" },
            new ApiKeyEntry { Id = "K2", Key = "key-2" }
        ]);
        var handler = new SequenceHandler(
            new ResponseSpec(HttpStatusCode.TooManyRequests, """{ "error": { "message": "rate limited" } }"""),
            new ResponseSpec(HttpStatusCode.OK, """
            data: {"choices":[{"delta":{"content":"换"}}]}

            data: {"choices":[{"delta":{"content":"好了"}}]}

            data: [DONE]

            """, "text/event-stream"));
        using var httpClient = new HttpClient(handler);
        var service = new ConfiguredAITextGenerationService(store, () => httpClient, rotation);

        var chunks = new List<OpenAICompatibleStreamChunk>();
        await foreach (var chunk in service.StreamAsync("写一章"))
        {
            chunks.Add(chunk);
        }

        Assert.Equal(["换", "好了"], chunks.Select(chunk => chunk.Content).ToArray());
        Assert.Equal(["key-2", "key-1"], handler.Requests.Select(request => request.Headers.Authorization!.Parameter!).ToArray());
        var status = rotation.GetPoolStatus("openai");
        Assert.Equal(KeyEntryStatus.TemporarilyDisabled, Assert.Single(status!.Entries, entry => entry.KeyId == "K2").Status);
        Assert.Equal(1, Assert.Single(status.Entries, entry => entry.KeyId == "K1").TotalRequests);
    }

    [Fact]
    public async Task StreamAsync_records_usage_statistics_on_success()
    {
        using var workspace = new TempDirectory();
        var library = System.IO.Path.Combine(workspace.Path, "Library");
        var configs = System.IO.Path.Combine(workspace.Path, "Configurations");
        WriteLibrary(library);
        var store = new FileAIConfigurationStore(library, configs);
        store.AddConfiguration(new UserConfiguration
        {
            Name = "Main",
            ProviderId = "openai",
            ModelId = "gpt-id",
            ApiKey = "sk-test",
            IsActive = true
        });
        var handler = new CapturingHandler(HttpStatusCode.OK, """
        data: {"choices":[{"delta":{"content":"你"}}]}

        data: [DONE]

        """, "text/event-stream");
        using var httpClient = new HttpClient(handler);
        var statistics = new FileUsageStatisticsService(System.IO.Path.Combine(workspace.Path, "api_statistics.json"));
        var service = new ConfiguredAITextGenerationService(store, () => httpClient, usageStatistics: statistics);

        await foreach (var _ in service.StreamAsync("写一章"))
        {
        }

        var record = Assert.Single(statistics.GetAllRecords());
        Assert.Equal("gpt-real", record.ModelName);
        Assert.Equal("openai", record.Provider);
        Assert.True(record.Success);
        Assert.Equal(0, record.InputTokens);
        Assert.Equal(0, record.OutputTokens);
        Assert.True(record.ResponseTimeMs >= 0);
    }

    [Fact]
    public async Task StreamAsync_records_usage_statistics_on_failure()
    {
        using var workspace = new TempDirectory();
        var library = System.IO.Path.Combine(workspace.Path, "Library");
        var configs = System.IO.Path.Combine(workspace.Path, "Configurations");
        WriteLibrary(library);
        var store = new FileAIConfigurationStore(library, configs);
        store.AddConfiguration(new UserConfiguration
        {
            Name = "Main",
            ProviderId = "openai",
            ModelId = "gpt-id",
            ApiKey = "sk-test",
            IsActive = true
        });
        var handler = new CapturingHandler(HttpStatusCode.Unauthorized, """
        { "error": { "message": "invalid api key" } }
        """);
        using var httpClient = new HttpClient(handler);
        var statistics = new FileUsageStatisticsService(System.IO.Path.Combine(workspace.Path, "api_statistics.json"));
        var service = new ConfiguredAITextGenerationService(store, () => httpClient, usageStatistics: statistics);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in service.StreamAsync("写一章"))
            {
            }
        });

        var record = Assert.Single(statistics.GetAllRecords());
        Assert.Equal("gpt-real", record.ModelName);
        Assert.Equal("openai", record.Provider);
        Assert.False(record.Success);
        Assert.Equal("invalid api key", record.ErrorMessage);
        Assert.True(record.ResponseTimeMs >= 0);
    }

    [Fact]
    public async Task StreamAsync_throws_configuration_error_when_no_active_model_exists()
    {
        using var workspace = new TempDirectory();
        var store = new FileAIConfigurationStore(
            System.IO.Path.Combine(workspace.Path, "Library"),
            System.IO.Path.Combine(workspace.Path, "Configurations"));
        using var httpClient = new HttpClient(new CapturingHandler(HttpStatusCode.OK, "{}"));
        var service = new ConfiguredAITextGenerationService(store, () => httpClient);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in service.StreamAsync("写一章"))
            {
            }
        });

        Assert.Equal("当前没有激活的AI模型，请前往“智能助手 > 模型管理”完成配置后重试。", ex.Message);
    }

    private static void WriteLibrary(string library)
    {
        Directory.CreateDirectory(library);
        File.WriteAllText(System.IO.Path.Combine(library, "providers.json"), """
        { "Providers": [
          { "Id": "openai", "Name": "OpenAI", "ApiEndpoint": "https://api.example.test", "Order": 1 }
        ] }
        """);
        File.WriteAllText(System.IO.Path.Combine(library, "models.json"), """
        { "Models": [
          { "Id": "gpt-id", "ProviderId": "openai", "Name": "gpt-real", "Order": 1 }
        ] }
        """);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = string.Empty;

        private readonly string _mediaType;

        public CapturingHandler(HttpStatusCode statusCode, string responseBody, string mediaType = "application/json")
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
            _mediaType = mediaType;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, _mediaType)
            };
        }
    }

    private sealed class SequenceHandler : HttpMessageHandler
    {
        private readonly Queue<ResponseSpec> _responses;

        public List<HttpRequestMessage> Requests { get; } = new();

        public SequenceHandler(params ResponseSpec[] responses)
        {
            _responses = new Queue<ResponseSpec>(responses);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(await CloneRequestAsync(request, cancellationToken));
            var response = _responses.Count == 0
                ? new ResponseSpec(HttpStatusCode.InternalServerError, "{}")
                : _responses.Dequeue();
            return new HttpResponseMessage(response.StatusCode)
            {
                Content = new StringContent(response.Body, Encoding.UTF8, response.MediaType)
            };
        }

        private static async Task<HttpRequestMessage> CloneRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var clone = new HttpRequestMessage(request.Method, request.RequestUri);
            foreach (var header in request.Headers)
                clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

            if (request.Content != null)
            {
                var body = await request.Content.ReadAsStringAsync(cancellationToken);
                clone.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            return clone;
        }
    }

    private sealed record ResponseSpec(HttpStatusCode StatusCode, string Body, string MediaType = "application/json");

    private sealed class InMemorySecretStore : IApiKeySecretStore
    {
        private readonly Dictionary<string, string> _secrets = new();

        public string? GetSecret(string configId)
        {
            return _secrets.TryGetValue(configId, out var secret) ? secret : null;
        }

        public void SaveSecret(string configId, string apiKey)
        {
            _secrets[configId] = apiKey;
        }

        public void DeleteSecret(string configId)
        {
            _secrets.Remove(configId);
        }
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tianming-configured-ai-{Guid.NewGuid():N}");

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
