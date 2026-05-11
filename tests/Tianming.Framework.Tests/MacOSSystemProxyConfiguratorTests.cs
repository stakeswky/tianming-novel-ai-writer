using TM.Framework.Proxy;
using Xunit;

namespace Tianming.Framework.Tests;

public class MacOSSystemProxyConfiguratorTests
{
    [Fact]
    public async Task ApplyAsync_sets_http_and_https_proxy_with_bypass_domains()
    {
        var runner = new RecordingSystemProxyCommandRunner();
        var configurator = new MacOSSystemProxyConfigurator(runner);

        var result = await configurator.ApplyAsync(
            "Wi-Fi",
            new PortableProxyConfig
            {
                Type = PortableProxyType.Http,
                Server = "127.0.0.1",
                Port = 7890,
                BypassList = ["<local>", "*.internal.test", "api.direct.test"]
            });

        Assert.True(result.Success);
        Assert.Equal([
            Invocation("-setwebproxy", "Wi-Fi", "127.0.0.1", "7890"),
            Invocation("-setsecurewebproxy", "Wi-Fi", "127.0.0.1", "7890"),
            Invocation("-setproxybypassdomains", "Wi-Fi", "<local>", "*.internal.test", "api.direct.test")
        ], runner.Invocations);
    }

    [Fact]
    public async Task ApplyAsync_sets_socks_proxy_for_socks_config()
    {
        var runner = new RecordingSystemProxyCommandRunner();
        var configurator = new MacOSSystemProxyConfigurator(runner);

        await configurator.ApplyAsync(
            "USB 10/100/1000 LAN",
            new PortableProxyConfig
            {
                Type = PortableProxyType.Socks5,
                Server = "proxy.local",
                Port = 1080
            });

        Assert.Equal([
            Invocation("-setsocksfirewallproxy", "USB 10/100/1000 LAN", "proxy.local", "1080")
        ], runner.Invocations);
    }

    [Fact]
    public async Task ApplyPacAsync_sets_autoproxy_url_and_state()
    {
        var runner = new RecordingSystemProxyCommandRunner();
        var configurator = new MacOSSystemProxyConfigurator(runner);

        var result = await configurator.ApplyPacAsync("Wi-Fi", new Uri("https://proxy.example/proxy.pac"));

        Assert.True(result.Success);
        Assert.Equal([
            Invocation("-setautoproxyurl", "Wi-Fi", "https://proxy.example/proxy.pac"),
            Invocation("-setautoproxystate", "Wi-Fi", "on")
        ], runner.Invocations);
    }

    [Fact]
    public async Task DisableAsync_turns_off_supported_proxy_modes()
    {
        var runner = new RecordingSystemProxyCommandRunner();
        var configurator = new MacOSSystemProxyConfigurator(runner);

        var result = await configurator.DisableAsync("Wi-Fi");

        Assert.True(result.Success);
        Assert.Equal([
            Invocation("-setwebproxystate", "Wi-Fi", "off"),
            Invocation("-setsecurewebproxystate", "Wi-Fi", "off"),
            Invocation("-setsocksfirewallproxystate", "Wi-Fi", "off"),
            Invocation("-setautoproxystate", "Wi-Fi", "off")
        ], runner.Invocations);
    }

    private static MacOSSystemProxyCommandInvocation Invocation(params string[] arguments)
    {
        return new MacOSSystemProxyCommandInvocation("/usr/sbin/networksetup", arguments);
    }

    private sealed class RecordingSystemProxyCommandRunner : IMacOSSystemProxyCommandRunner
    {
        public List<MacOSSystemProxyCommandInvocation> Invocations { get; } = [];

        public Task<MacOSSystemProxyCommandResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            CancellationToken cancellationToken = default)
        {
            Invocations.Add(new MacOSSystemProxyCommandInvocation(fileName, arguments.ToArray()));
            return Task.FromResult(new MacOSSystemProxyCommandResult(0, string.Empty, string.Empty));
        }
    }
}
