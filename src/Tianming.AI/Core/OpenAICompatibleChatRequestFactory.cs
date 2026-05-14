using System;
using System.Collections.Generic;

namespace TM.Services.Framework.AI.Core;

public static class OpenAICompatibleChatRequestFactory
{
    public static OpenAICompatibleChatRequest Build(
        FileAIConfigurationStore configurationStore,
        UserConfiguration configuration,
        List<OpenAICompatibleChatMessage> messages,
        List<OpenAICompatibleToolDefinition>? tools = null,
        int? overrideMaxTokens = null,
        double? overrideTemperature = null)
    {
        if (configurationStore == null)
            throw new ArgumentNullException(nameof(configurationStore));
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        if (messages == null)
            throw new ArgumentNullException(nameof(messages));

        var provider = configurationStore.GetProviderById(configuration.ProviderId);
        var endpoint = string.IsNullOrWhiteSpace(configuration.CustomEndpoint)
            ? provider?.ApiEndpoint
            : configuration.CustomEndpoint;
        var model = configurationStore.GetModelById(configuration.ModelId);
        var modelName = string.IsNullOrWhiteSpace(model?.Name)
            ? configuration.ModelId
            : model.Name;

        return new OpenAICompatibleChatRequest
        {
            BaseUrl = endpoint ?? string.Empty,
            ApiKey = configuration.ApiKey,
            Model = modelName,
            Messages = messages,
            Temperature = overrideTemperature ?? configuration.Temperature,
            MaxTokens = overrideMaxTokens ?? configuration.MaxTokens,
            Tools = tools,
        };
    }
}
