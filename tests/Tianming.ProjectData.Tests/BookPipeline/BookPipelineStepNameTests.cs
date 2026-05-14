using TM.Services.Modules.ProjectData.BookPipeline;
using Xunit;

namespace Tianming.ProjectData.Tests.BookPipeline;

public class BookPipelineStepNameTests
{
    [Fact]
    public void Ten_step_names_defined()
    {
        var names = new[]
        {
            BookPipelineStepName.Design,
            BookPipelineStepName.Outline,
            BookPipelineStepName.Volume,
            BookPipelineStepName.ChapterPlanning,
            BookPipelineStepName.Blueprint,
            BookPipelineStepName.Generate,
            BookPipelineStepName.Humanize,
            BookPipelineStepName.Gate,
            BookPipelineStepName.Save,
            BookPipelineStepName.Index,
        };

        Assert.Equal(10, names.Length);
        Assert.Equal("Design", names[0]);
    }
}
