using System;
using System.IO;
using TM.Services.Framework.AI.SemanticKernel;
using Xunit;

namespace Tianming.AI.Tests;

public class EmbeddingSettingsTests
{
    [Fact]
    public void Default_ShouldUseHashingFallback()
    {
        var s = EmbeddingSettings.Default;
        Assert.Null(s.ModelFilePath);
        Assert.Null(s.VocabFilePath);
        Assert.Equal(256, s.HashingDimension);
        Assert.Equal(512, s.MaxSequenceLength);
    }

    [Fact]
    public void Validate_WithModelButNoVocab_ReturnsError()
    {
        var s = EmbeddingSettings.Default with { ModelFilePath = "/tmp/x.onnx" };
        var result = s.Validate();
        Assert.False(result.IsValid);
        Assert.Contains("VocabFilePath", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithVocabButNoModel_ReturnsError()
    {
        var s = EmbeddingSettings.Default with { VocabFilePath = "/tmp/vocab.txt" };
        var result = s.Validate();
        Assert.False(result.IsValid);
        Assert.Contains("ModelFilePath", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithNonExistingFiles_ReturnsError()
    {
        var s = EmbeddingSettings.Default with
        {
            ModelFilePath = "/tmp/does-not-exist-12345.onnx",
            VocabFilePath = "/tmp/does-not-exist-12345.txt"
        };
        var result = s.Validate();
        Assert.False(result.IsValid);
        Assert.Contains("不存在", result.ErrorMessage);
    }

    [Fact]
    public void Validate_WithExistingFiles_ReturnsValid()
    {
        var model = Path.GetTempFileName();
        var vocab = Path.GetTempFileName();
        try
        {
            var s = EmbeddingSettings.Default with
            {
                ModelFilePath = model,
                VocabFilePath = vocab
            };
            var result = s.Validate();
            Assert.True(result.IsValid);
            Assert.Equal(string.Empty, result.ErrorMessage);
        }
        finally
        {
            File.Delete(model);
            File.Delete(vocab);
        }
    }

    [Fact]
    public void Validate_WithNeitherFile_ReturnsValid_BecauseFallbackIsUsed()
    {
        var result = EmbeddingSettings.Default.Validate();
        Assert.True(result.IsValid);
    }
}
