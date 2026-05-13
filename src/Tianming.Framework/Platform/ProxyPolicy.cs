using System;
using System.Collections.Generic;

namespace TM.Framework.Platform;

/// <summary>
/// 系统代理快照：解析自 macOS <c>scutil --proxy</c> 输出，描述当前 HTTP / HTTPS 代理 URI 与 bypass 例外列表。
/// 不可变记录；用 <see cref="Direct"/> 表示"无代理直连"。
/// </summary>
public sealed record ProxyPolicy(
    Uri? HttpProxy,
    Uri? HttpsProxy,
    IReadOnlyList<string> Exceptions)
{
    /// <summary>无任何代理（直连）的全局共享实例。</summary>
    public static ProxyPolicy Direct { get; } = new(null, null, Array.Empty<string>());

    /// <summary>当前快照是否包含至少一个代理 URI。</summary>
    public bool HasProxy => HttpProxy is not null || HttpsProxy is not null;

    /// <summary>
    /// 根据目标 URI 的 scheme 选择应使用的代理 URI；
    /// HTTPS 目标优先用 HTTPS 代理，回退 HTTP 代理。
    /// </summary>
    public Uri? ResolveFor(Uri target) => target.Scheme.ToLowerInvariant() switch
    {
        "https" => HttpsProxy ?? HttpProxy,
        "http"  => HttpProxy,
        _       => null,
    };

    /// <summary>判断目标 URI 是否命中 bypass 例外列表（命中则不走代理）。</summary>
    public bool ShouldBypass(Uri target)
    {
        if (target is null) return false;
        foreach (var rule in Exceptions)
        {
            if (MatchesBypass(target.Host, rule)) return true;
        }
        return false;
    }

    private static bool MatchesBypass(string host, string rule)
    {
        // 常见形式："*.local"、"169.254/16"、"localhost"、"10.0.0.0/8"
        if (string.IsNullOrWhiteSpace(rule)) return false;
        if (rule.Equals(host, StringComparison.OrdinalIgnoreCase)) return true;
        if (rule.StartsWith("*.", StringComparison.Ordinal)
            && host.EndsWith(rule[1..], StringComparison.OrdinalIgnoreCase)) return true;
        if (rule.Contains('/', StringComparison.Ordinal))
        {
            // CIDR 不精确匹配，只做点分前缀匹配（自用场景够用）
            var prefix = rule.Split('/')[0];
            return host.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}
