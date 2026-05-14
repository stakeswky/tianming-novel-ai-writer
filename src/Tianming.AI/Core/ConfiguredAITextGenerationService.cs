using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TM.Framework.Common.Helpers.AI;
using TM.Services.Framework.AI.Core.Routing;
using TM.Services.Framework.AI.Monitoring;

namespace TM.Services.Framework.AI.Core;

public sealed class ConfiguredAITextGenerationService : IPromptTextGenerator
{
    private const string NoActiveConfigurationMessage = "当前没有激活的AI模型，请前往“智能助手 > 模型管理”完成配置后重试。";

    private readonly FileAIConfigurationStore _configurationStore;
    private readonly Func<HttpClient> _httpClientFactory;
    private readonly ApiKeyRotationService? _keyRotation;
    private readonly FileUsageStatisticsService? _usageStatistics;
    private readonly IAIModelRouter? _router;
    private readonly AITaskPurpose _defaultPurpose;

    public ConfiguredAITextGenerationService(
        FileAIConfigurationStore configurationStore,
        Func<HttpClient> httpClientFactory,
        ApiKeyRotationService? keyRotation = null,
        FileUsageStatisticsService? usageStatistics = null,
        IAIModelRouter? router = null,
        AITaskPurpose defaultPurpose = AITaskPurpose.Writing)
    {
        _configurationStore = configurationStore ?? throw new ArgumentNullException(nameof(configurationStore));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _keyRotation = keyRotation;
        _usageStatistics = usageStatistics;
        _router = router;
        _defaultPurpose = defaultPurpose;
    }

    public Task<PromptGenerationAiResult> GenerateAsync(string prompt)
    {
        return GenerateAsync(prompt, _defaultPurpose);
    }

    public async Task<PromptGenerationAiResult> GenerateAsync(string prompt, AITaskPurpose purpose)
    {
        if (!TryBuildRequest(prompt, purpose, out var context, out var errorMessage))
            return new PromptGenerationAiResult(false, string.Empty, errorMessage);

        var excludedKeyIds = new HashSet<string>();
        OpenAICompatibleChatResult? lastResult = null;
        while (true)
        {
            var selection = _keyRotation?.GetNextKey(context.ProviderId, excludedKeyIds);
            if (selection == null && excludedKeyIds.Count > 0)
                break;

            var request = context.Request;
            if (selection != null)
                request.ApiKey = selection.ApiKey;

            var httpClient = _httpClientFactory();
            var client = new OpenAICompatibleChatClient(httpClient);
            var stopwatch = Stopwatch.StartNew();
            var result = await client.CompleteAsync(request);
            stopwatch.Stop();
            RecordUsage(context, result, stopwatch.ElapsedMilliseconds);
            lastResult = result;

            if (selection == null)
            {
                return result.Success
                    ? new PromptGenerationAiResult(true, result.Content)
                    : new PromptGenerationAiResult(false, string.Empty, result.ErrorMessage);
            }

            if (result.Success)
            {
                _keyRotation?.ReportKeyResult(context.ProviderId, selection.KeyId, KeyUseResult.Success);
                return new PromptGenerationAiResult(true, result.Content);
            }

            var keyUseResult = MapKeyUseResult(result);
            _keyRotation?.ReportKeyResult(context.ProviderId, selection.KeyId, keyUseResult, result.ErrorMessage);
            excludedKeyIds.Add(selection.KeyId);

            if (!ShouldRetryWithAnotherKey(keyUseResult))
                break;
        }

        return new PromptGenerationAiResult(false, string.Empty, lastResult?.ErrorMessage ?? "AI生成失败");
    }

    public IAsyncEnumerable<OpenAICompatibleStreamChunk> StreamAsync(
        string prompt,
        CancellationToken cancellationToken = default)
    {
        return StreamAsync(prompt, _defaultPurpose, cancellationToken);
    }

    public async IAsyncEnumerable<OpenAICompatibleStreamChunk> StreamAsync(
        string prompt,
        AITaskPurpose purpose,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!TryBuildRequest(prompt, purpose, out var context, out var errorMessage))
            throw new InvalidOperationException(errorMessage);

        var excludedKeyIds = new HashSet<string>();
        Exception? lastError = null;
        while (true)
        {
            var selection = _keyRotation?.GetNextKey(context.ProviderId, excludedKeyIds);
            if (selection == null && excludedKeyIds.Count > 0)
                break;

            if (selection != null)
                context.Request.ApiKey = selection.ApiKey;

            var httpClient = _httpClientFactory();
            var client = new OpenAICompatibleChatClient(httpClient);
            var yieldedAny = false;
            var completed = false;
            var stopwatch = Stopwatch.StartNew();

            await using var enumerator = client.StreamAsync(context.Request, cancellationToken)
                .GetAsyncEnumerator(cancellationToken);
            while (true)
            {
                OpenAICompatibleStreamChunk chunk;
                try
                {
                    if (!await enumerator.MoveNextAsync())
                    {
                        completed = true;
                        break;
                    }

                    chunk = enumerator.Current;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    lastError = ex;
                    break;
                }

                yieldedAny = true;
                yield return chunk;
            }

            if (completed)
            {
                stopwatch.Stop();
                RecordUsage(context, success: true, stopwatch.ElapsedMilliseconds);
                if (selection != null)
                    _keyRotation?.ReportKeyResult(context.ProviderId, selection.KeyId, KeyUseResult.Success);
                yield break;
            }

            RecordUsage(context, success: false, stopwatch.ElapsedMilliseconds, errorMessage: lastError?.Message);

            if (selection == null || yieldedAny)
                break;

            var keyUseResult = MapKeyUseResult(lastError);
            _keyRotation?.ReportKeyResult(context.ProviderId, selection.KeyId, keyUseResult, lastError?.Message);
            excludedKeyIds.Add(selection.KeyId);

            if (!ShouldRetryWithAnotherKey(keyUseResult))
                break;
        }

        throw new InvalidOperationException(lastError?.Message ?? "AI生成失败", lastError);
    }

    private bool TryBuildRequest(
        string prompt,
        AITaskPurpose purpose,
        out GenerationRequestContext context,
        out string errorMessage)
    {
        context = new GenerationRequestContext(string.Empty, new OpenAICompatibleChatRequest());
        errorMessage = string.Empty;

        UserConfiguration configuration;
        if (_router != null)
        {
            try
            {
                configuration = _router.Resolve(purpose);
            }
            catch (InvalidOperationException ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }
        else
        {
            var activeConfig = _configurationStore.GetActiveConfiguration();
            if (activeConfig == null)
            {
                errorMessage = NoActiveConfigurationMessage;
                return false;
            }

            configuration = activeConfig;
        }

        var provider = _configurationStore.GetProviderById(configuration.ProviderId);
        if (provider == null && string.IsNullOrWhiteSpace(configuration.CustomEndpoint))
        {
            errorMessage = $"未找到供应商: {configuration.ProviderId}";
            return false;
        }

        var messages = new List<OpenAICompatibleChatMessage>();
        if (!string.IsNullOrWhiteSpace(configuration.DeveloperMessage))
            messages.Add(new OpenAICompatibleChatMessage("system", configuration.DeveloperMessage));

        messages.Add(new OpenAICompatibleChatMessage("user", prompt));
        var request = OpenAICompatibleChatRequestFactory.Build(_configurationStore, configuration, messages);
        if (string.IsNullOrWhiteSpace(request.BaseUrl))
        {
            errorMessage = "端点地址为空";
            return false;
        }

        context = new GenerationRequestContext(configuration.ProviderId, request);
        return true;
    }

    private static KeyUseResult MapKeyUseResult(OpenAICompatibleChatResult result)
    {
        return MapKeyUseResult(result.StatusCode);
    }

    private static KeyUseResult MapKeyUseResult(Exception? exception)
    {
        return exception switch
        {
            OpenAICompatibleChatException chatException => MapKeyUseResult(chatException.StatusCode),
            HttpRequestException => KeyUseResult.NetworkError,
            TaskCanceledException => KeyUseResult.NetworkError,
            _ => KeyUseResult.Unknown
        };
    }

    private static KeyUseResult MapKeyUseResult(int? statusCode)
    {
        return statusCode switch
        {
            401 => KeyUseResult.AuthFailure,
            403 => KeyUseResult.Forbidden,
            402 => KeyUseResult.QuotaExhausted,
            429 => KeyUseResult.RateLimited,
            >= 500 and <= 599 => KeyUseResult.ServerError,
            null => KeyUseResult.NetworkError,
            _ => KeyUseResult.Unknown
        };
    }

    private static bool ShouldRetryWithAnotherKey(KeyUseResult result)
    {
        return result is KeyUseResult.AuthFailure
            or KeyUseResult.Forbidden
            or KeyUseResult.RateLimited
            or KeyUseResult.QuotaExhausted
            or KeyUseResult.ServerError;
    }

    private void RecordUsage(
        GenerationRequestContext context,
        OpenAICompatibleChatResult result,
        long elapsedMilliseconds)
    {
        RecordUsage(
            context,
            result.Success,
            elapsedMilliseconds,
            result.PromptTokens ?? 0,
            result.CompletionTokens ?? 0,
            result.Success ? null : result.ErrorMessage);
    }

    private void RecordUsage(
        GenerationRequestContext context,
        bool success,
        long elapsedMilliseconds,
        int inputTokens = 0,
        int outputTokens = 0,
        string? errorMessage = null)
    {
        _usageStatistics?.RecordCall(
            context.Request.Model,
            context.ProviderId,
            success,
            (int)Math.Min(int.MaxValue, Math.Max(0, elapsedMilliseconds)),
            inputTokens,
            outputTokens,
            errorMessage);
    }

    private sealed record GenerationRequestContext(string ProviderId, OpenAICompatibleChatRequest Request);
}
