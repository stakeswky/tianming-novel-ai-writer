using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.BookPipeline;
using TM.Services.Modules.ProjectData.BookPipeline.Steps;
using Xunit;

namespace Tianming.ProjectData.Tests.BookPipeline;

public class DesignStepTests
{
    [Fact]
    public async Task Name_and_placeholder_success_match_plan()
    {
        var step = new DesignStep();

        var result = await step.ExecuteAsync(new BookPipelineContext());

        Assert.Equal(BookPipelineStepName.Design, step.Name);
        Assert.True(result.Success);
    }
}
