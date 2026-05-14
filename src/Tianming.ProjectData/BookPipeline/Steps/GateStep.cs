using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.BookPipeline.Steps;

public sealed class GateStep : IBookPipelineStep
{
    private readonly GenerationGate? _gate;

    public GateStep()
    {
    }

    public GateStep(GenerationGate gate)
    {
        _gate = gate;
    }

    public string Name => BookPipelineStepName.Gate;

    public async Task<BookStepResult> ExecuteAsync(BookPipelineContext context, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (_gate == null || context.ChapterIds.Count == 0)
            return new BookStepResult { Success = true, ProcessedChapterIds = context.ChapterIds.ToArray() };

        var processed = new List<string>();
        foreach (var chapterId in context.ChapterIds)
        {
            ct.ThrowIfCancellationRequested();

            if (!context.Scratchpad.TryGetValue($"gate.raw.{chapterId}", out var rawContent))
                continue;

            var snapshot = TryReadFactSnapshot(context, $"gate.snapshot.{chapterId}");
            var result = await _gate.ValidateAsync(chapterId, rawContent, snapshot).ConfigureAwait(false);
            context.Scratchpad[$"gate.result.{chapterId}"] = result.Success
                ? "Success"
                : string.Join("; ", result.GetTopFailures(5));

            if (!result.Success)
            {
                return new BookStepResult
                {
                    Success = false,
                    ErrorMessage = result.GetTopFailures(5).FirstOrDefault() ?? "Gate step failed.",
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
