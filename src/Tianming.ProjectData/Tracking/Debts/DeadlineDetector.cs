using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations.Tracking.Debts
{
    public sealed class DeadlineDetector : ITrackingDebtDetector
    {
        public TrackingDebtCategory Category => TrackingDebtCategory.Deadline;

        public Task<IReadOnlyList<TrackingDebt>> DetectAsync(
            string chapterId,
            ChapterChanges currentChanges,
            FactSnapshot previousSnapshot,
            TrackingDebtDetectionContext context,
            CancellationToken ct = default)
        {
            var debts = new List<TrackingDebt>();
            if (context.Foreshadowings == null)
                return Task.FromResult<IReadOnlyList<TrackingDebt>>(debts);

            foreach (var (id, entry) in context.Foreshadowings.Foreshadowings)
            {
                ct.ThrowIfCancellationRequested();
                if (entry.IsResolved || !entry.IsOverdue)
                    continue;

                debts.Add(new TrackingDebt
                {
                    Id = context.IdGenerator(TrackingDebtCategory.Deadline),
                    Category = TrackingDebtCategory.Deadline,
                    ChapterId = chapterId,
                    EntityId = id,
                    Description = $"伏笔「{entry.Name}」已过预期 payoff 章 {entry.ExpectedPayoffChapter}，仍未 resolve",
                    Severity = TrackingDebtSeverity.High,
                    DetectedAtChapter = chapterId,
                    EvidenceJson = JsonSerializer.Serialize(new
                    {
                        expectedPayoff = entry.ExpectedPayoffChapter,
                        entry.IsResolved,
                        entry.IsOverdue,
                    }),
                });
            }

            return Task.FromResult<IReadOnlyList<TrackingDebt>>(debts);
        }
    }
}
