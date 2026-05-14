using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.BookPipeline;

public interface IBookPipelineStep
{
    string Name { get; }

    Task<BookStepResult> ExecuteAsync(BookPipelineContext context, CancellationToken ct = default);
}

public sealed class BookPipelineContext
{
    public string ProjectRoot { get; set; } = string.Empty;

    public List<string> ChapterIds { get; set; } = new();

    public Dictionary<string, string> Scratchpad { get; set; } = new();
}

public sealed class BookStepResult
{
    public bool Success { get; set; }

    public string? ErrorMessage { get; set; }

    public IReadOnlyList<string> ProcessedChapterIds { get; set; } = Array.Empty<string>();

    public string? PayloadJson { get; set; }
}
