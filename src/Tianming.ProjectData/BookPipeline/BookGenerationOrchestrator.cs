using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.BookPipeline;

public sealed class BookGenerationOrchestrator
{
    private readonly IReadOnlyList<IBookPipelineStep> _steps;
    private readonly IBookGenerationJournal _journal;

    public BookGenerationOrchestrator(IEnumerable<IBookPipelineStep> steps, IBookGenerationJournal journal)
    {
        _steps = new List<IBookPipelineStep>(steps);
        _journal = journal;
    }

    public async Task<BookOrchestratorResult> RunAsync(BookPipelineContext context, CancellationToken ct = default)
    {
        foreach (var step in _steps)
        {
            ct.ThrowIfCancellationRequested();

            if (await _journal.IsCompletedAsync(step.Name, ct).ConfigureAwait(false))
                continue;

            if (await _journal.IsSkippedAsync(step.Name, ct).ConfigureAwait(false))
                continue;

            var result = await step.ExecuteAsync(context, ct).ConfigureAwait(false);
            if (!result.Success)
            {
                return new BookOrchestratorResult
                {
                    Success = false,
                    FailedStepName = step.Name,
                    ErrorMessage = result.ErrorMessage ?? "(no message)",
                };
            }

            await _journal.RecordCompletedAsync(step.Name, ct).ConfigureAwait(false);
        }

        return new BookOrchestratorResult { Success = true };
    }
}

public sealed class BookOrchestratorResult
{
    public bool Success { get; set; }

    public string? FailedStepName { get; set; }

    public string? ErrorMessage { get; set; }
}
