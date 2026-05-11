using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace TM.Services.Framework.AI.SemanticKernel;

public sealed class HashingTextEmbedder : ITextEmbedder
{
    private readonly int _dimension;

    public HashingTextEmbedder(int dimension = 256)
    {
        if (dimension <= 0)
            throw new ArgumentOutOfRangeException(nameof(dimension), "向量维度必须大于 0");

        _dimension = dimension;
    }

    public float[] Embed(string text)
    {
        var vector = new float[_dimension];
        foreach (var token in Tokenize(text))
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
            var index = BitConverter.ToUInt32(bytes, 0) % (uint)_dimension;
            var sign = (bytes[4] & 1) == 0 ? 1f : -1f;
            vector[index] += sign;
        }

        Normalize(vector);
        return vector;
    }

    public double Similarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
            return 0;

        double dot = 0;
        double normA = 0;
        double normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denominator < 1e-10 ? 0 : dot / denominator;
    }

    public void Dispose()
    {
    }

    private static string[] Tokenize(string text)
    {
        return (text ?? string.Empty)
            .ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\r', '\n', '，', ',', '、', '。', '！', '？', '.', '!', '?', '：', ':' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(SplitMixedToken)
            .Where(token => token.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] SplitMixedToken(string token)
    {
        if (token.Any(char.IsWhiteSpace))
            return token.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        return token.Any(IsCjk)
            ? token.Select(ch => ch.ToString()).ToArray()
            : new[] { token };
    }

    private static bool IsCjk(char ch)
    {
        return ch >= '\u4e00' && ch <= '\u9fff';
    }

    private static void Normalize(float[] vector)
    {
        double norm = 0;
        for (var i = 0; i < vector.Length; i++)
            norm += vector[i] * vector[i];

        norm = Math.Sqrt(norm);
        if (norm < 1e-10)
            return;

        for (var i = 0; i < vector.Length; i++)
            vector[i] = (float)(vector[i] / norm);
    }
}
