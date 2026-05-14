using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.BookPipeline.Steps;

public sealed class GenerateStep : IBookPipelineStep
{
    private readonly ChapterGenerationPipeline? _pipeline;

    public GenerateStep()
    {
    }

    public GenerateStep(ChapterGenerationPipeline pipeline)
    {
        _pipeline = pipeline;
    }

    public string Name => BookPipelineStepName.Generate;

    public async Task<BookStepResult> ExecuteAsync(BookPipelineContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_pipeline == null || context.ChapterIds.Count == 0)
            return new BookStepResult { Success = true, ProcessedChapterIds = context.ChapterIds.ToArray() };

        var processed = new List<string>();
        foreach (var chapterId in context.ChapterIds)
        {
            ct.ThrowIfCancellationRequested();

            if (!context.Scratchpad.TryGetValue($"generate.raw.{chapterId}", out var rawContent))
                continue;

            var factSnapshot = TryReadFactSnapshot(context, $"generate.snapshot.{chapterId}");
            var result = await _pipeline
                .SaveGeneratedChapterStrictAsync(chapterId, rawContent, factSnapshot)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                return new BookStepResult
                {
                    Success = false,
                    ErrorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage) ? "Generate step failed." : result.ErrorMessage,
                    ProcessedChapterIds = processed,
                };
            }

            processed.Add(chapterId);
        }

        return new BookStepResult { Success = true, ProcessedChapterIds = processed };
    }

    private static FactSnapshot TryReadFactSnapshot(BookPipelineContext context, string key)
    {
        if (!context.Scratchpad.TryGetValue(key, out var json) || string.IsNullOrWhiteSpace(json))
            return new FactSnapshot();

        return JsonSerializer.Deserialize<FactSnapshot>(json) ?? new FactSnapshot();
    }
}
