using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations.Tracking.Debts
{
    public sealed class SecretRevealDetector : ITrackingDebtDetector
    {
        private static readonly Regex ChapterPattern = new(@"(?:vol|v)(\d+)_(?:ch|c)(\d+)|^(\d+)_(\d+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public TrackingDebtCategory Category => TrackingDebtCategory.SecretReveal;

        public Task<IReadOnlyList<TrackingDebt>> DetectAsync(
            string chapterId,
            ChapterChanges currentChanges,
            FactSnapshot previousSnapshot,
            TrackingDebtDetectionContext context,
            CancellationToken ct = default)
        {
            var debts = new List<TrackingDebt>();
            if (context.Secrets == null)
                return Task.FromResult<IReadOnlyList<TrackingDebt>>(debts);

            foreach (var (id, secret) in context.Secrets.Secrets)
            {
                ct.ThrowIfCancellationRequested();
                if (!secret.IsRevealed ||
                    string.IsNullOrWhiteSpace(secret.ExpectedRevealChapter) ||
                    string.IsNullOrWhiteSpace(secret.ActualRevealChapter))
                {
                    continue;
                }

                if (CompareChapterIds(secret.ActualRevealChapter, secret.ExpectedRevealChapter) >= 0)
                    continue;

                debts.Add(new TrackingDebt
                {
                    Id = context.IdGenerator(TrackingDebtCategory.SecretReveal),
                    Category = TrackingDebtCategory.SecretReveal,
                    ChapterId = chapterId,
                    EntityId = id,
                    Description = $"秘密「{secret.Name}」在 {secret.ActualRevealChapter} 被提前揭露，预期 {secret.ExpectedRevealChapter}",
                    Severity = TrackingDebtSeverity.Medium,
                    DetectedAtChapter = chapterId,
                    EvidenceJson = JsonSerializer.Serialize(new
                    {
                        expected = secret.ExpectedRevealChapter,
                        actual = secret.ActualRevealChapter,
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
