using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Generation.Wal;

public interface IGenerationJournal
{
    Task AppendAsync(GenerationJournalEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<GenerationJournalEntry>> ReadAllAsync(string chapterId, CancellationToken ct = default);

    Task<IReadOnlyList<string>> ListPendingAsync(CancellationToken ct = default);

    Task ClearAsync(string chapterId, CancellationToken ct = default);
}
