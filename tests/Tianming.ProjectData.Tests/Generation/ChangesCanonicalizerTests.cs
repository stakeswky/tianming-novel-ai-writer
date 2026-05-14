using System.Text.Json;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Generation;

public class ChangesCanonicalizerTests
{
    [Fact]
    public void Reorders_fields_to_canonical_order()
    {
        var input = """{ "角色移动": [], "角色状态变化": [], "时间推进": null }""";
        var output = ChangesCanonicalizer.Canonicalize(input);
        var first = output.IndexOf("CharacterStateChanges", System.StringComparison.Ordinal);
        var second = output.IndexOf("CharacterMovements", System.StringComparison.Ordinal);
        Assert.True(first < second, "CharacterStateChanges should come before CharacterMovements");
    }

    [Fact]
    public void Adds_missing_required_fields_as_empty_arrays()
    {
        var input = """{ "角色状态变化": [] }""";
        var output = ChangesCanonicalizer.Canonicalize(input);
        Assert.Contains("ConflictProgress", output);
        Assert.Contains("ForeshadowingActions", output);
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

    [Fact]
    public void Canonicalize_maps_chinese_alias_payload_to_non_empty_english_protocol()
    {
        var input = """
        {
          "角色变化": [
            {
              "角色ID": "C7M3VT2K9P4NA",
              "新心理状态": "愤怒",
              "关键事件": "拔剑"
            }
          ],
          "时间推进": {
            "时间段": "第三天黄昏",
            "经过时间": "半日",
            "关键时间事件": "抵达寒潭"
          }
        }
        """;

        var output = ChangesCanonicalizer.Canonicalize(input);
        var parsed = JsonSerializer.Deserialize<ChapterChanges>(output);

        Assert.NotNull(parsed);
        Assert.Single(parsed!.CharacterStateChanges);
        Assert.Equal("C7M3VT2K9P4NA", parsed.CharacterStateChanges[0].CharacterId);
        Assert.Equal("愤怒", parsed.CharacterStateChanges[0].NewMentalState);
        Assert.Equal("拔剑", parsed.CharacterStateChanges[0].KeyEvent);
        Assert.Equal("第三天黄昏", parsed.TimeProgression!.TimePeriod);
        Assert.Contains("CharacterStateChanges", output);
        Assert.DoesNotContain("\"角色变化\"", output);
    }
}
