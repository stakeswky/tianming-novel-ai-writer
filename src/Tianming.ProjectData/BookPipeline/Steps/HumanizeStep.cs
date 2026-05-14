using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Humanize;

namespace TM.Services.Modules.ProjectData.BookPipeline.Steps;

public sealed class HumanizeStep : IBookPipelineStep
{
    private readonly HumanizePipeline? _pipeline;

    public HumanizeStep()
    {
    }

    public HumanizeStep(HumanizePipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public string Name => BookPipelineStepName.Humanize;

    public async Task<BookStepResult> ExecuteAsync(BookPipelineContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_pipeline == null || context.ChapterIds.Count == 0)
            return new BookStepResult { Success = true, ProcessedChapterIds = context.ChapterIds.ToArray() };

        var processed = new List<string>();
        foreach (var chapterId in context.ChapterIds)
        {
            ct.ThrowIfCancellationRequested();

            if (!context.Scratchpad.TryGetValue($"humanize.input.{chapterId}", out var input))
                continue;

            var output = await _pipeline.RunAsync(
                input,
                new HumanizeContext { ChapterId = chapterId, InputText = input },
                ct).ConfigureAwait(false);

            context.Scratchpad[$"humanize.output.{chapterId}"] = output;
            processed.Add(chapterId);
        }

        return new BookStepResult { Success = true, ProcessedChapterIds = processed };
    }
}
