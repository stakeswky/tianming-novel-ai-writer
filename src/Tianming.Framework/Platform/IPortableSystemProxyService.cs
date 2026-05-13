namespace TM.Framework.Platform;

/// <summary>
/// 平台无关的系统代理读取接口。macOS 实现走 shell <c>scutil --proxy</c>。
/// </summary>
public interface IPortableSystemProxyService
{
    /// <summary>读取当前系统代理快照；任何失败返回 <see cref="ProxyPolicy.Direct"/>（不抛）。</summary>
    ProxyPolicy GetCurrent();
}
