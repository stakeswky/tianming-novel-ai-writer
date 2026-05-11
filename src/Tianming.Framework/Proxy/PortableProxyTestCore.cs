using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace TM.Framework.Proxy;

public sealed class PortableProxyTestResult
{
    public bool IsConnected { get; init; }

    public int Latency { get; init; }

    public double DownloadSpeed { get; init; }

    public string ExitIP { get; init; } = string.Empty;

    public string Location { get; init; } = string.Empty;

    public bool DNSLeakDetected { get; init; }

    public int AnonymityScore { get; init; }

    public List<string> Issues { get; init; } = [];

    public DateTime TestTime { get; init; } = DateTime.Now;
}

public sealed class PortableProxyOneClickVerificationResult
{
    public DateTime TestTime { get; init; } = DateTime.Now;

    public bool DirectSuccess { get; init; }

    public string DirectIP { get; init; } = string.Empty;

    public string DirectError { get; init; } = string.Empty;

    public bool ProxySuccess { get; init; }

    public string ProxyIP { get; init; } = string.Empty;

    public string ProxyError { get; init; } = string.Empty;

    public bool IsProxyEffective { get; init; }

    public string Summary { get; init; } = string.Empty;
}

public sealed record PortableProxyIpProbeResult(bool Success, string Ip, string Error);

public sealed record PortableProxyHttpProbeRequest(Uri Url, PortableProxyConfig? ProxyConfig);

public sealed record PortableProxyHttpProbeResponse(
    bool Success,
    string Content,
    long BytesReceived,
    TimeSpan Elapsed,
    string Error);

public interface IPortableProxyProbeHttpClient
{
    Task<PortableProxyHttpProbeResponse> GetAsync(
        Uri url,
        PortableProxyConfig? proxyConfig,
        CancellationToken cancellationToken = default);
}

public sealed class PortableProxyTestHistoryStore
{
    private readonly string _historyFile;
    private readonly int _maxRecords;
    private readonly List<PortableProxyTestResult> _history;

    public PortableProxyTestHistoryStore(string historyFile, int maxRecords = 100)
    {
        if (maxRecords <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRecords), "Max records must be positive.");
        }

        _historyFile = historyFile;
        _maxRecords = maxRecords;
        _history = LoadHistory(historyFile)
            .OrderByDescending(result => result.TestTime)
            .Take(maxRecords)
            .ToList();
    }

    public IReadOnlyList<PortableProxyTestResult> GetHistory()
    {
        return _history.ToList();
    }

    public void Add(PortableProxyTestResult result)
    {
        _history.Insert(0, result);
        while (_history.Count > _maxRecords)
        {
            _history.RemoveAt(_history.Count - 1);
        }

        SaveHistory();
    }

    private static List<PortableProxyTestResult> LoadHistory(string historyFile)
    {
        try
        {
            if (!File.Exists(historyFile))
            {
                return [];
            }

            var history = JsonSerializer.Deserialize<List<PortableProxyTestResult>>(File.ReadAllText(historyFile));
            return history ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }
    }

    private void SaveHistory()
    {
        var directory = Path.GetDirectoryName(_historyFile);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = _historyFile + ".tmp";
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(tempPath, JsonSerializer.Serialize(_history, options));
        File.Move(tempPath, _historyFile, overwrite: true);
    }
}

public static class PortableProxyVerificationEvaluator
{
    public static PortableProxyOneClickVerificationResult Evaluate(
        PortableProxyIpProbeResult direct,
        PortableProxyIpProbeResult proxied,
        DateTime? testTime = null)
    {
        if (direct.Success && proxied.Success)
        {
            var effective = !string.Equals(direct.Ip, proxied.Ip, StringComparison.OrdinalIgnoreCase);
            return new PortableProxyOneClickVerificationResult
            {
                TestTime = testTime ?? DateTime.Now,
                DirectSuccess = true,
                DirectIP = direct.Ip,
                ProxySuccess = true,
                ProxyIP = proxied.Ip,
                IsProxyEffective = effective,
                Summary = effective
                    ? $"应用内代理已生效（直连IP={direct.Ip}，代理IP={proxied.Ip}）"
                    : $"直连IP与代理IP相同（{direct.Ip}），可能未走代理或代理出口与直连一致"
            };
        }

        if (!direct.Success && proxied.Success)
        {
            return new PortableProxyOneClickVerificationResult
            {
                TestTime = testTime ?? DateTime.Now,
                DirectSuccess = false,
                DirectError = direct.Error,
                ProxySuccess = true,
                ProxyIP = proxied.Ip,
                IsProxyEffective = true,
                Summary = $"直连失败，但应用内代理可用（代理IP={proxied.Ip}）"
            };
        }

        if (direct.Success && !proxied.Success)
        {
            return new PortableProxyOneClickVerificationResult
            {
                TestTime = testTime ?? DateTime.Now,
                DirectSuccess = true,
                DirectIP = direct.Ip,
                ProxySuccess = false,
                ProxyError = proxied.Error,
                IsProxyEffective = false,
                Summary = $"直连可用（IP={direct.Ip}），但应用内代理请求失败：{proxied.Error}"
            };
        }

        return new PortableProxyOneClickVerificationResult
        {
            TestTime = testTime ?? DateTime.Now,
            DirectSuccess = false,
            DirectError = direct.Error,
            ProxySuccess = false,
            ProxyError = proxied.Error,
            IsProxyEffective = false,
            Summary = $"直连与代理均失败：直连={direct.Error}；代理={proxied.Error}"
        };
    }

    public static int CalculateAnonymityScore(PortableProxyTestResult result)
    {
        var score = 100;

        if (!result.IsConnected)
        {
            score -= 100;
        }

        if (result.Latency > 1000)
        {
            score -= 20;
        }
        else if (result.Latency > 500)
        {
            score -= 10;
        }

        if (result.DNSLeakDetected)
        {
            score -= 30;
        }

        if (result.DownloadSpeed < 0.1)
        {
            score -= 10;
        }

        return Math.Max(0, score);
    }
}

public sealed class PortableProxyNetworkProbe
{
    public static readonly Uri DefaultConnectivityUrl = new("https://www.google.com");

    public static readonly Uri DefaultIpCheckUrl = new("https://api.ipify.org?format=json");

    public static readonly Uri DefaultSpeedTestUrl = new("https://speed.cloudflare.com/__down?bytes=10000000");

    public static readonly Uri DefaultDnsLeakTestUrl = new("https://dnsleaktest.com");

    private readonly IPortableProxyProbeHttpClient _httpClient;
    private readonly Func<DateTime> _clock;

    public PortableProxyNetworkProbe()
        : this(new PortableProxyProbeHttpClient())
    {
    }

    public PortableProxyNetworkProbe(
        IPortableProxyProbeHttpClient httpClient,
        Func<DateTime>? clock = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _clock = clock ?? (() => DateTime.Now);
    }

    public async Task<PortableProxyTestResult> TestAllAsync(
        PortableProxyConfig config,
        CancellationToken cancellationToken = default)
    {
        var testTime = _clock();
        var connectivity = await TestConnectivityAsync(config, cancellationToken).ConfigureAwait(false);
        if (!connectivity.IsConnected)
        {
            return new PortableProxyTestResult
            {
                IsConnected = false,
                Latency = connectivity.Latency,
                Issues = ["代理服务器无法连接"],
                AnonymityScore = 0,
                TestTime = testTime
            };
        }

        var ipResult = await TestIpAddressAsync(config, cancellationToken).ConfigureAwait(false);
        var speed = await TestSpeedAsync(config, cancellationToken).ConfigureAwait(false);
        var dnsLeak = await TestDnsLeakAsync(config, cancellationToken).ConfigureAwait(false);
        var issues = ipResult.Issues.ToList();
        if (dnsLeak)
        {
            issues.Add("检测到DNS泄漏");
        }

        var result = new PortableProxyTestResult
        {
            IsConnected = true,
            Latency = connectivity.Latency,
            DownloadSpeed = speed,
            ExitIP = ipResult.ExitIP,
            Location = ipResult.Location,
            DNSLeakDetected = dnsLeak,
            Issues = issues,
            TestTime = testTime
        };

        return new PortableProxyTestResult
        {
            IsConnected = result.IsConnected,
            Latency = result.Latency,
            DownloadSpeed = result.DownloadSpeed,
            ExitIP = result.ExitIP,
            Location = result.Location,
            DNSLeakDetected = result.DNSLeakDetected,
            Issues = result.Issues,
            TestTime = result.TestTime,
            AnonymityScore = PortableProxyVerificationEvaluator.CalculateAnonymityScore(result)
        };
    }

    public async Task<PortableProxyTestResult> TestConnectivityAsync(
        PortableProxyConfig config,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(DefaultConnectivityUrl, config, cancellationToken).ConfigureAwait(false);
        return new PortableProxyTestResult
        {
            IsConnected = response.Success,
            Latency = (int)Math.Round(response.Elapsed.TotalMilliseconds),
            Issues = response.Success ? [] : ["连接失败"]
        };
    }

    public async Task<PortableProxyTestResult> TestIpAddressAsync(
        PortableProxyConfig config,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(DefaultIpCheckUrl, config, cancellationToken).ConfigureAwait(false);
        if (!response.Success)
        {
            return new PortableProxyTestResult
            {
                Issues = [$"IP检测失败: {response.Error}"]
            };
        }

        var ip = ParseIp(response.Content);
        if (string.IsNullOrWhiteSpace(ip))
        {
            return new PortableProxyTestResult
            {
                Issues = ["IP检测失败: 无法解析IP响应"]
            };
        }

        var location = await GetIpLocationAsync(ip, cancellationToken).ConfigureAwait(false);
        return new PortableProxyTestResult
        {
            ExitIP = ip,
            Location = location
        };
    }

    public async Task<double> TestSpeedAsync(
        PortableProxyConfig config,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(DefaultSpeedTestUrl, config, cancellationToken).ConfigureAwait(false);
        if (!response.Success || response.Elapsed <= TimeSpan.Zero)
        {
            return 0;
        }

        var bytesPerSecond = response.BytesReceived / response.Elapsed.TotalSeconds;
        return bytesPerSecond / (1024 * 1024);
    }

    public async Task<bool> TestDnsLeakAsync(
        PortableProxyConfig config,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync(DefaultDnsLeakTestUrl, config, cancellationToken).ConfigureAwait(false);
        return !response.Success;
    }

    public async Task<PortableProxyOneClickVerificationResult> VerifyApplicationProxyAsync(
        PortableProxyConfig config,
        CancellationToken cancellationToken = default)
    {
        var direct = await GetPublicIpAsync(null, cancellationToken).ConfigureAwait(false);
        var proxied = await GetPublicIpAsync(config, cancellationToken).ConfigureAwait(false);
        return PortableProxyVerificationEvaluator.Evaluate(direct, proxied, _clock());
    }

    private async Task<string> GetIpLocationAsync(string ip, CancellationToken cancellationToken)
    {
        var response = await _httpClient
            .GetAsync(new Uri($"https://ipapi.co/{Uri.EscapeDataString(ip)}/json/"), proxyConfig: null, cancellationToken)
            .ConfigureAwait(false);
        if (!response.Success)
        {
            return "未知";
        }

        try
        {
            using var document = JsonDocument.Parse(response.Content);
            var root = document.RootElement;
            var country = root.TryGetProperty("country_name", out var countryElement)
                ? countryElement.GetString()
                : string.Empty;
            var city = root.TryGetProperty("city", out var cityElement)
                ? cityElement.GetString()
                : string.Empty;
            var location = $"{country} {city}".Trim();
            return string.IsNullOrWhiteSpace(location) ? "未知" : location;
        }
        catch (JsonException)
        {
            return "未知";
        }
    }

    private async Task<PortableProxyIpProbeResult> GetPublicIpAsync(
        PortableProxyConfig? config,
        CancellationToken cancellationToken)
    {
        var response = await _httpClient.GetAsync(DefaultIpCheckUrl, config, cancellationToken).ConfigureAwait(false);
        if (!response.Success)
        {
            return new PortableProxyIpProbeResult(false, string.Empty, response.Error);
        }

        var ip = ParseIp(response.Content);
        return string.IsNullOrWhiteSpace(ip)
            ? new PortableProxyIpProbeResult(false, string.Empty, "无法解析IP响应")
            : new PortableProxyIpProbeResult(true, ip, string.Empty);
    }

    private static string ParseIp(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            return document.RootElement.TryGetProperty("ip", out var ipElement)
                ? ipElement.GetString() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}

public sealed class PortableProxyProbeHttpClient : IPortableProxyProbeHttpClient
{
    public async Task<PortableProxyHttpProbeResponse> GetAsync(
        Uri url,
        PortableProxyConfig? proxyConfig,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var handler = CreateHandler(proxyConfig);
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
            using var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();
            var content = System.Text.Encoding.UTF8.GetString(bytes);
            return new PortableProxyHttpProbeResponse(
                response.IsSuccessStatusCode,
                content,
                bytes.LongLength,
                stopwatch.Elapsed,
                response.IsSuccessStatusCode ? string.Empty : $"{(int)response.StatusCode} {response.ReasonPhrase}".Trim());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException or InvalidOperationException)
        {
            stopwatch.Stop();
            return new PortableProxyHttpProbeResponse(false, string.Empty, 0, stopwatch.Elapsed, ex.Message);
        }
    }

    private static HttpClientHandler CreateHandler(PortableProxyConfig? config)
    {
        var handler = new HttpClientHandler();
        if (config is null || string.IsNullOrWhiteSpace(config.Server) || config.Port <= 0)
        {
            handler.UseProxy = false;
            return handler;
        }

        var scheme = config.Type switch
        {
            PortableProxyType.Https => "https",
            PortableProxyType.Socks4 => "socks4",
            PortableProxyType.Socks5 => "socks5",
            _ => "http"
        };
        var proxy = new WebProxy(new UriBuilder(scheme, config.Server, config.Port).Uri);
        if (config.RequiresAuth && !string.IsNullOrWhiteSpace(config.Username))
        {
            proxy.Credentials = new NetworkCredential(config.Username, config.Password);
        }

        handler.Proxy = proxy;
        handler.UseProxy = true;
        return handler;
    }
}
