using System.Text.Json;
using TM.Services.Modules.ProjectData.StagedChanges;
using Xunit;

namespace Tianming.ProjectData.Tests.StagedChanges;

public class StagedChangeTests
{
    [Fact]
    public void StagedChange_round_trips()
    {
        var change = new StagedChange
        {
            Id = "stg-abc",
            ChangeType = StagedChangeType.ContentEdit,
            TargetId = "ch-005",
            OldContentSnippet = "old text",
            NewContentSnippet = "new text",
            Reason = "AI suggested rewrite",
        };

        var json = JsonSerializer.Serialize(change);
        var back = JsonSerializer.Deserialize<StagedChange>(json);

        Assert.NotNull(back);
        Assert.Equal(StagedChangeType.ContentEdit, back!.ChangeType);
        Assert.Equal("ch-005", back.TargetId);
    }

    [Fact]
    public void Three_change_types_defined()
    {
        Assert.True(System.Enum.IsDefined(typeof(StagedChangeType), StagedChangeType.WorkspaceEdit));
        Assert.True(System.Enum.IsDefined(typeof(StagedChangeType), StagedChangeType.DataEdit));
        Assert.True(System.Enum.IsDefined(typeof(StagedChangeType), StagedChangeType.ContentEdit));
    }
}
