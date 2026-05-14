using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations.Tracking.Debts
{
    public sealed class TrackingDebtRegistry
    {
        private readonly IReadOnlyList<ITrackingDebtDetector> _detectors;

        public TrackingDebtRegistry(IEnumerable<ITrackingDebtDetector> detectors)
        {
            _detectors = detectors.ToList();
        }

        public IReadOnlyList<TrackingDebtCategory> SupportedCategories =>
            _detectors.Select(detector => detector.Category).Distinct().ToList();

        public async Task<IReadOnlyList<TrackingDebt>> DetectAllAsync(
            string chapterId,
            ChapterChanges currentChanges,
            FactSnapshot previousSnapshot,
            TrackingDebtDetectionContext context,
            CancellationToken ct = default)
        {
            var allDebts = new List<TrackingDebt>();
            foreach (var detector in _detectors)
            {
                ct.ThrowIfCancellationRequested();
                var debts = await detector
                    .DetectAsync(chapterId, currentChanges, previousSnapshot, context, ct)
                    .ConfigureAwait(false);
                if (debts.Count > 0)
                    allDebts.AddRange(debts);
            }

            return allDebts;
        }
    }
}
