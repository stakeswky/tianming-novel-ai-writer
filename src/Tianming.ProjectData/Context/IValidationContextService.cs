using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Context;

public interface IValidationContextService
{
    Task<ValidationContextBundle> BuildAsync(string chapterId, CancellationToken ct = default);
}

public sealed class ValidationContextBundle
{
    public string ChapterId { get; set; } = string.Empty;

    public LedgerRuleSet RuleSet { get; set; } = new();

    public FactSnapshot FactSnapshot { get; set; } = new();
}
