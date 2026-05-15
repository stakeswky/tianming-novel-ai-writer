using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TM.Framework.Common.Helpers.AI;

namespace TM.Services.Framework.AI.Core;

public sealed record OpenAICompatibleChatMessage(string Role, string Content);

public sealed class OpenAICompatibleChatRequest
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public List<OpenAICompatibleChatMessage> Messages { get; set; } = new();
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }

    /// <summary>可选的工具声明列表（OpenAI function calling）。</summary>
    public List<OpenAICompatibleToolDefinition>? Tools { get; set; }
}

public sealed class OpenAICompatibleChatResult
{
    public bool Success { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public int? StatusCode { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
}

public sealed record OpenAICompatibleStreamChunk(
    string Content,
    string? FinishReason = null,
    IReadOnlyList<OpenAICompatibleToolCall>? ToolCalls = null,
    string? Role = null);

/// <summary>工具调用声明，用于 OpenAI /v1/chat/completions 请求的 tools 字段。</summary>
public sealed class OpenAICompatibleToolDefinition
{
    public string Type { get; set; } = "function";
    public OpenAICompatibleFunctionDefinition Function { get; set; } = new();
}

public sealed class OpenAICompatibleFunctionDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Parameters { get; set; } = string.Empty; // JSON string
}

/// <summary>从流式响应中解析出的 tool_call。</summary>
public sealed class OpenAICompatibleToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "function";
    public string FunctionName { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
}

public sealed class OpenAICompatibleChatException : InvalidOperationException
{
    public OpenAICompatibleChatException(string message, int? statusCode = null)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public int? StatusCode { get; }
}

public sealed class OpenAICompatiblePromptTextGeneratorOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public double? Temperature { get; set; }
    public int? MaxTokens { get; set; }
}

public sealed class OpenAICompatiblePromptTextGenerator : IPromptTextGenerator
{
    private readonly OpenAICompatibleChatClient _client;
    private readonly OpenAICompatiblePromptTextGeneratorOptions _options;

    public OpenAICompatiblePromptTextGenerator(
        HttpClient httpClient,
        OpenAICompatiblePromptTextGeneratorOptions options)
    {
        _client = new OpenAICompatibleChatClient(httpClient);
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<PromptGenerationAiResult> GenerateAsync(string prompt)
    {
        var messages = new List<OpenAICompatibleChatMessage>();
        if (!string.IsNullOrWhiteSpace(_options.SystemPrompt))
            messages.Add(new OpenAICompatibleChatMessage("system", _options.SystemPrompt));

        messages.Add(new OpenAICompatibleChatMessage("user", prompt));

        var result = await _client.CompleteAsync(new OpenAICompatibleChatRequest
        {
            BaseUrl = _options.BaseUrl,
            ApiKey = _options.ApiKey,
            Model = _options.Model,
            Messages = messages,
            Temperature = _options.Temperature,
            MaxTokens = _options.MaxTokens
        });

        return result.Success
            ? new PromptGenerationAiResult(true, result.Content)
            : new PromptGenerationAiResult(false, string.Empty, result.ErrorMessage);
    }
}

public sealed class OpenAICompatibleChatClient
{
    private const string ChromeUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
        "AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/132.0.0.0 Safari/537.36";

    private readonly HttpClient _httpClient;

    public OpenAICompatibleChatClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<OpenAICompatibleChatResult> CompleteAsync(
        OpenAICompatibleChatRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.BaseUrl))
            return Fail("端点地址为空");

        if (TryValidateBaseUrlScheme(request.BaseUrl, out var schemeError))
            return Fail(schemeError!);

        if (string.IsNullOrWhiteSpace(request.Model))
            return Fail("未指定测试模型");

        if (request.Messages.Count == 0 || request.Messages.All(m => string.IsNullOrWhiteSpace(m.Content)))
            return Fail("消息内容为空");

        using var httpRequest = BuildRequest(request);
        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var requestUri = httpRequest.RequestUri?.ToString() ?? "<unknown>";
                var baseMessage = ParseErrorMessage(body) ?? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                return new OpenAICompatibleChatResult
                {
                    Success = false,
                    StatusCode = (int)response.StatusCode,
                    ErrorMessage = $"{baseMessage} (请求 URL: {requestUri})"
                };
            }

            return ParseSuccessResponse(body);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Fail("连接超时，请检查网络或端点地址");
        }
        catch (HttpRequestException ex)
        {
            return Fail($"网络错误: {ex.Message}");
        }
        catch (JsonException)
        {
            return Fail("响应格式不符合 OpenAI 规范");
        }
    }

    public async IAsyncEnumerable<OpenAICompatibleStreamChunk> StreamAsync(
        OpenAICompatibleChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (string.IsNullOrWhiteSpace(request.BaseUrl))
            throw new OpenAICompatibleChatException("端点地址为空", 0);

        if (TryValidateBaseUrlScheme(request.BaseUrl, out var schemeError))
            throw new OpenAICompatibleChatException(schemeError!, 0);

        using var httpRequest = BuildRequest(request, stream: true);
        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            var requestUri = httpRequest.RequestUri?.ToString() ?? "<unknown>";
            var baseMessage = ParseErrorMessage(errorBody) ?? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
            throw new OpenAICompatibleChatException(
                $"{baseMessage} (请求 URL: {requestUri})",
                (int)response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                continue;

            var payload = trimmed["data:".Length..].Trim();
            if (payload == "[DONE]")
                yield break;

            if (TryParseStreamChunk(payload, out var chunk))
                yield return chunk;
        }
    }

    private static HttpRequestMessage BuildRequest(OpenAICompatibleChatRequest request, bool stream = false)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = StripModelNamePrefix(request.Model),
            ["messages"] = request.Messages
                .Where(m => !string.IsNullOrWhiteSpace(m.Content))
                .Select(m => new Dictionary<string, string>
                {
                    ["role"] = string.IsNullOrWhiteSpace(m.Role) ? "user" : m.Role,
                    ["content"] = m.Content
                })
                .ToList()
        };

        if (request.Temperature.HasValue)
            body["temperature"] = request.Temperature.Value;

        if (request.MaxTokens.HasValue)
            body["max_tokens"] = request.MaxTokens.Value;

        if (stream)
            body["stream"] = true;

        if (request.Tools is { Count: > 0 })
        {
            body["tools"] = request.Tools
                .Select(t => new Dictionary<string, object?>
                {
                    ["type"] = t.Type,
                    ["function"] = new Dictionary<string, string>
                    {
                        ["name"] = t.Function.Name,
                        ["description"] = t.Function.Description,
                        ["parameters"] = t.Function.Parameters
                    }
                })
                .ToList();
        }

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUri(request.BaseUrl))
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };

        ApplyStandardHeaders(httpRequest, request.ApiKey);
        return httpRequest;
    }

    private static OpenAICompatibleChatResult ParseSuccessResponse(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return Fail("响应格式不符合 OpenAI 规范");

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        if (!root.TryGetProperty("choices", out var choices)
            || choices.ValueKind != JsonValueKind.Array
            || choices.GetArrayLength() == 0)
        {
            return Fail("响应格式不符合 OpenAI 规范");
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var message)
            || !message.TryGetProperty("content", out var content)
            || content.ValueKind != JsonValueKind.String)
        {
            return Fail("响应格式不符合 OpenAI 规范");
        }

        var result = new OpenAICompatibleChatResult
        {
            Success = true,
            Content = content.GetString() ?? string.Empty
        };

        if (root.TryGetProperty("usage", out var usage) && usage.ValueKind == JsonValueKind.Object)
        {
            result.PromptTokens = TryGetInt(usage, "prompt_tokens");
            result.CompletionTokens = TryGetInt(usage, "completion_tokens");
            result.TotalTokens = TryGetInt(usage, "total_tokens");
        }

        return result;
    }

    private static int? TryGetInt(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var property) && property.TryGetInt32(out var value)
            ? value
            : null;
    }

    private static bool TryParseStreamChunk(string payload, out OpenAICompatibleStreamChunk chunk)
    {
        chunk = new OpenAICompatibleStreamChunk(string.Empty);
        if (string.IsNullOrWhiteSpace(payload))
            return false;

        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            if (!root.TryGetProperty("choices", out var choices)
                || choices.ValueKind != JsonValueKind.Array
                || choices.GetArrayLength() == 0)
            {
                return false;
            }

            var choice = choices[0];
            var finishReason = choice.TryGetProperty("finish_reason", out var finishReasonElement)
                && finishReasonElement.ValueKind == JsonValueKind.String
                    ? finishReasonElement.GetString()
                    : null;

            // Try to parse tool_calls from delta
            List<OpenAICompatibleToolCall>? toolCalls = null;
            if (choice.TryGetProperty("delta", out var delta))
            {
                if (delta.TryGetProperty("tool_calls", out var toolCallsElement)
                    && toolCallsElement.ValueKind == JsonValueKind.Array
                    && toolCallsElement.GetArrayLength() > 0)
                {
                    toolCalls = new List<OpenAICompatibleToolCall>();
                    foreach (var tc in toolCallsElement.EnumerateArray())
                    {
                        var call = new OpenAICompatibleToolCall();
                        if (tc.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                            call.Id = idEl.GetString() ?? string.Empty;
                        if (tc.TryGetProperty("type", out var typeEl) && typeEl.ValueKind == JsonValueKind.String)
                            call.Type = typeEl.GetString() ?? "function";
                        if (tc.TryGetProperty("function", out var funcEl))
                        {
                            if (funcEl.TryGetProperty("name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String)
                                call.FunctionName = nameEl.GetString() ?? string.Empty;
                            if (funcEl.TryGetProperty("arguments", out var argsEl) && argsEl.ValueKind == JsonValueKind.String)
                                call.Arguments = argsEl.GetString() ?? string.Empty;
                        }
                        toolCalls.Add(call);
                    }
                }

                // Also try to get role from delta
                string? role = null;
                if (delta.TryGetProperty("role", out var roleEl) && roleEl.ValueKind == JsonValueKind.String)
                    role = roleEl.GetString();

                // Parse content as before
                if (delta.TryGetProperty("content", out var content)
                    && content.ValueKind == JsonValueKind.String)
                {
                    var text = content.GetString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(text))
                    {
                        chunk = new OpenAICompatibleStreamChunk(text, finishReason, toolCalls, role);
                        return true;
                    }
                }

                // If we have tool_calls but no content, still yield the chunk
                if (toolCalls is { Count: > 0 })
                {
                    chunk = new OpenAICompatibleStreamChunk(string.Empty, finishReason, toolCalls, role);
                    return true;
                }
            }

            return false;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static Uri BuildChatCompletionsUri(string baseUrl)
    {
        var normalized = EnsureApiVersion(baseUrl).TrimEnd('/');
        if (normalized.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
            return new Uri(normalized);

        return new Uri($"{normalized}/chat/completions");
    }

    private static string EnsureApiVersion(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        var normalized = url.TrimEnd();
        if (Regex.IsMatch(normalized.TrimEnd('/'), @"/v\d+(/|$)", RegexOptions.IgnoreCase))
            return normalized;

        return normalized.TrimEnd('/') + "/v1";
    }

    /// <summary>
    /// 校验 BaseUrl 必须含 http/https scheme。返回 true 表示有错误，
    /// 错误信息通过 out 参数返回；返回 false 表示通过校验。
    /// 历史背景：Round 3 Lane B 复核发现 new Uri(无 scheme) 会抛 UriFormatException
    /// 没被 CompleteAsync catch 列表覆盖，错误冒泡到 UI 显示原始堆栈。
    /// </summary>
    private static bool TryValidateBaseUrlScheme(string baseUrl, out string? error)
    {
        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            error = $"端点地址必须以 http:// 或 https:// 开头：{baseUrl}";
            return true;
        }
        error = null;
        return false;
    }

    private static void ApplyStandardHeaders(HttpRequestMessage request, string apiKey)
    {
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
        }

        request.Headers.TryAddWithoutValidation("HTTP-Referer", "https://cherry-ai.com");
        request.Headers.TryAddWithoutValidation("X-Title", "Cherry Studio");
        request.Headers.TryAddWithoutValidation("User-Agent", ChromeUserAgent);
        request.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9,zh-CN;q=0.8,zh;q=0.7");
        request.Headers.TryAddWithoutValidation("sec-ch-ua", "\"Not A(Brand\";v=\"8\", \"Chromium\";v=\"132\", \"Google Chrome\";v=\"132\"");
        request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
    }

    private static string? ParseErrorMessage(string errorContent)
    {
        if (string.IsNullOrWhiteSpace(errorContent))
            return null;

        try
        {
            using var document = JsonDocument.Parse(errorContent);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var errorElement)
                && errorElement.TryGetProperty("message", out var messageElement)
                && messageElement.ValueKind == JsonValueKind.String)
            {
                return messageElement.GetString();
            }
        }
        catch (JsonException)
        {
            return null;
        }

        return null;
    }

    private static string StripModelNamePrefix(string modelName)
    {
        var normalized = modelName.Trim();
        while (normalized.StartsWith('['))
        {
            var closeBracket = normalized.IndexOf(']');
            if (closeBracket < 0 || closeBracket >= normalized.Length - 1)
                break;

            normalized = normalized[(closeBracket + 1)..];
        }

        if (normalized.EndsWith("-free", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[..^5];

        return normalized;
    }

    private static OpenAICompatibleChatResult Fail(string message)
    {
        return new OpenAICompatibleChatResult
        {
            Success = false,
            ErrorMessage = message
        };
    }
}
