namespace TM.Framework.Platform;

/// <summary>
/// macOS 系统代理读取服务：调用 <see cref="IScutilCommandRunner"/> 取 scutil 输出，
/// 交给 <see cref="ScutilProxyOutputParser"/> 解析成 <see cref="ProxyPolicy"/>。
/// 任何异常吞掉返回 <see cref="ProxyPolicy.Direct"/>，永不抛出。
/// </summary>
public sealed class MacOSSystemProxyService : IPortableSystemProxyService
{
    private readonly IScutilCommandRunner _runner;

    /// <summary>使用默认 <see cref="ProcessScutilCommandRunner"/>。生产入口。</summary>
    public MacOSSystemProxyService() : this(new ProcessScutilCommandRunner()) { }

    public MacOSSystemProxyService(IScutilCommandRunner runner)
    {
        _runner = runner;
    }

    public ProxyPolicy GetCurrent()
    {
        try
        {
            var output = _runner.Run();
            return ScutilProxyOutputParser.Parse(output);
        }
        catch
        {
            // scutil 不可用 / 权限不足 / Mojave 之前格式异常都退化到 Direct，
            // 不让代理读取错误中断 AI 出站请求。
            return ProxyPolicy.Direct;
        }
    }
}
