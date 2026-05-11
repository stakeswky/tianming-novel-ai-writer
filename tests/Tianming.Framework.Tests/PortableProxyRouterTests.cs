using TM.Framework.Proxy;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableProxyRouterTests
{
    [Fact]
    public void Resolve_routes_http_requests_through_configured_proxy_with_credentials()
    {
        var router = new PortableProxyRouter(new PortableProxyConfig
        {
            Type = PortableProxyType.Http,
            Server = "127.0.0.1",
            Port = 7890,
            RequiresAuth = true,
            Username = "alice",
            Password = "secret"
        });

        var decision = router.Resolve(new Uri("https://api.openai.com/v1/models"));

        Assert.Equal(PortableProxyDecisionKind.Proxy, decision.Kind);
        Assert.Equal(new Uri("http://127.0.0.1:7890/"), decision.ProxyUri);
        Assert.NotNull(decision.Credentials);
        Assert.Equal("alice", decision.Credentials!.UserName);
        Assert.Equal("secret", decision.Credentials.Password);
    }

    [Fact]
    public void Resolve_applies_rules_by_priority_before_default_proxy()
    {
        var router = new PortableProxyRouter(
            new PortableProxyConfig
            {
                Type = PortableProxyType.Http,
                Server = "proxy.local",
                Port = 8080
            },
            [
                new PortableProxyRule
                {
                    Type = PortableProxyRuleType.Wildcard,
                    Pattern = "*.example.com",
                    Action = PortableProxyAction.Proxy,
                    Priority = 2,
                    Enabled = true
                },
                new PortableProxyRule
                {
                    Type = PortableProxyRuleType.Domain,
                    Pattern = "api.example.com",
                    Action = PortableProxyAction.Direct,
                    Priority = 1,
                    Enabled = true
                }
            ]);

        var decision = router.Resolve(new Uri("https://api.example.com/chat"));

        Assert.Equal(PortableProxyDecisionKind.Direct, decision.Kind);
        Assert.Equal("Matched rule: api.example.com", decision.Reason);
    }

    [Fact]
    public void Resolve_blocks_requests_when_block_rule_matches()
    {
        var router = new PortableProxyRouter(
            new PortableProxyConfig { Type = PortableProxyType.Http, Server = "proxy.local", Port = 8080 },
            [
                new PortableProxyRule
                {
                    Type = PortableProxyRuleType.Regex,
                    Pattern = @"(^|\.)tracker\.",
                    Action = PortableProxyAction.Block,
                    Priority = 1,
                    Enabled = true
                }
            ]);

        var decision = router.Resolve(new Uri("https://cdn.tracker.example/pixel"));

        Assert.Equal(PortableProxyDecisionKind.Block, decision.Kind);
        Assert.Equal("Matched rule: (^|\\.)tracker\\.", decision.Reason);
    }

    [Fact]
    public void Resolve_bypasses_localhost_and_matching_bypass_entries()
    {
        var router = new PortableProxyRouter(new PortableProxyConfig
        {
            Type = PortableProxyType.Http,
            Server = "proxy.local",
            Port = 8080,
            BypassList = ["<local>", "*.internal.test", "api.direct.test"]
        });

        Assert.Equal(PortableProxyDecisionKind.Direct, router.Resolve(new Uri("http://localhost:5000")).Kind);
        Assert.Equal(PortableProxyDecisionKind.Direct, router.Resolve(new Uri("https://svc.internal.test")).Kind);
        Assert.Equal(PortableProxyDecisionKind.Direct, router.Resolve(new Uri("https://api.direct.test")).Kind);
        Assert.Equal(PortableProxyDecisionKind.Proxy, router.Resolve(new Uri("https://external.test")).Kind);
    }

    [Fact]
    public void Resolve_does_not_use_unsupported_proxy_protocols_for_http_clients()
    {
        var router = new PortableProxyRouter(new PortableProxyConfig
        {
            Type = PortableProxyType.Socks5,
            Server = "127.0.0.1",
            Port = 1080
        });

        var decision = router.Resolve(new Uri("https://api.example.com"));

        Assert.Equal(PortableProxyDecisionKind.Direct, decision.Kind);
        Assert.Contains("Unsupported proxy type", decision.Reason);
    }
}
