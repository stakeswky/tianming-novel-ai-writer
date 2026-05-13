using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Framework.AI.SemanticKernel;
using Tianming.Desktop.Avalonia.Shell;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class OnnxHealthProbe : IOnnxHealthProbe
{
    private readonly EmbeddingSettings _settings;

    public OnnxHealthProbe(EmbeddingSettings settings) { _settings = settings; }

    public Task<StatusIndicator> ProbeAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_settings.ModelFilePath) ||
            string.IsNullOrWhiteSpace(_settings.VocabFilePath))
        {
            return Task.FromResult(new StatusIndicator(
                "ONNX", StatusKind.Info, "ONNX 模型未配置，向量化用 Hashing 降级"));
        }

        if (!File.Exists(_settings.ModelFilePath))
            return Task.FromResult(new StatusIndicator(
                "ONNX", StatusKind.Warning, $"模型文件不存在：{_settings.ModelFilePath}"));

        if (!File.Exists(_settings.VocabFilePath))
            return Task.FromResult(new StatusIndicator(
                "ONNX", StatusKind.Warning, $"词表文件不存在：{_settings.VocabFilePath}"));

        return Task.FromResult(new StatusIndicator(
            "ONNX", StatusKind.Success, $"模型 {Path.GetFileName(_settings.ModelFilePath)}"));
    }
}
