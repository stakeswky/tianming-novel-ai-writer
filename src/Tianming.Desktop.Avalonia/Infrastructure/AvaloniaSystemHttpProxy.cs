using System;
using System.Net;
using TM.Framework.Platform;

namespace Tianming.Desktop.Avalonia.Infrastructure;

/// <summary>
/// IWebProxy 适配器：每次 HTTP 请求询问 <see cref="IPortableSystemProxyService"/>
/// 并按 <see cref="ProxyPolicy"/> 决定走代理 / 直连。
/// 在 macOS 上从 <c>scutil --proxy</c> 读系统代理；用户在系统偏好里改完代理
/// 不用重启天命即可生效（下一次出站请求重新读取）。
/// </summary>
public sealed class AvaloniaSystemHttpProxy : IWebProxy
{
    private readonly IPortableSystemProxyService _proxyService;

    public AvaloniaSystemHttpProxy(IPortableSystemProxyService proxyService)
    {
        _proxyService = proxyService ?? throw new ArgumentNullException(nameof(proxyService));
    }

    /// <summary>系统代理没有 credentials 概念，留空即可。</summary>
    public ICredentials? Credentials { get; set; }

    public Uri? GetProxy(Uri destination)
    {
        if (destination is null) return null;
        var policy = _proxyService.GetCurrent();
        if (!policy.HasProxy) return null;
        if (policy.ShouldBypass(destination)) return null;
        return policy.ResolveFor(destination);
    }

    public bool IsBypassed(Uri host)
    {
        if (host is null) return true;
        var policy = _proxyService.GetCurrent();
        if (!policy.HasProxy) return true;
        return policy.ShouldBypass(host);
    }
}
