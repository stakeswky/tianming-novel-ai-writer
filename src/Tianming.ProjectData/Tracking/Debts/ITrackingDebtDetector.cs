using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Guides;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations.Tracking.Debts
{
    /// <summary>
    /// Detects one category of tracking debt using the current chapter changes,
    /// the pre-chapter snapshot, and optional guide context.
    /// </summary>
    public interface ITrackingDebtDetector
    {
        TrackingDebtCategory Category { get; }

        Task<IReadOnlyList<TrackingDebt>> DetectAsync(
            string chapterId,
            ChapterChanges currentChanges,
            FactSnapshot previousSnapshot,
            TrackingDebtDetectionContext context,
            CancellationToken ct = default);
    }

    public sealed class TrackingDebtDetectionContext
    {
        public ForeshadowingStatusGuide? Foreshadowings { get; set; }

        public PledgeGuide? Pledges { get; set; }

        public SecretGuide? Secrets { get; set; }

        public Func<TrackingDebtCategory, string> IdGenerator { get; set; }
            = category => $"{category}-{Guid.NewGuid():N}"[..24];
    }
}
