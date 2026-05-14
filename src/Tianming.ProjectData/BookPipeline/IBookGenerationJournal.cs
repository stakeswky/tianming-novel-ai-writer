using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.BookPipeline;

public interface IBookGenerationJournal
{
    Task<bool> IsCompletedAsync(string stepName, CancellationToken ct = default);

    Task<bool> IsSkippedAsync(string stepName, CancellationToken ct = default);

    Task RecordCompletedAsync(string stepName, CancellationToken ct = default);

    Task MarkSkippedAsync(string stepName, CancellationToken ct = default);

    Task ResetAsync(string stepName, CancellationToken ct = default);
}
