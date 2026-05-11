using System.Diagnostics;

namespace TM.Framework.Logging;

public enum PortableLogOutputTestStatus
{
    Success,
    Failed,
    Timeout,
    NotTested
}

public sealed record PortableLogOutputTestResult(
    string TargetName,
    PortableLogOutputTargetType TargetType,
    PortableLogOutputTestStatus Status,
    DateTime TestTime,
    long ResponseTimeMs,
    string Message,
    string Details);

public sealed class PortableLogOutputTestRunner
{
    private readonly PortableLogOutputDispatcher _dispatcher;
    private readonly PortableLogOutputTelemetryStore _telemetryStore;
    private readonly Func<DateTime> _clock;

    public PortableLogOutputTestRunner(
        PortableLogOutputDispatcher dispatcher,
        PortableLogOutputTelemetryStore telemetryStore,
        Func<DateTime>? clock = null)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _telemetryStore = telemetryStore ?? throw new ArgumentNullException(nameof(telemetryStore));
        _clock = clock ?? (() => DateTime.Now);
    }

    public async Task<IReadOnlyList<PortableLogOutputTestResult>> TestAllAsync(
        PortableLogOutputSettings settings,
        string testContent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var targets = PortableLogOutputTargetBuilder.BuildTargets(settings)
            .Where(target => target.IsEnabled)
            .ToList();
        if (targets.Count == 0)
        {
            return [];
        }

        var stopwatch = Stopwatch.StartNew();
        var dispatchResult = await _dispatcher.DispatchAsync(targets, testContent, cancellationToken)
            .ConfigureAwait(false);
        stopwatch.Stop();

        var responseTimes = dispatchResult.Attempts.ToDictionary(
            attempt => attempt.TargetName,
            _ => stopwatch.ElapsedMilliseconds,
            StringComparer.OrdinalIgnoreCase);
        await _telemetryStore.ApplyDispatchResultAsync(dispatchResult, responseTimes, cancellationToken)
            .ConfigureAwait(false);

        var testTime = _clock();
        return dispatchResult.Attempts
            .Select(attempt => new PortableLogOutputTestResult(
                attempt.TargetName,
                attempt.TargetType,
                attempt.Success ? PortableLogOutputTestStatus.Success : PortableLogOutputTestStatus.Failed,
                testTime,
                responseTimes.TryGetValue(attempt.TargetName, out var responseTimeMs) ? responseTimeMs : 0,
                attempt.Success ? "输出测试成功" : attempt.ErrorMessage ?? "输出测试失败",
                attempt.Success ? "目标已成功接收测试日志。" : attempt.ErrorMessage ?? string.Empty))
            .ToList();
    }
}
