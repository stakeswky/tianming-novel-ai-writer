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
    private readonly ProjectContextDataBuilder? _builder;

    public ValidationContextService(RuleSetProvider ruleSetProvider, FactSnapshotProvider snapshotProvider)
    {
        _ruleSetProvider = ruleSetProvider;
        _snapshotProvider = snapshotProvider;
    }

    public ValidationContextService(
        string projectRoot,
        TM.Services.Modules.ProjectData.Modules.Schema.ModuleDataAdapter<TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning.ChapterCategory, TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning.ChapterData> chapterAdapter,
        TM.Services.Modules.ProjectData.Modules.Schema.ModuleDataAdapter<TM.Services.Modules.ProjectData.Models.Design.Characters.CharacterRulesCategory, TM.Services.Modules.ProjectData.Models.Design.Characters.CharacterRulesData> characterAdapter,
        TM.Services.Modules.ProjectData.Modules.Schema.ModuleDataAdapter<TM.Services.Modules.ProjectData.Models.Design.Factions.FactionRulesCategory, TM.Services.Modules.ProjectData.Models.Design.Factions.FactionRulesData> factionAdapter,
        TM.Services.Modules.ProjectData.Modules.Schema.ModuleDataAdapter<TM.Services.Modules.ProjectData.Models.Design.Location.LocationRulesCategory, TM.Services.Modules.ProjectData.Models.Design.Location.LocationRulesData> locationAdapter,
        TM.Services.Modules.ProjectData.Modules.Schema.ModuleDataAdapter<TM.Services.Modules.ProjectData.Models.Design.Worldview.WorldRulesCategory, TM.Services.Modules.ProjectData.Models.Design.Worldview.WorldRulesData> worldRuleAdapter,
        LedgerRuleSetProvider ledgerRuleSetProvider)
        : this(
            new ProjectContextDataBuilder(
                projectRoot,
                chapterAdapter,
                characterAdapter,
                factionAdapter,
                locationAdapter,
                worldRuleAdapter: worldRuleAdapter),
            ledgerRuleSetProvider)
    {
    }

    public ValidationContextService(ProjectContextDataBuilder builder, LedgerRuleSetProvider ledgerRuleSetProvider)
    {
        _builder = builder;
        _ruleSetProvider = ct => Task.FromResult(ledgerRuleSetProvider.GetRuleSetForGate());
        _snapshotProvider = (chapterId, ct) => _builder.BuildFactSnapshotAsync(chapterId, ct);
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
