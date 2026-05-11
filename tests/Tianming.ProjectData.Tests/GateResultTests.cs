using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class GateResultTests
{
    [Fact]
    public void GetTopFailures_prefixes_failure_type_and_limits_count()
    {
        var result = new GateResult();
        result.AddFailure(FailureType.Protocol, ["missing changes", "bad json"]);
        result.AddFailure(FailureType.Consistency, "movement mismatch");

        var failures = result.GetTopFailures(2);

        Assert.Equal(2, failures.Count);
        Assert.Equal("[Protocol] missing changes", failures[0]);
        Assert.Equal("[Protocol] bad json", failures[1]);
    }

    [Fact]
    public void GetHumanReadableFailures_explains_missing_changes_protocol()
    {
        var result = new GateResult();
        result.AddFailure(FailureType.Protocol, "未识别到CHANGES区域");

        var failures = result.GetHumanReadableFailures(1);

        Assert.Single(failures);
        Assert.Contains("---CHANGES---", failures[0]);
    }

    [Fact]
    public void GetHumanReadableFailures_explains_consistency_issue_without_global_name_resolution()
    {
        var result = new GateResult();
        result.AddFailure(
            FailureType.Consistency,
            "[MovementChainBreak] 实体: C7M3VT2K9P4NA, 期望: 移动路径连续, 实际: 路径断裂");

        var failures = result.GetHumanReadableFailures(1);

        Assert.Single(failures);
        Assert.Contains("C7M3VT2K9P4NA", failures[0]);
        Assert.Contains("路径不连续", failures[0]);
    }
}
