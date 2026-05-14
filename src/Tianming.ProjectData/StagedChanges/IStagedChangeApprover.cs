using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.StagedChanges;

public interface IStagedChangeApprover
{
    Task<bool> ApproveAsync(string changeId, CancellationToken ct = default);

    Task<bool> RejectAsync(string changeId, CancellationToken ct = default);
}
