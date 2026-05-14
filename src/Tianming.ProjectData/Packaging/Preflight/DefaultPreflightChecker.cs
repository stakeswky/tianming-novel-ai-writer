using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Packaging.Preflight;

public sealed class DefaultPreflightChecker : IPreflightChecker
{
    private readonly string _projectRoot;

    public DefaultPreflightChecker(string projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot))
            throw new ArgumentException("项目根目录不能为空", nameof(projectRoot));

        _projectRoot = projectRoot;
    }

    public Task<PreflightReport> CheckAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var report = new PreflightReport();
        CheckGeneratedChapters(report);
        CheckStagedChanges(report);
        CheckDesignDirectory(report);
        return Task.FromResult(report);
    }

    private void CheckGeneratedChapters(PreflightReport report)
    {
        var chaptersDirectory = Path.Combine(_projectRoot, "Generated", "chapters");
        var chapters = Directory.Exists(chaptersDirectory)
            ? Directory.GetFiles(chaptersDirectory, "*.md", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();

        if (chapters.Length == 0)
        {
            report.Issues.Add(new PreflightIssue
            {
                Code = "NoChapters",
                Severity = PreflightSeverity.Error,
                Message = "项目下没有任何已生成的章节（Generated/chapters/*.md）。",
            });
        }
    }

    private void CheckStagedChanges(PreflightReport report)
    {
        var stagedDirectory = Path.Combine(_projectRoot, ".staged");
        if (!Directory.Exists(stagedDirectory))
            return;

        var stagedCount = Directory.GetFiles(stagedDirectory, "*.json", SearchOption.TopDirectoryOnly).Length;
        if (stagedCount == 0)
            return;

        report.Issues.Add(new PreflightIssue
        {
            Code = "StagedChangesPending",
            Severity = PreflightSeverity.Warning,
            Message = $"有 {stagedCount} 个待审核改动未处理（.staged/）。",
        });
    }

    private void CheckDesignDirectory(PreflightReport report)
    {
        var designDirectory = Path.Combine(_projectRoot, "Design");
        if (Directory.Exists(designDirectory))
            return;

        report.Issues.Add(new PreflightIssue
        {
            Code = "NoDesign",
            Severity = PreflightSeverity.Warning,
            Message = "Design 目录不存在；导出的 ZIP 将不含设计数据。",
        });
    }
}
