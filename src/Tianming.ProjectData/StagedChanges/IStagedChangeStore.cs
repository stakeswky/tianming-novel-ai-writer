using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.StagedChanges;

public interface IStagedChangeStore
{
    Task<string> StageAsync(StagedChange change, CancellationToken ct = default);

    Task<StagedChange?> GetAsync(string id, CancellationToken ct = default);

    Task<IReadOnlyList<StagedChange>> ListPendingAsync(CancellationToken ct = default);

    Task RemoveAsync(string id, CancellationToken ct = default);
}
