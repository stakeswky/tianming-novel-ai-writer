using System.Threading;
using System.Threading.Tasks;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Shell;

public class AppStatusBarViewModelTests
{
    private sealed class FakeRuntime : IRuntimeInfoProvider
    {
        public string FrameworkDescription => ".NET 8.0.test";
        public bool IsLocalMode => true;
    }

    private sealed class FakeKeyProbe : IKeychainHealthProbe
    {
        private readonly StatusIndicator _r;
        public FakeKeyProbe(StatusIndicator r) { _r = r; }
        public Task<StatusIndicator> ProbeAsync(CancellationToken ct = default) => Task.FromResult(_r);
    }

    private sealed class FakeOnnxProbe : IOnnxHealthProbe
    {
        private readonly StatusIndicator _r;
        public FakeOnnxProbe(StatusIndicator r) { _r = r; }
        public Task<StatusIndicator> ProbeAsync(CancellationToken ct = default) => Task.FromResult(_r);
    }

    [Fact]
    public void Constructor_InitializesImmediateFields()
    {
        var vm = new AppStatusBarViewModel(
            new FakeRuntime(),
            new FakeKeyProbe(new StatusIndicator("X", StatusKind.Success)),
            new FakeOnnxProbe(new StatusIndicator("Y", StatusKind.Info)));
        Assert.Equal(".NET 8.0.test", vm.DotNetRuntime);
        Assert.Equal(StatusKind.Success, vm.LocalMode.Kind);
        Assert.Equal(StatusKind.Neutral, vm.KeychainStatus.Kind);
        Assert.Equal(StatusKind.Neutral, vm.OnnxStatus.Kind);
    }

    [Fact]
    public async Task RefreshProbes_PopulatesStatuses()
    {
        var vm = new AppStatusBarViewModel(
            new FakeRuntime(),
            new FakeKeyProbe(new StatusIndicator("Keychain", StatusKind.Success)),
            new FakeOnnxProbe(new StatusIndicator("ONNX", StatusKind.Info)));
        await vm.RefreshProbesAsync();
        Assert.Equal(StatusKind.Success, vm.KeychainStatus.Kind);
        Assert.Equal(StatusKind.Info, vm.OnnxStatus.Kind);
    }

    [Fact]
    public async Task RefreshProbes_ProbeThrows_SetDanger()
    {
        var vm = new AppStatusBarViewModel(
            new FakeRuntime(),
            new ThrowingProbe(),
            new FakeOnnxProbe(new StatusIndicator("ONNX", StatusKind.Info)));
        await vm.RefreshProbesAsync();
        Assert.Equal(StatusKind.Danger, vm.KeychainStatus.Kind);
    }

    private sealed class ThrowingProbe : IKeychainHealthProbe
    {
        public Task<StatusIndicator> ProbeAsync(CancellationToken ct = default)
            => throw new System.InvalidOperationException("boom");
    }
}
