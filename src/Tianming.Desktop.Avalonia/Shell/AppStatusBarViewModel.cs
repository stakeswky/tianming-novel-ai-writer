using System;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Tianming.Desktop.Avalonia.Infrastructure;

namespace Tianming.Desktop.Avalonia.Shell;

public partial class AppStatusBarViewModel : ObservableObject
{
    private readonly IKeychainHealthProbe _keychainProbe;
    private readonly IOnnxHealthProbe _onnxProbe;

    [ObservableProperty] private string _dotNetRuntime;
    [ObservableProperty] private StatusIndicator _localMode;
    [ObservableProperty] private StatusIndicator _keychainStatus;
    [ObservableProperty] private StatusIndicator _onnxStatus;
    [ObservableProperty] private string? _currentProjectPath;

    public AppStatusBarViewModel(
        IRuntimeInfoProvider runtime,
        IKeychainHealthProbe keychainProbe,
        IOnnxHealthProbe onnxProbe)
    {
        _keychainProbe = keychainProbe;
        _onnxProbe = onnxProbe;

        _dotNetRuntime = runtime.FrameworkDescription;
        _localMode = new StatusIndicator("本地写作模式", StatusKind.Success);
        _keychainStatus = new StatusIndicator("Keychain", StatusKind.Neutral, "检测中…");
        _onnxStatus = new StatusIndicator("ONNX", StatusKind.Neutral, "检测中…");
    }

    /// <summary>由 App 启动时 fire-and-forget 调用。</summary>
    public async Task RefreshProbesAsync(CancellationToken ct = default)
    {
        try { KeychainStatus = await _keychainProbe.ProbeAsync(ct); }
        catch (Exception ex) { KeychainStatus = new StatusIndicator("Keychain", StatusKind.Danger, ex.Message); }

        try { OnnxStatus = await _onnxProbe.ProbeAsync(ct); }
        catch (Exception ex) { OnnxStatus = new StatusIndicator("ONNX", StatusKind.Danger, ex.Message); }
    }
}
