using System;
using System.IO;
using TM.Services.Framework.AI.SemanticKernel;
using Xunit;

namespace Tianming.AI.Tests;

public class OnnxEmbedderFactoryTests
{
    [Fact]
    public void Create_WithDefaultSettings_ReturnsHashingEmbedder()
    {
        var embedder = OnnxEmbedderFactory.Create(EmbeddingSettings.Default);
        Assert.IsType<HashingTextEmbedder>(embedder);
    }

    [Fact]
    public void Create_WithPartialConfig_FallsBackToHashing()
    {
        var s = EmbeddingSettings.Default with { ModelFilePath = "/tmp/only-model.onnx" };
        var embedder = OnnxEmbedderFactory.Create(s);
        Assert.IsType<HashingTextEmbedder>(embedder);
    }

    [Fact]
    public void Create_WithNonExistingModelFile_FallsBackToHashing()
    {
        var s = EmbeddingSettings.Default with
        {
            ModelFilePath = "/tmp/does-not-exist-98765.onnx",
            VocabFilePath = "/tmp/does-not-exist-98765.txt"
        };
        var embedder = OnnxEmbedderFactory.Create(s);
        Assert.IsType<HashingTextEmbedder>(embedder);
    }

    [Fact]
    public void Create_WithCorruptedModelFile_FallsBackToHashing()
    {
        var model = Path.GetTempFileName() + ".onnx";
        var vocab = Path.GetTempFileName();
        File.WriteAllText(model, "this is not a valid onnx file");
        File.WriteAllLines(vocab, new[] { "[PAD]", "[UNK]", "[CLS]", "[SEP]", "hello" });
        try
        {
            var s = EmbeddingSettings.Default with
            {
                ModelFilePath = model,
                VocabFilePath = vocab
            };
            var embedder = OnnxEmbedderFactory.Create(s);
            // 模型损坏时构造器应抛错被捕获，工厂回退到 Hashing
            Assert.IsType<HashingTextEmbedder>(embedder);
        }
        finally
        {
            File.Delete(model);
            File.Delete(vocab);
        }
    }

    [Fact]
    public void Create_RespectsCustomHashingDimension()
    {
        var s = EmbeddingSettings.Default with { HashingDimension = 128 };
        var embedder = OnnxEmbedderFactory.Create(s);
        var vec = embedder.Embed("hello world");
        Assert.Equal(128, vec.Length);
    }
}
