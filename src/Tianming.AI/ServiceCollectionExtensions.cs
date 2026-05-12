using Microsoft.Extensions.DependencyInjection;
using TM.Services.Framework.AI.Core;
using TM.Services.Framework.AI.SemanticKernel;

namespace TM.Services.Framework.AI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAIServices(this IServiceCollection s)
    {
        s.AddSingleton<IApiKeySecretStore, MacOSKeychainApiKeySecretStore>();
        s.AddSingleton<ITextEmbedder>(_ => OnnxEmbedderFactory.Create(EmbeddingSettings.Default));
        // M4+ 继续加 FileAIConfigurationStore、FilePromptTemplateStore 等
        return s;
    }
}
