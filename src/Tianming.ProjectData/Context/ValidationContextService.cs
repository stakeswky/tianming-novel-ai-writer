using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Context;

public sealed class ValidationContextService : IValidationContextService
{
    public delegate Task<LedgerRuleSet> RuleSetProvider(CancellationToken ct);

    public delegate Task<FactSnapshot> FactSnapshotProvider(string chapterId, CancellationToken ct);

    private readonly RuleSetProvider _ruleSetProvider;
    private readonly FactSnapshotProvider _snapshotProvider;

    public ValidationContextService(RuleSetProvider ruleSetProvider, FactSnapshotProvider snapshotProvider)
    {
        _ruleSetProvider = ruleSetProvider;
        _snapshotProvider = snapshotProvider;
    }

    public async Task<ValidationContextBundle> BuildAsync(string chapterId, CancellationToken ct = default)
    {
        var rules = await _ruleSetProvider(ct).ConfigureAwait(false);
        var snapshot = await _snapshotProvider(chapterId, ct).ConfigureAwait(false);

        return new ValidationContextBundle
        {
            ChapterId = chapterId,
            RuleSet = rules,
            FactSnapshot = snapshot,
        };
    }
}
