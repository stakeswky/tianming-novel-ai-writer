using TM.Services.Modules.ProjectData.Humanize;
using Xunit;

namespace Tianming.ProjectData.Tests.Humanize;

public class HumanizeContextTests
{
    [Fact]
    public void HumanizeContext_carries_chapter_id_and_text()
    {
        var ctx = new HumanizeContext { ChapterId = "ch-001", InputText = "hello AI" };
        Assert.Equal("ch-001", ctx.ChapterId);
        Assert.Equal("hello AI", ctx.InputText);
    }
}
