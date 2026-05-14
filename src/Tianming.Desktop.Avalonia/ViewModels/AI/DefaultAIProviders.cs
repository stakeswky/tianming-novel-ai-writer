namespace Tianming.Desktop.Avalonia.ViewModels.AI;

internal static class DefaultAIProviders
{
    public static IReadOnlyList<DefaultAIProviderOption> Options { get; } =
    [
        new("openai", "OpenAI"),
        new("anthropic", "Anthropic"),
        new("google", "Google Gemini"),
        new("azure-openai", "Azure OpenAI"),
        new("deepseek", "DeepSeek"),
        new("cherry-studio", "Cherry Studio"),
    ];
}

public sealed record DefaultAIProviderOption(string Id, string DisplayName)
{
    public string Name => DisplayName;
}
