using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.BookPipeline;
using TM.Services.Modules.ProjectData.BookPipeline.Steps;
using Xunit;

namespace Tianming.ProjectData.Tests.BookPipeline;

public class IndexStepTests
{
    [Fact]
    public async Task Name_and_placeholder_success_match_plan()
    {
        var step = new IndexStep();

        var result = await step.ExecuteAsync(new BookPipelineContext());

        Assert.Equal(BookPipelineStepName.Index, step.Name);
        Assert.True(result.Success);
    }
}
