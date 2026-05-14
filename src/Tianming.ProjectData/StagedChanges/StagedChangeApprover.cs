using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Modules.ProjectData.StagedChanges;

public sealed class StagedChangeApprover : IStagedChangeApprover
{
    public delegate Task ContentApplyHandler(string chapterId, string newContent, CancellationToken ct);
    public delegate Task DataApplyHandler(string category, string dataId, string dataJson, CancellationToken ct);
    public delegate Task WorkspaceApplyHandler(string relativePath, string newContent, CancellationToken ct);

    private readonly IStagedChangeStore _store;
    private readonly ContentApplyHandler _content;
    private readonly DataApplyHandler _data;
    private readonly WorkspaceApplyHandler _workspace;

    public StagedChangeApprover(
        IStagedChangeStore store,
        ContentApplyHandler content,
        DataApplyHandler data,
        WorkspaceApplyHandler workspace)
    {
        _store = store;
        _content = content;
        _data = data;
        _workspace = workspace;
    }

    public async Task<bool> ApproveAsync(string changeId, CancellationToken ct = default)
    {
        var change = await _store.GetAsync(changeId, ct).ConfigureAwait(false);
        if (change == null)
        {
            return false;
        }

        switch (change.ChangeType)
        {
            case StagedChangeType.ContentEdit:
                await _content(change.TargetId, change.NewContentSnippet, ct).ConfigureAwait(false);
                break;
            case StagedChangeType.DataEdit:
                var parts = change.TargetId.Split(':', 2);
                if (parts.Length != 2)
                {
                    return false;
                }

                await _data(parts[0], parts[1], change.PayloadJson, ct).ConfigureAwait(false);
                break;
            case StagedChangeType.WorkspaceEdit:
                await _workspace(change.TargetId, change.NewContentSnippet, ct).ConfigureAwait(false);
                break;
            default:
                return false;
        }

        await _store.RemoveAsync(changeId, ct).ConfigureAwait(false);
        return true;
    }

    public async Task<bool> RejectAsync(string changeId, CancellationToken ct = default)
    {
        var change = await _store.GetAsync(changeId, ct).ConfigureAwait(false);
        if (change == null)
        {
            return false;
        }

        await _store.RemoveAsync(changeId, ct).ConfigureAwait(false);
        return true;
    }
}
