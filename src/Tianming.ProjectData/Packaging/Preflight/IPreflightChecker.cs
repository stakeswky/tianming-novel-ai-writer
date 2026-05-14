using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Packaging.Preflight;

public interface IPreflightChecker
{
    Task<PreflightReport> CheckAsync(CancellationToken ct = default);
}

public sealed class PreflightReport
{
    public bool IsPass => Issues.Count == 0 || !Issues.Any(issue => issue.Severity == PreflightSeverity.Error);

    public List<PreflightIssue> Issues { get; } = new();
}

public sealed class PreflightIssue
{
    public string Code { get; set; } = string.Empty;

    public PreflightSeverity Severity { get; set; }

    public string Message { get; set; } = string.Empty;
}

public enum PreflightSeverity
{
    Warning,
    Error,
}
