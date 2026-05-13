using System;
using System.Collections.Generic;
using Tianming.Desktop.Avalonia.Infrastructure;
using TM.Framework.Platform;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class AvaloniaSystemHttpProxyTests
{
    [Fact]
    public void GetProxy_NoSystemProxy_ReturnsNull()
    {
        var proxy = new AvaloniaSystemHttpProxy(new FakeSystemProxyService(ProxyPolicy.Direct));

        var result = proxy.GetProxy(new Uri("https://api.openai.com/v1"));

        Assert.Null(result);
    }

    [Fact]
    public void GetProxy_HttpsTarget_ReturnsHttpsProxy()
    {
        var policy = new ProxyPolicy(
            HttpProxy: new Uri("http://127.0.0.1:7890"),
            HttpsProxy: new Uri("http://127.0.0.1:7890"),
            Exceptions: Array.Empty<string>());
        var proxy = new AvaloniaSystemHttpProxy(new FakeSystemProxyService(policy));

        var result = proxy.GetProxy(new Uri("https://api.openai.com/v1"));

        Assert.Equal(new Uri("http://127.0.0.1:7890"), result);
    }

    [Fact]
    public void GetProxy_HostInExceptions_ReturnsNull()
    {
        var policy = new ProxyPolicy(
            HttpProxy: new Uri("http://127.0.0.1:7890"),
            HttpsProxy: new Uri("http://127.0.0.1:7890"),
            Exceptions: new[] { "localhost", "*.local" });
        var proxy = new AvaloniaSystemHttpProxy(new FakeSystemProxyService(policy));

        Assert.Null(proxy.GetProxy(new Uri("http://localhost:11434/api")));
        Assert.Null(proxy.GetProxy(new Uri("http://server.local/api")));
    }

    [Fact]
    public void IsBypassed_NoProxy_AlwaysTrue()
    {
        var proxy = new AvaloniaSystemHttpProxy(new FakeSystemProxyService(ProxyPolicy.Direct));
        Assert.True(proxy.IsBypassed(new Uri("https://example.com")));
    }

    private sealed class FakeSystemProxyService : IPortableSystemProxyService
    {
        private readonly ProxyPolicy _policy;
        public FakeSystemProxyService(ProxyPolicy policy) => _policy = policy;
        public ProxyPolicy GetCurrent() => _policy;
    }
}
