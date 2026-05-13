using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations.Tracking.Debts
{
    public sealed class OmissionDetector : ITrackingDebtDetector
    {
        public TrackingDebtCategory Category => TrackingDebtCategory.Omission;

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
                if (entry.IsSetup ||
                    string.IsNullOrWhiteSpace(entry.ExpectedSetupChapter) ||
                    !string.Equals(entry.ExpectedSetupChapter, chapterId, StringComparison.OrdinalIgnoreCase) ||
                    !string.IsNullOrWhiteSpace(entry.ActualSetupChapter))
                {
                    continue;
                }

                debts.Add(new TrackingDebt
                {
                    Id = context.IdGenerator(TrackingDebtCategory.Omission),
                    Category = TrackingDebtCategory.Omission,
                    ChapterId = chapterId,
                    EntityId = id,
                    Description = $"伏笔「{entry.Name}」预期在 {entry.ExpectedSetupChapter} 埋设，但该章仍未埋设",
                    Severity = TrackingDebtSeverity.Medium,
                    DetectedAtChapter = chapterId,
                    EvidenceJson = JsonSerializer.Serialize(new
                    {
                        expectedSetup = entry.ExpectedSetupChapter,
                        actualSetup = entry.ActualSetupChapter,
                    }),
                });
            }

            return Task.FromResult<IReadOnlyList<TrackingDebt>>(debts);
        }
    }
}
