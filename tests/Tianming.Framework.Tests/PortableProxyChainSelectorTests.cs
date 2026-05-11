using TM.Framework.Proxy;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableProxyChainSelectorTests
{
    [Fact]
    public void SelectActiveProxy_returns_null_when_active_chain_is_missing_or_disabled()
    {
        var settings = new PortableProxyChainSettings
        {
            ActiveChainId = "chain-1",
            Chains =
            [
                new PortableProxyChainConfig
                {
                    Id = "chain-1",
                    Enabled = false,
                    Nodes =
                    [
                        Node("node-1", order: 1, latency: 50, available: true)
                    ]
                }
            ]
        };

        Assert.Null(PortableProxyChainSelector.SelectActiveProxy(settings));
    }

    [Fact]
    public void SelectActiveProxy_uses_enabled_available_nodes_before_unavailable_nodes()
    {
        var settings = new PortableProxyChainSettings
        {
            ActiveChainId = "chain-1",
            Chains =
            [
                new PortableProxyChainConfig
                {
                    Id = "chain-1",
                    Strategy = PortableProxyChainStrategy.Sequential,
                    Enabled = true,
                    Nodes =
                    [
                        Node("first", order: 1, latency: 20, available: false),
                        Node("second", order: 2, latency: 300, available: true)
                    ]
                }
            ]
        };

        var selected = PortableProxyChainSelector.SelectActiveProxy(settings);

        Assert.NotNull(selected);
        Assert.Equal("second", selected!.NodeId);
        Assert.Equal("second.proxy", selected.Config.Server);
    }

    [Fact]
    public void SelectActiveProxy_falls_back_to_enabled_nodes_when_none_are_available()
    {
        var settings = new PortableProxyChainSettings
        {
            ActiveChainId = "chain-1",
            Chains =
            [
                new PortableProxyChainConfig
                {
                    Id = "chain-1",
                    Enabled = true,
                    Nodes =
                    [
                        Node("disabled", order: 1, latency: 10, available: true, enabled: false),
                        Node("first", order: 2, latency: 20, available: false),
                        Node("second", order: 3, latency: 30, available: false)
                    ]
                }
            ]
        };

        var selected = PortableProxyChainSelector.SelectActiveProxy(settings);

        Assert.NotNull(selected);
        Assert.Equal("first", selected!.NodeId);
    }

    [Fact]
    public void SelectActiveProxy_load_balance_prefers_lowest_latency_then_order()
    {
        var settings = new PortableProxyChainSettings
        {
            ActiveChainId = "chain-1",
            Chains =
            [
                new PortableProxyChainConfig
                {
                    Id = "chain-1",
                    Strategy = PortableProxyChainStrategy.LoadBalance,
                    Enabled = true,
                    Nodes =
                    [
                        Node("late", order: 1, latency: 120, available: true),
                        Node("tie-second", order: 2, latency: 80, available: true),
                        Node("tie-first", order: 1, latency: 80, available: true)
                    ]
                }
            ]
        };

        var selected = PortableProxyChainSelector.SelectActiveProxy(settings);

        Assert.NotNull(selected);
        Assert.Equal("tie-first", selected!.NodeId);
    }

    [Fact]
    public void SelectActiveProxy_returns_null_when_selected_proxy_type_is_not_supported()
    {
        var settings = new PortableProxyChainSettings
        {
            ActiveChainId = "chain-1",
            Chains =
            [
                new PortableProxyChainConfig
                {
                    Id = "chain-1",
                    Enabled = true,
                    Nodes =
                    [
                        new PortableProxyNode
                        {
                            Id = "socks",
                            Name = "socks",
                            Order = 1,
                            Latency = 20,
                            IsAvailable = true,
                            Enabled = true,
                            Config = new PortableProxyConfig
                            {
                                Type = PortableProxyType.Socks5,
                                Server = "socks.proxy",
                                Port = 1080
                            }
                        }
                    ]
                }
            ]
        };

        Assert.Null(PortableProxyChainSelector.SelectActiveProxy(settings));
    }

    private static PortableProxyNode Node(
        string id,
        int order,
        int latency,
        bool available,
        bool enabled = true)
    {
        return new PortableProxyNode
        {
            Id = id,
            Name = id,
            Order = order,
            Latency = latency,
            IsAvailable = available,
            Enabled = enabled,
            Config = new PortableProxyConfig
            {
                Type = PortableProxyType.Http,
                Server = id + ".proxy",
                Port = 8080
            }
        };
    }
}
