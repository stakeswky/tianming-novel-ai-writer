using System;
using TM.Framework.Platform;
using Xunit;

namespace Tianming.Framework.Tests.Platform;

public class MacOSSystemProxyServiceTests
{
    [Fact]
    public void GetCurrent_ProxyOff_ReturnsDirect()
    {
        var runner = new FakeScutilRunner("""
            <dictionary> {
              HTTPEnable : 0
              HTTPSEnable : 0
            }
            """);
        var svc = new MacOSSystemProxyService(runner);

        var policy = svc.GetCurrent();

        Assert.False(policy.HasProxy);
    }

    [Fact]
    public void GetCurrent_ProxyOn_ReturnsHttpPolicy()
    {
        var runner = new FakeScutilRunner("""
            <dictionary> {
              HTTPEnable : 1
              HTTPPort : 7890
              HTTPProxy : 127.0.0.1
            }
            """);
        var svc = new MacOSSystemProxyService(runner);

        var policy = svc.GetCurrent();

        Assert.True(policy.HasProxy);
        Assert.Equal("http://127.0.0.1:7890/", policy.HttpProxy!.ToString());
    }

    [Fact]
    public void GetCurrent_RunnerThrows_ReturnsDirect_DoesNotPropagate()
    {
        var runner = new ThrowingScutilRunner();
        var svc = new MacOSSystemProxyService(runner);

        var policy = svc.GetCurrent();

        Assert.Same(ProxyPolicy.Direct, policy);
    }

    private sealed class FakeScutilRunner : IScutilCommandRunner
    {
        private readonly string _output;
        public FakeScutilRunner(string output) => _output = output;
        public string Run() => _output;
    }

    private sealed class ThrowingScutilRunner : IScutilCommandRunner
    {
        public string Run() => throw new InvalidOperationException("scutil simulated failure");
    }
}
