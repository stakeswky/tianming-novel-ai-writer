using System.Text.Json;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests.Tracking;

public class ConsistencyIssueExtendedTests
{
    [Fact]
    public void Issue_carries_layer_chunk_position_score()
    {
        dynamic issue = new ConsistencyIssue();
        issue.EntityId = "char-001";
        issue.IssueType = "LevelRegression";
        issue.Expected = "金丹";
        issue.Actual = "练气";
        issue.Layer = "Entity";
        issue.ChunkPosition = 3;
        issue.VectorScore = 0.87d;

        var json = JsonSerializer.Serialize((ConsistencyIssue)issue);
        var back = JsonSerializer.Deserialize<ConsistencyIssue>(json);

        Assert.NotNull(back);
        Assert.Equal("Entity", Get<string>(back!, "Layer"));
        Assert.Equal(3, Get<int>(back!, "ChunkPosition"));
        Assert.InRange(Get<double>(back!, "VectorScore"), 0.86d, 0.88d);
    }

    [Fact]
    public void Default_layer_and_position_are_zero_or_empty()
    {
        var issue = new ConsistencyIssue();

        Assert.Equal(string.Empty, Get<string>(issue, "Layer"));
        Assert.Equal(-1, Get<int>(issue, "ChunkPosition"));
        Assert.Equal(0d, Get<double>(issue, "VectorScore"));
    }

    private static T Get<T>(ConsistencyIssue issue, string propertyName)
    {
        var property = typeof(ConsistencyIssue).GetProperty(propertyName);
        Assert.NotNull(property);
        return Assert.IsType<T>(property!.GetValue(issue));
    }
}
