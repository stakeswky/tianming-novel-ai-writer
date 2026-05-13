using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations.Tracking.Debts
{
    public sealed class PledgeDetector : ITrackingDebtDetector
    {
        private static readonly Regex ChapterPattern = new(@"(?:vol|v)(\d+)_(?:ch|c)(\d+)|^(\d+)_(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public TrackingDebtCategory Category => TrackingDebtCategory.Pledge;

        public Task<IReadOnlyList<TrackingDebt>> DetectAsync(
            string chapterId,
            ChapterChanges currentChanges,
            FactSnapshot previousSnapshot,
            TrackingDebtDetectionContext context,
            CancellationToken ct = default)
        {
            var debts = new List<TrackingDebt>();
            if (context.Pledges == null)
                return Task.FromResult<IReadOnlyList<TrackingDebt>>(debts);

            foreach (var (id, pledge) in context.Pledges.Pledges)
            {
                ct.ThrowIfCancellationRequested();
                if (pledge.IsFulfilled || string.IsNullOrWhiteSpace(pledge.DeadlineChapter))
                    continue;

                if (CompareChapterIds(chapterId, pledge.DeadlineChapter) <= 0)
                    continue;

                debts.Add(new TrackingDebt
                {
                    Id = context.IdGenerator(TrackingDebtCategory.Pledge),
                    Category = TrackingDebtCategory.Pledge,
                    ChapterId = chapterId,
                    EntityId = id,
                    Description = $"承诺「{pledge.Name}」已过 deadline {pledge.DeadlineChapter}，仍未兑现",
                    Severity = TrackingDebtSeverity.High,
                    DetectedAtChapter = chapterId,
                    EvidenceJson = JsonSerializer.Serialize(new
                    {
                        promisedAt = pledge.PromisedAtChapter,
                        deadline = pledge.DeadlineChapter,
                        pledge.IsFulfilled,
                    }),
                });
            }

            return Task.FromResult<IReadOnlyList<TrackingDebt>>(debts);
        }

        private static int CompareChapterIds(string left, string right)
        {
            var leftParts = ParseChapterId(left);
            var rightParts = ParseChapterId(right);
            var volumeCompare = leftParts.Volume.CompareTo(rightParts.Volume);
            return volumeCompare != 0 ? volumeCompare : leftParts.Chapter.CompareTo(rightParts.Chapter);
        }

        private static (int Volume, int Chapter) ParseChapterId(string chapterId)
        {
            var match = ChapterPattern.Match(chapterId ?? string.Empty);
            if (!match.Success)
                return (0, 0);

            var volumeText = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
            var chapterText = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value;
            return (int.Parse(volumeText), int.Parse(chapterText));
        }
    }
}
