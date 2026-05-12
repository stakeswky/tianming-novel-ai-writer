using System.IO;

namespace TM.Services.Framework.AI.SemanticKernel;

public sealed record EmbeddingSettings
{
    public static EmbeddingSettings Default { get; } = new();

    public string? ModelFilePath { get; init; }
    public string? VocabFilePath { get; init; }
    public int HashingDimension { get; init; } = 256;
    public int MaxSequenceLength { get; init; } = 512;
    public int OutputDimension { get; init; } = 512;

    public EmbeddingSettingsValidationResult Validate()
    {
        var hasModel = !string.IsNullOrWhiteSpace(ModelFilePath);
        var hasVocab = !string.IsNullOrWhiteSpace(VocabFilePath);

        if (!hasModel && !hasVocab)
            return EmbeddingSettingsValidationResult.Valid();

        if (hasModel && !hasVocab)
            return EmbeddingSettingsValidationResult.Invalid("配置了 ModelFilePath 但未配置 VocabFilePath。");

        if (!hasModel && hasVocab)
            return EmbeddingSettingsValidationResult.Invalid("配置了 VocabFilePath 但未配置 ModelFilePath。");

        if (!File.Exists(ModelFilePath))
            return EmbeddingSettingsValidationResult.Invalid($"ModelFilePath 指向的文件不存在：{ModelFilePath}");

        if (!File.Exists(VocabFilePath))
            return EmbeddingSettingsValidationResult.Invalid($"VocabFilePath 指向的文件不存在：{VocabFilePath}");

        return EmbeddingSettingsValidationResult.Valid();
    }
}

public sealed record EmbeddingSettingsValidationResult(bool IsValid, string ErrorMessage)
{
    public static EmbeddingSettingsValidationResult Valid() => new(true, string.Empty);
    public static EmbeddingSettingsValidationResult Invalid(string msg) => new(false, msg);
}
