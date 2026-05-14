using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.Generation.Wal;

public sealed class GenerationRecoveryService
{
    public delegate Task ReplayHandler(string chapterId, GenerationStep lastStep, CancellationToken ct);

    private readonly System.Func<IGenerationJournal> _journalFactory;
    private readonly ReplayHandler _handler;

    public GenerationRecoveryService(IGenerationJournal journal, ReplayHandler handler)
        : this(() => journal, handler)
    {
    }

    public GenerationRecoveryService(System.Func<IGenerationJournal> journalFactory, ReplayHandler handler)
    {
        _journalFactory = journalFactory;
        _handler = handler;
    }

    public async Task<int> ReplayAsync(CancellationToken ct = default)
    {
        // Replay policy is owned by the handler. M6.3 startup only discovers pending
        // journals and hands the latest step back to the caller.
        var journal = _journalFactory();
        var pending = await journal.ListPendingAsync(ct).ConfigureAwait(false);
        var discovered = 0;
        foreach (var chapterId in pending)
        {
            var entries = await journal.ReadAllAsync(chapterId, ct).ConfigureAwait(false);
            if (entries.Count == 0)
                continue;

            await _handler(chapterId, entries[^1].Step, ct).ConfigureAwait(false);
            discovered++;
        }

        return discovered;
    }
}
