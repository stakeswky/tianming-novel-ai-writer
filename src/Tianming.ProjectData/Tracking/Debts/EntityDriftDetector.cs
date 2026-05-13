using System;
using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations.Tracking.Debts
{
    public sealed class EntityDriftDetector : ITrackingDebtDetector
    {
        private static readonly JsonSerializerOptions EvidenceJsonOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private static readonly HashSet<string> MonitoredFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "HairColor",
            "EyeColor",
            "Appearance",
        };

        public TrackingDebtCategory Category => TrackingDebtCategory.EntityDrift;

        public Task<IReadOnlyList<TrackingDebt>> DetectAsync(
            string chapterId,
            ChapterChanges currentChanges,
            FactSnapshot previousSnapshot,
            TrackingDebtDetectionContext context,
            CancellationToken ct = default)
        {
            var debts = new List<TrackingDebt>();
            if (currentChanges?.CharacterStateChanges == null || previousSnapshot == null)
                return Task.FromResult<IReadOnlyList<TrackingDebt>>(debts);

            foreach (var change in currentChanges.CharacterStateChanges)
            {
                ct.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(change.CharacterId) ||
                    change.FieldChanges == null ||
                    change.FieldChanges.Count == 0 ||
                    !previousSnapshot.CharacterDescriptions.TryGetValue(change.CharacterId, out var previousDescription))
                {
                    continue;
                }

                foreach (var (field, newValue) in change.FieldChanges)
                {
                    if (!MonitoredFields.Contains(field))
                        continue;

                    var oldValue = field switch
                    {
                        "HairColor" => previousDescription.HairColor,
                        "EyeColor" => previousDescription.EyeColor,
                        "Appearance" => previousDescription.Appearance,
                        _ => string.Empty,
                    };

                    if (string.IsNullOrWhiteSpace(oldValue) ||
                        string.Equals(oldValue, newValue, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    debts.Add(new TrackingDebt
                    {
                        Id = context.IdGenerator(TrackingDebtCategory.EntityDrift),
                        Category = TrackingDebtCategory.EntityDrift,
                        ChapterId = chapterId,
                        EntityId = change.CharacterId,
                        Description = $"角色 {previousDescription.Name} 的 {field} 由「{oldValue}」变为「{newValue}」",
                        Severity = TrackingDebtSeverity.High,
                        DetectedAtChapter = chapterId,
                        EvidenceJson = JsonSerializer.Serialize(new { field, old = oldValue, @new = newValue }, EvidenceJsonOptions),
                    });
                }
            }

            return Task.FromResult<IReadOnlyList<TrackingDebt>>(debts);
        }
    }
}
