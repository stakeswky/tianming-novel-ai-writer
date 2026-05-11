using System;

namespace TM.Services.Framework.AI.SemanticKernel;

public interface ITextEmbedder : IDisposable
{
    float[] Embed(string text);

    double Similarity(float[] a, float[] b);
}
