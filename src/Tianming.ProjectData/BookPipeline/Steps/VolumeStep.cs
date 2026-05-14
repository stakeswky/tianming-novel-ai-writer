using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.BookPipeline.Steps;

public sealed class VolumeStep : IBookPipelineStep
{
    public string Name => BookPipelineStepName.Volume;

    public Task<BookStepResult> ExecuteAsync(BookPipelineContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(new BookStepResult { Success = true });
    }
}
