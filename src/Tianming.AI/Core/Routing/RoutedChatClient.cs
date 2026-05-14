using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Framework.AI.Core.Routing;

public sealed class RoutedChatClient
{
    private readonly OpenAICompatibleChatClient _inner;
    private readonly IAIModelRouter _router;

    public RoutedChatClient(OpenAICompatibleChatClient inner, IAIModelRouter router)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _router = router ?? throw new ArgumentNullException(nameof(router));
    }

    public OpenAICompatibleChatRequest BuildRequestFor(
        AITaskPurpose purpose,
        List<OpenAICompatibleChatMessage> messages,
        List<OpenAICompatibleToolDefinition>? tools = null,
        int? overrideMaxTokens = null,
        double? overrideTemperature = null)
    {
        var config = _router.Resolve(purpose);
        return new OpenAICompatibleChatRequest
        {
            BaseUrl = config.CustomEndpoint ?? string.Empty,
            ApiKey = config.ApiKey,
            Model = config.ModelId,
            Messages = messages,
            Temperature = overrideTemperature ?? config.Temperature,
            MaxTokens = overrideMaxTokens ?? config.MaxTokens,
            Tools = tools,
        };
    }

    public Task<OpenAICompatibleChatResult> CompleteAsync(
        AITaskPurpose purpose,
        List<OpenAICompatibleChatMessage> messages,
        CancellationToken cancellationToken = default)
    {
        var request = BuildRequestFor(purpose, messages);
        return _inner.CompleteAsync(request, cancellationToken);
    }

    public IAsyncEnumerable<OpenAICompatibleStreamChunk> StreamAsync(
        AITaskPurpose purpose,
        List<OpenAICompatibleChatMessage> messages,
        List<OpenAICompatibleToolDefinition>? tools = null,
        CancellationToken cancellationToken = default)
    {
        var request = BuildRequestFor(purpose, messages, tools);
        return _inner.StreamAsync(request, cancellationToken);
    }
}
