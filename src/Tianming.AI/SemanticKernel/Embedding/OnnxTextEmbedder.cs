using System;
using System.IO;
using System.Linq;
using FastBertTokenizer;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace TM.Services.Framework.AI.SemanticKernel;

public sealed class OnnxTextEmbedder : ITextEmbedder
{
    private readonly EmbeddingSettings _settings;
    private readonly Lazy<(InferenceSession Session, BertTokenizer Tokenizer)> _lazy;

    public OnnxTextEmbedder(EmbeddingSettings settings)
    {
        var v = settings.Validate();
        if (!v.IsValid || string.IsNullOrWhiteSpace(settings.ModelFilePath))
            throw new InvalidOperationException($"EmbeddingSettings 无效或缺失：{v.ErrorMessage}");

        _settings = settings;
        _lazy = new Lazy<(InferenceSession, BertTokenizer)>(() =>
        {
            var session = new InferenceSession(settings.ModelFilePath);
            var tokenizer = new BertTokenizer();
            using (var reader = File.OpenText(settings.VocabFilePath!))
                tokenizer.LoadVocabulary(reader, convertInputToLowercase: true);
            return (session, tokenizer);
        });
    }

    public float[] Embed(string text)
    {
        var (session, tokenizer) = _lazy.Value;
        var maxLen = _settings.MaxSequenceLength;

        // FastBertTokenizer 1.0.28: Encode(string, int maxTokens, int? padTo) returns
        // (Memory<long> InputIds, Memory<long> AttentionMask, Memory<long> TokenTypeIds)
        var encoded = tokenizer.Encode(text ?? string.Empty, maxLen);
        var inputIds = encoded.InputIds.ToArray();
        var attentionMask = encoded.AttentionMask.ToArray();
        var tokenTypeIds = encoded.TokenTypeIds.ToArray();

        var shape = new[] { 1, inputIds.Length };
        var inputs = new[]
        {
            NamedOnnxValue.CreateFromTensor("input_ids", new DenseTensor<long>(inputIds, shape)),
            NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(attentionMask, shape)),
            NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(tokenTypeIds, shape)),
        };

        using var outputs = session.Run(inputs);
        var hidden = outputs.First().AsTensor<float>();

        var seqLen = (int)hidden.Dimensions[1];
        var dim = (int)hidden.Dimensions[2];
        var pooled = new float[dim];
        var divisor = 0f;

        for (var t = 0; t < seqLen; t++)
        {
            if (attentionMask[t] == 0) continue;
            divisor += 1f;
            for (var d = 0; d < dim; d++)
                pooled[d] += hidden[0, t, d];
        }

        if (divisor > 0)
            for (var d = 0; d < dim; d++) pooled[d] /= divisor;

        var norm = (float)Math.Sqrt(pooled.Sum(x => x * x));
        if (norm > 1e-12f)
            for (var d = 0; d < dim; d++) pooled[d] /= norm;

        return pooled;
    }

    public double Similarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0.0;
        var s = 0.0;
        for (var i = 0; i < a.Length; i++) s += a[i] * b[i];
        return s;
    }

    public void Dispose()
    {
        if (_lazy.IsValueCreated) _lazy.Value.Session.Dispose();
    }
}
