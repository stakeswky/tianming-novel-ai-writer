using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.BookPipeline.Steps;

public sealed class SaveStep : IBookPipelineStep
{
    public string Name => BookPipelineStepName.Save;

    public Task<BookStepResult> ExecuteAsync(BookPipelineContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new BookStepResult
        {
            Success = true,
            ProcessedChapterIds = context.ChapterIds.ToArray(),
        });
    }
}
