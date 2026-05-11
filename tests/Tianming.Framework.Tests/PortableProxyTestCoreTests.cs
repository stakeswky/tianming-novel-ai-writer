using TM.Framework.Proxy;
using Xunit;

namespace Tianming.Framework.Tests;

public class PortableProxyTestCoreTests
{
    [Fact]
    public void History_store_inserts_newest_first_trims_limit_and_reloads()
    {
        using var workspace = new TempDirectory();
        var historyFile = Path.Combine(workspace.Path, "test_history.json");
        var store = new PortableProxyTestHistoryStore(historyFile, maxRecords: 3);

        store.Add(new PortableProxyTestResult { ExitIP = "1.1.1.1", TestTime = new DateTime(2026, 5, 10, 8, 0, 0) });
        store.Add(new PortableProxyTestResult { ExitIP = "2.2.2.2", TestTime = new DateTime(2026, 5, 10, 8, 1, 0) });
        store.Add(new PortableProxyTestResult { ExitIP = "3.3.3.3", TestTime = new DateTime(2026, 5, 10, 8, 2, 0) });
        store.Add(new PortableProxyTestResult { ExitIP = "4.4.4.4", TestTime = new DateTime(2026, 5, 10, 8, 3, 0) });

        var reloaded = new PortableProxyTestHistoryStore(historyFile, maxRecords: 3);
        var history = reloaded.GetHistory();

        Assert.Equal(["4.4.4.4", "3.3.3.3", "2.2.2.2"], history.Select(item => item.ExitIP));
    }

    [Fact]
    public void History_store_recovers_from_invalid_json_and_overwrites_on_next_save()
    {
        using var workspace = new TempDirectory();
        var historyFile = Path.Combine(workspace.Path, "test_history.json");
        File.WriteAllText(historyFile, "{not-json");
        var store = new PortableProxyTestHistoryStore(historyFile);

        Assert.Empty(store.GetHistory());

        store.Add(new PortableProxyTestResult { IsConnected = true, ExitIP = "8.8.8.8" });

        var reloaded = new PortableProxyTestHistoryStore(historyFile);
        var result = Assert.Single(reloaded.GetHistory());
        Assert.Equal("8.8.8.8", result.ExitIP);
    }

    [Theory]
    [InlineData(true, "1.1.1.1", true, "2.2.2.2", true, "应用内代理已生效")]
    [InlineData(true, "1.1.1.1", true, "1.1.1.1", false, "直连IP与代理IP相同")]
    [InlineData(false, "", true, "2.2.2.2", true, "直连失败，但应用内代理可用")]
    [InlineData(true, "1.1.1.1", false, "", false, "直连可用")]
    [InlineData(false, "", false, "", false, "直连与代理均失败")]
    public void Verification_evaluator_matches_original_one_click_verdict(
        bool directSuccess,
        string directIp,
        bool proxySuccess,
        string proxyIp,
        bool expectedEffective,
        string expectedSummaryPrefix)
    {
        var result = PortableProxyVerificationEvaluator.Evaluate(
            new PortableProxyIpProbeResult(directSuccess, directIp, directSuccess ? "" : "direct failed"),
            new PortableProxyIpProbeResult(proxySuccess, proxyIp, proxySuccess ? "" : "proxy failed"),
            new DateTime(2026, 5, 10, 8, 30, 0));

        Assert.Equal(expectedEffective, result.IsProxyEffective);
        Assert.StartsWith(expectedSummaryPrefix, result.Summary);
        Assert.Equal(new DateTime(2026, 5, 10, 8, 30, 0), result.TestTime);
    }

    [Fact]
    public void Calculate_anonymity_score_applies_latency_dns_and_speed_penalties()
    {
        var result = new PortableProxyTestResult
        {
            IsConnected = true,
            Latency = 1200,
            DownloadSpeed = 0.05,
            DNSLeakDetected = true
        };

        var score = PortableProxyVerificationEvaluator.CalculateAnonymityScore(result);

        Assert.Equal(40, score);
    }

    [Fact]
    public async Task Network_probe_runs_connectivity_ip_location_and_speed_checks()
    {
        var http = new RecordingProxyProbeHttpClient(
            new PortableProxyHttpProbeResponse(true, "OK", 2, TimeSpan.FromMilliseconds(125), string.Empty),
            new PortableProxyHttpProbeResponse(true, """{"ip":"203.0.113.10"}""", 0, TimeSpan.FromMilliseconds(30), string.Empty),
            new PortableProxyHttpProbeResponse(true, """{"country_name":"Japan","city":"Tokyo"}""", 0, TimeSpan.FromMilliseconds(20), string.Empty),
            new PortableProxyHttpProbeResponse(true, string.Empty, 2 * 1024 * 1024, TimeSpan.FromSeconds(2), string.Empty),
            new PortableProxyHttpProbeResponse(true, "dns page", 0, TimeSpan.FromMilliseconds(40), string.Empty));
        var probe = new PortableProxyNetworkProbe(http, () => new DateTime(2026, 5, 10, 9, 0, 0));

        var result = await probe.TestAllAsync(new PortableProxyConfig
        {
            Type = PortableProxyType.Http,
            Server = "127.0.0.1",
            Port = 7890
        });

        Assert.True(result.IsConnected);
        Assert.Equal(125, result.Latency);
        Assert.Equal(1.0, result.DownloadSpeed);
        Assert.Equal("203.0.113.10", result.ExitIP);
        Assert.Equal("Japan Tokyo", result.Location);
        Assert.False(result.DNSLeakDetected);
        Assert.Equal(100, result.AnonymityScore);
        Assert.Empty(result.Issues);
        Assert.Equal(new DateTime(2026, 5, 10, 9, 0, 0), result.TestTime);
        Assert.Equal([true, true, false, true, true], http.Requests.Select(request => request.ProxyConfig is not null));
        Assert.Equal(
            [
                PortableProxyNetworkProbe.DefaultConnectivityUrl,
                PortableProxyNetworkProbe.DefaultIpCheckUrl,
                new Uri("https://ipapi.co/203.0.113.10/json/"),
                PortableProxyNetworkProbe.DefaultSpeedTestUrl,
                PortableProxyNetworkProbe.DefaultDnsLeakTestUrl
            ],
            http.Requests.Select(request => request.Url));
    }

    [Fact]
    public async Task Network_probe_short_circuits_full_test_when_connectivity_fails()
    {
        var http = new RecordingProxyProbeHttpClient(
            new PortableProxyHttpProbeResponse(false, string.Empty, 0, TimeSpan.FromMilliseconds(500), "timeout"));
        var probe = new PortableProxyNetworkProbe(http, () => new DateTime(2026, 5, 10, 9, 30, 0));

        var result = await probe.TestAllAsync(new PortableProxyConfig { Server = "proxy.local", Port = 8080 });

        Assert.False(result.IsConnected);
        Assert.Equal(500, result.Latency);
        Assert.Equal(0, result.AnonymityScore);
        Assert.Equal(["代理服务器无法连接"], result.Issues);
        Assert.Single(http.Requests);
    }

    [Fact]
    public async Task Network_probe_verifies_direct_and_proxied_public_ip()
    {
        var http = new RecordingProxyProbeHttpClient(
            new PortableProxyHttpProbeResponse(true, """{"ip":"198.51.100.1"}""", 0, TimeSpan.FromMilliseconds(20), string.Empty),
            new PortableProxyHttpProbeResponse(true, """{"ip":"203.0.113.10"}""", 0, TimeSpan.FromMilliseconds(25), string.Empty));
        var probe = new PortableProxyNetworkProbe(http, () => new DateTime(2026, 5, 10, 10, 0, 0));

        var result = await probe.VerifyApplicationProxyAsync(new PortableProxyConfig { Server = "proxy.local", Port = 8080 });

        Assert.True(result.DirectSuccess);
        Assert.Equal("198.51.100.1", result.DirectIP);
        Assert.True(result.ProxySuccess);
        Assert.Equal("203.0.113.10", result.ProxyIP);
        Assert.True(result.IsProxyEffective);
        Assert.StartsWith("应用内代理已生效", result.Summary);
        Assert.Null(http.Requests[0].ProxyConfig);
        Assert.NotNull(http.Requests[1].ProxyConfig);
    }

    private sealed class RecordingProxyProbeHttpClient : IPortableProxyProbeHttpClient
    {
        private readonly Queue<PortableProxyHttpProbeResponse> _responses;

        public RecordingProxyProbeHttpClient(params PortableProxyHttpProbeResponse[] responses)
        {
            _responses = new Queue<PortableProxyHttpProbeResponse>(responses);
        }

        public List<PortableProxyHttpProbeRequest> Requests { get; } = [];

        public Task<PortableProxyHttpProbeResponse> GetAsync(
            Uri url,
            PortableProxyConfig? proxyConfig,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(new PortableProxyHttpProbeRequest(url, proxyConfig));
            return Task.FromResult(_responses.Dequeue());
        }
    }
}
