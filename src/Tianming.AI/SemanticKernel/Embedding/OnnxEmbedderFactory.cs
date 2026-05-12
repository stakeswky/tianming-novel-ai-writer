using System;

namespace TM.Services.Framework.AI.SemanticKernel;

public static class OnnxEmbedderFactory
{
    public static ITextEmbedder Create(EmbeddingSettings settings)
    {
        var v = settings.Validate();
        if (!v.IsValid || string.IsNullOrWhiteSpace(settings.ModelFilePath))
            return new HashingTextEmbedder(settings.HashingDimension);

        try
        {
            var onnx = new OnnxTextEmbedder(settings);
            // 触发懒加载，确保模型真能打开；若抛错立即捕获降级
            _ = onnx.Embed("");
            return onnx;
        }
        catch
        {
            return new HashingTextEmbedder(settings.HashingDimension);
        }
    }
}
