using System.Threading.Tasks;
using TM.Services.Framework.AI.Core;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class KeychainHealthProbeTests
{
    private sealed class FakeRunner_Ok : ISecurityCommandRunner
    {
        public SecurityCommandResult Run(string fileName, System.Collections.Generic.IReadOnlyList<string> arguments)
            => new(ExitCode: 44, StandardOutput: string.Empty, StandardError: "item not in keychain.");
        // exit 44 = not found 但 security tool 自身可用 → keychain 工作正常
    }

    private sealed class FakeRunner_NoTool : ISecurityCommandRunner
    {
        public SecurityCommandResult Run(string fileName, System.Collections.Generic.IReadOnlyList<string> arguments)
            => throw new System.IO.FileNotFoundException("/usr/bin/security not found");
    }

    [Fact]
    public async Task ProbeAsync_ToolAvailable_ReturnsSuccess()
    {
        var store = new MacOSKeychainApiKeySecretStore(new FakeRunner_Ok(), "tianming-test");
        var probe = new KeychainHealthProbe(store);
        var status = await probe.ProbeAsync();
        Assert.Equal(StatusKind.Success, status.Kind);
        Assert.Contains("Keychain", status.Label);
    }

    [Fact]
    public async Task ProbeAsync_ToolMissing_ReturnsDanger()
    {
        var store = new MacOSKeychainApiKeySecretStore(new FakeRunner_NoTool(), "tianming-test");
        var probe = new KeychainHealthProbe(store);
        var status = await probe.ProbeAsync();
        Assert.Equal(StatusKind.Danger, status.Kind);
    }
}
