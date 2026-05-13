namespace TM.Framework.Platform;

/// <summary>
/// Shell 子进程调用抽象——把 <c>/usr/sbin/scutil --proxy</c> 的执行隔离出来，便于测试 mock。
/// </summary>
public interface IScutilCommandRunner
{
    /// <summary>执行 scutil 并返回 stdout；失败可抛异常。</summary>
    string Run();
}
