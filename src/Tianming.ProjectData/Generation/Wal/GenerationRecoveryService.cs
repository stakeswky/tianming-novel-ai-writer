using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Generation.Wal;

public sealed class GenerationRecoveryService
{
    public delegate Task ReplayHandler(string chapterId, GenerationStep lastStep, CancellationToken ct);

    private readonly IGenerationJournal _journal;
    private readonly ReplayHandler _handler;

    public GenerationRecoveryService(IGenerationJournal journal, ReplayHandler handler)
    {
        _journal = journal;
        _handler = handler;
    }

    public async Task<int> ReplayAsync(CancellationToken ct = default)
    {
        var pending = await _journal.ListPendingAsync(ct).ConfigureAwait(false);
        var replayed = 0;
        foreach (var chapterId in pending)
        {
            var entries = await _journal.ReadAllAsync(chapterId, ct).ConfigureAwait(false);
            if (entries.Count == 0)
                continue;

            await _handler(chapterId, entries[^1].Step, ct).ConfigureAwait(false);
            replayed++;
        }

        return replayed;
    }
}
