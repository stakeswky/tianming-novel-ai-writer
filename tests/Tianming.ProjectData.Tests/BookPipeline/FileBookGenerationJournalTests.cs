using System;
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.BookPipeline;
using Xunit;

namespace Tianming.ProjectData.Tests.BookPipeline;

public class FileBookGenerationJournalTests
{
    [Fact]
    public async Task Records_step_completion()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-bookj-{Guid.NewGuid():N}");
        var journal = new FileBookGenerationJournal(dir);

        await journal.RecordCompletedAsync(BookPipelineStepName.Design);

        Assert.True(await journal.IsCompletedAsync(BookPipelineStepName.Design));
        Assert.False(await journal.IsCompletedAsync(BookPipelineStepName.Outline));
    }

    [Fact]
    public async Task Skip_marks_step_as_skipped_not_completed()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-bookj-{Guid.NewGuid():N}");
        var journal = new FileBookGenerationJournal(dir);

        await journal.MarkSkippedAsync(BookPipelineStepName.Humanize);

        Assert.True(await journal.IsSkippedAsync(BookPipelineStepName.Humanize));
        Assert.False(await journal.IsCompletedAsync(BookPipelineStepName.Humanize));
    }

    [Fact]
    public async Task Reset_clears_step()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"tm-bookj-{Guid.NewGuid():N}");
        var journal = new FileBookGenerationJournal(dir);

        await journal.RecordCompletedAsync(BookPipelineStepName.Design);
        await journal.ResetAsync(BookPipelineStepName.Design);

        Assert.False(await journal.IsCompletedAsync(BookPipelineStepName.Design));
    }
}
