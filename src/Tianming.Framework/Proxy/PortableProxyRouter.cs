using System.Text.RegularExpressions;

namespace TM.Framework.Proxy;

public enum PortableProxyType
{
    Http,
    Https,
    Socks5,
    Socks4
}

public enum PortableProxyRuleType
{
    Domain,
    IP,
    Wildcard,
    Regex
}

public enum PortableProxyAction
{
    Direct,
    Proxy,
    Block
}

public enum PortableProxyDecisionKind
{
    Direct,
    Proxy,
    Block
}

public sealed class PortableProxyConfig
{
    public PortableProxyType Type { get; init; } = PortableProxyType.Http;

    public string Server { get; init; } = string.Empty;

    public int Port { get; init; } = 8080;

    public bool RequiresAuth { get; init; }

    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public IReadOnlyList<string> BypassList { get; init; } = [];
}

public sealed class PortableProxyCredentials
{
    public string UserName { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}

public sealed class PortableProxyRule
{
    public PortableProxyRuleType Type { get; init; }

    public string Pattern { get; init; } = string.Empty;

    public PortableProxyAction Action { get; init; }

    public int Priority { get; init; }

    public bool Enabled { get; init; } = true;
}

public sealed class PortableProxyDecision
{
    public PortableProxyDecisionKind Kind { get; init; }

    public Uri? ProxyUri { get; init; }

    public PortableProxyCredentials? Credentials { get; init; }

    public string Reason { get; init; } = string.Empty;
}

public sealed class PortableProxyRouter
{
    private readonly PortableProxyConfig _config;
    private readonly IReadOnlyList<PortableProxyRule> _rules;

    public PortableProxyRouter(PortableProxyConfig config, IReadOnlyList<PortableProxyRule>? rules = null)
    {
        _config = config;
        _rules = rules ?? [];
    }

    public PortableProxyDecision Resolve(Uri destination)
    {
        var host = destination.Host;
        var rule = _rules
            .Where(candidate => candidate.Enabled)
            .OrderBy(candidate => candidate.Priority)
            .FirstOrDefault(candidate => IsRuleMatch(candidate, host));

        if (rule is not null)
        {
            return rule.Action switch
            {
                PortableProxyAction.Direct => Direct($"Matched rule: {rule.Pattern}"),
                PortableProxyAction.Block => new PortableProxyDecision
                {
                    Kind = PortableProxyDecisionKind.Block,
                    Reason = $"Matched rule: {rule.Pattern}"
                },
                PortableProxyAction.Proxy => ResolveConfiguredProxy($"Matched rule: {rule.Pattern}"),
                _ => Direct($"Matched rule: {rule.Pattern}")
            };
        }

        if (IsBypassed(host))
        {
            return Direct("Matched bypass list");
        }

        return ResolveConfiguredProxy("Default proxy");
    }

    private PortableProxyDecision ResolveConfiguredProxy(string reason)
    {
        if (string.IsNullOrWhiteSpace(_config.Server) || _config.Port <= 0)
        {
            return Direct("Proxy server is not configured");
        }

        if (_config.Type is not (PortableProxyType.Http or PortableProxyType.Https))
        {
            return Direct($"Unsupported proxy type for managed HTTP clients: {_config.Type}");
        }

        var scheme = _config.Type == PortableProxyType.Https ? "https" : "http";
        var builder = new UriBuilder(scheme, _config.Server, _config.Port);
        return new PortableProxyDecision
        {
            Kind = PortableProxyDecisionKind.Proxy,
            ProxyUri = builder.Uri,
            Credentials = _config.RequiresAuth && !string.IsNullOrWhiteSpace(_config.Username)
                ? new PortableProxyCredentials { UserName = _config.Username, Password = _config.Password }
                : null,
            Reason = reason
        };
    }

    private bool IsBypassed(string host)
    {
        foreach (var entry in _config.BypassList.Where(entry => !string.IsNullOrWhiteSpace(entry)))
        {
            if (string.Equals(entry, "<local>", StringComparison.OrdinalIgnoreCase) && IsLocalHost(host))
            {
                return true;
            }

            if (IsWildcardMatch(entry, host))
            {
                return true;
            }

            if (string.Equals(host, entry, StringComparison.OrdinalIgnoreCase) ||
                host.EndsWith("." + entry, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsRuleMatch(PortableProxyRule rule, string host)
    {
        try
        {
            return rule.Type switch
            {
                PortableProxyRuleType.Domain =>
                    string.Equals(host, rule.Pattern, StringComparison.OrdinalIgnoreCase) ||
                    host.EndsWith("." + rule.Pattern, StringComparison.OrdinalIgnoreCase),
                PortableProxyRuleType.IP => string.Equals(host, rule.Pattern, StringComparison.OrdinalIgnoreCase),
                PortableProxyRuleType.Wildcard => IsWildcardMatch(rule.Pattern, host),
                PortableProxyRuleType.Regex => Regex.IsMatch(host, rule.Pattern, RegexOptions.IgnoreCase),
                _ => false
            };
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool IsWildcardMatch(string pattern, string value)
    {
        var regexPattern = "^" + Regex.Escape(pattern).Replace("\\*", ".*", StringComparison.Ordinal).Replace("\\?", ".", StringComparison.Ordinal) + "$";
        return Regex.IsMatch(value, regexPattern, RegexOptions.IgnoreCase);
    }

    private static bool IsLocalHost(string host)
    {
        return string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase) ||
               !host.Contains('.', StringComparison.Ordinal);
    }

    private static PortableProxyDecision Direct(string reason)
    {
        return new PortableProxyDecision
        {
            Kind = PortableProxyDecisionKind.Direct,
            Reason = reason
        };
    }
}
