using System;
using System.Diagnostics;

namespace TM.Framework.Platform;

/// <summary>
/// 默认 shell 实现：fork <c>/usr/sbin/scutil --proxy</c>，读 stdout。
/// 5 秒超时；任何 IO / 启动失败抛回上层（由 <see cref="MacOSSystemProxyService"/> 兜底）。
/// </summary>
public sealed class ProcessScutilCommandRunner : IScutilCommandRunner
{
    private const string ScutilPath = "/usr/sbin/scutil";
    private const int TimeoutMs = 5000;

    public string Run()
    {
        var psi = new ProcessStartInfo(ScutilPath, "--proxy")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {ScutilPath}");

        var stdout = process.StandardOutput.ReadToEnd();
        if (!process.WaitForExit(TimeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
            throw new TimeoutException($"{ScutilPath} --proxy timed out after {TimeoutMs}ms");
        }
        return stdout;
    }
}
