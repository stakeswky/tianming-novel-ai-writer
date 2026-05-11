using TM.Framework.Proxy;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableProxyChainHealthControllerTests
{
    [Fact]
    public async Task CheckActiveChainAsync_updates_node_availability_latency_and_history()
    {
        var tester = new RecordingProxyNodeHealthTester(
            ("primary-a", true, 42),
            ("primary-b", false, 900));
        var controller = new PortableProxyChainHealthController(tester, () => new DateTime(2026, 5, 10, 11, 0, 0));

        var result = await controller.CheckActiveChainAsync(Settings(activeChainId: "primary"));

        Assert.False(result.ActiveChainChanged);
        Assert.Equal("primary", result.Settings.ActiveChainId);
        var active = Assert.Single(result.Settings.Chains, chain => chain.Id == "primary");
        Assert.Equal([true, false], active.Nodes.Select(node => node.IsAvailable));
        Assert.Equal([42, 900], active.Nodes.Select(node => node.Latency));
        var history = Assert.Single(result.Settings.History);
        Assert.Equal("primary", history.ChainId);
        Assert.True(history.Success);
        Assert.Equal(2, history.TotalNodes);
        Assert.Equal(1, history.SuccessfulNodes);
        Assert.Equal(42, history.AverageLatency);
    }

    [Fact]
    public async Task CheckActiveChainAsync_skips_health_check_when_auto_failover_is_disabled()
    {
        var tester = new RecordingProxyNodeHealthTester(("primary-a", false, 700));
        var controller = new PortableProxyChainHealthController(tester, () => new DateTime(2026, 5, 10, 11, 5, 0));
        var settings = Settings(activeChainId: "primary", primaryAutoFailover: false);

        var result = await controller.CheckActiveChainAsync(settings);

        Assert.Same(settings, result.Settings);
        Assert.Equal(PortableProxyChainHealthStatus.Skipped, result.Status);
        Assert.Empty(tester.CheckedNodeIds);
    }

    [Fact]
    public async Task CheckActiveChainAsync_switches_active_chain_when_current_chain_has_no_available_nodes()
    {
        var tester = new RecordingProxyNodeHealthTester(
            ("primary-a", false, 700),
            ("primary-b", false, 800),
            ("backup-a", true, 120));
        var controller = new PortableProxyChainHealthController(tester, () => new DateTime(2026, 5, 10, 11, 10, 0));

        var result = await controller.CheckActiveChainAsync(Settings(activeChainId: "primary"));

        Assert.True(result.ActiveChainChanged);
        Assert.Equal("backup", result.Settings.ActiveChainId);
        Assert.Equal(PortableProxyChainHealthStatus.FailedOver, result.Status);
        Assert.Equal(["primary-a", "primary-b", "backup-a"], tester.CheckedNodeIds);
        Assert.Contains("backup", result.Message);
    }

    private static PortableProxyChainSettings Settings(
        string activeChainId,
        bool primaryAutoFailover = true)
    {
        return new PortableProxyChainSettings
        {
            ActiveChainId = activeChainId,
            Chains =
            [
                new PortableProxyChainConfig
                {
                    Id = "primary",
                    Name = "Primary",
                    Enabled = true,
                    AutoFailover = primaryAutoFailover,
                    Nodes =
                    [
                        Node("primary-a", order: 1, available: true, latency: 10),
                        Node("primary-b", order: 2, available: true, latency: 20)
                    ]
                },
                new PortableProxyChainConfig
                {
                    Id = "backup",
                    Name = "Backup",
                    Enabled = true,
                    AutoFailover = true,
                    Nodes =
                    [
                        Node("backup-a", order: 1, available: true, latency: 100)
                    ]
                }
            ]
        };
    }

    private static PortableProxyNode Node(string id, int order, bool available, int latency)
    {
        return new PortableProxyNode
        {
            Id = id,
            Name = id,
            Order = order,
            IsAvailable = available,
            Latency = latency,
            Config = new PortableProxyConfig
            {
                Type = PortableProxyType.Http,
                Server = id + ".proxy",
                Port = 8080
            }
        };
    }

    private sealed class RecordingProxyNodeHealthTester : IPortableProxyNodeHealthTester
    {
        private readonly Dictionary<string, PortableProxyNodeHealthResult> _results;

        public RecordingProxyNodeHealthTester(params (string NodeId, bool Available, int Latency)[] results)
        {
            _results = results.ToDictionary(
                result => result.NodeId,
                result => new PortableProxyNodeHealthResult(result.Available, result.Latency, string.Empty));
        }

        public List<string> CheckedNodeIds { get; } = [];

        public Task<PortableProxyNodeHealthResult> TestAsync(
            PortableProxyNode node,
            CancellationToken cancellationToken = default)
        {
            CheckedNodeIds.Add(node.Id);
            return Task.FromResult(_results[node.Id]);
        }
    }
}
