using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Rules;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Tracking.Layers;

/// <summary>
/// 单层校验：负责一类一致性规则（结构 / 实体 / 伏笔 / 时间线 / 关系）。
/// </summary>
public interface IConsistencyLayer
{
    /// <summary>层名（"Structural" / "Entity" / "Foreshadow" / "Timeline" / "Relationship"）。</summary>
    string LayerName { get; }

    Task<IReadOnlyList<ConsistencyIssue>> CheckAsync(
        ChapterChanges changes,
        FactSnapshot factSnapshot,
        LedgerRuleSet ruleSet,
        CancellationToken ct = default);
}
