using System.IO;
using System.Threading.Tasks;
using TM.Services.Framework.AI.SemanticKernel;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class OnnxHealthProbeTests
{
    [Fact]
    public async Task ProbeAsync_NoModelConfigured_ReturnsInfoOptional()
    {
        var settings = EmbeddingSettings.Default; // ModelFilePath / VocabFilePath 都 null
        var probe = new OnnxHealthProbe(settings);
        var status = await probe.ProbeAsync();
        Assert.Equal(StatusKind.Info, status.Kind);
    }

    [Fact]
    public async Task ProbeAsync_ModelMissing_ReturnsWarning()
    {
        var settings = EmbeddingSettings.Default with
        {
            ModelFilePath = "/nonexistent/model.onnx",
            VocabFilePath = "/nonexistent/vocab.txt"
        };
        var probe = new OnnxHealthProbe(settings);
        var status = await probe.ProbeAsync();
        Assert.Equal(StatusKind.Warning, status.Kind);
    }

    [Fact]
    public async Task ProbeAsync_BothExist_ReturnsSuccess()
    {
        var tmp = Path.GetTempPath();
        var model = Path.Combine(tmp, $"probe-{System.Guid.NewGuid():N}.onnx");
        var vocab = Path.Combine(tmp, $"probe-{System.Guid.NewGuid():N}.txt");
        File.WriteAllText(model, "");
        File.WriteAllText(vocab, "");
        try
        {
            var settings = EmbeddingSettings.Default with { ModelFilePath = model, VocabFilePath = vocab };
            var probe = new OnnxHealthProbe(settings);
            var status = await probe.ProbeAsync();
            Assert.Equal(StatusKind.Success, status.Kind);
        }
        finally
        {
            File.Delete(model);
            File.Delete(vocab);
        }
    }
}
