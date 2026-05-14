using TM.Services.Modules.ProjectData.Implementations;
using Xunit;

namespace Tianming.ProjectData.Tests.Generation;

public class ChangesCanonicalizerTests
{
    [Fact]
    public void Reorders_fields_to_canonical_order()
    {
        var input = """{ "角色移动": [], "角色状态变化": [], "时间推进": null }""";
        var output = ChangesCanonicalizer.Canonicalize(input);
        var first = output.IndexOf("角色状态变化", System.StringComparison.Ordinal);
        var second = output.IndexOf("角色移动", System.StringComparison.Ordinal);
        Assert.True(first < second, "角色状态变化 should come before 角色移动");
    }

    [Fact]
    public void Adds_missing_required_fields_as_empty_arrays()
    {
        var input = """{ "角色状态变化": [] }""";
        var output = ChangesCanonicalizer.Canonicalize(input);
        Assert.Contains("冲突进度", output);
        Assert.Contains("伏笔动作", output);
    }

    [Fact]
    public void Preserves_non_empty_content()
    {
        var input = """{ "角色状态变化": [{"角色ID":"char-001","状态":"愤怒"}] }""";
        var output = ChangesCanonicalizer.Canonicalize(input);
        Assert.Contains("char-001", output);
        Assert.Contains("愤怒", output);
    }

    [Fact]
    public void Preserves_existing_english_protocol_fields()
    {
        var input = """{ "CharacterMovements": [], "CharacterStateChanges": [], "TimeProgression": null }""";
        var output = ChangesCanonicalizer.Canonicalize(input);
        var first = output.IndexOf("CharacterStateChanges", System.StringComparison.Ordinal);
        var second = output.IndexOf("CharacterMovements", System.StringComparison.Ordinal);
        Assert.True(first < second, "CharacterStateChanges should come before CharacterMovements");
    }
}
