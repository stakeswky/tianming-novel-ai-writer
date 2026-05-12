using System;
using TM.Services.Framework.AI.SemanticKernel;

namespace Tianming.AI.Tests.TestSupport;

internal sealed class FakeTextEmbedder : ITextEmbedder
{
    public string Name { get; }
    public bool Disposed { get; private set; }

    public FakeTextEmbedder(string name) { Name = name; }

    public float[] Embed(string text)
        => new[] { (float)text.Length, (float)Name.GetHashCode() };

    public double Similarity(float[] a, float[] b) => 0.0;

    public void Dispose() { Disposed = true; }
}
