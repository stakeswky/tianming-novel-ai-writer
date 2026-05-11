using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Models.Design.Location;
using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Models.Guides;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public interface IFactSnapshotGuideSource
    {
        Task<CharacterStateGuide> GetCharacterStateGuideAsync(bool allVolumes, CancellationToken cancellationToken = default);

        Task<ConflictProgressGuide> GetConflictProgressGuideAsync(bool allVolumes, CancellationToken cancellationToken = default);

        Task<ForeshadowingStatusGuide> GetForeshadowingStatusGuideAsync(CancellationToken cancellationToken = default);

        Task<LocationStateGuide> GetLocationStateGuideAsync(bool allVolumes, CancellationToken cancellationToken = default);

        Task<FactionStateGuide> GetFactionStateGuideAsync(bool allVolumes, CancellationToken cancellationToken = default);

        Task<TimelineGuide> GetTimelineGuideAsync(bool allVolumes, CancellationToken cancellationToken = default);

        Task<ItemStateGuide> GetItemStateGuideAsync(bool allVolumes, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PlotPointEntry>> GetPlotPointsAsync(
            string currentChapterId,
            IReadOnlyCollection<string> characterIds,
            IReadOnlyCollection<string> otherEntityIds,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<CharacterRulesData>> GetCharactersAsync(
            IReadOnlyCollection<string> characterIds,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<LocationRulesData>> GetLocationsAsync(
            IReadOnlyCollection<string> locationIds,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<WorldRulesData>> GetWorldRulesAsync(
            IReadOnlyCollection<string> worldRuleIds,
            CancellationToken cancellationToken = default);
    }

    public sealed class PortableFactSnapshotExtractorOptions
    {
        public int ActiveEntityWindowChapters { get; init; } = 8;
        public int ActiveEntityWindowMaxCount { get; init; } = 25;
        public int ChaptersPerVolume { get; init; } = 20;
        public int MaxFactionStates { get; init; } = 30;
        public int MaxItemStates { get; init; } = 50;
        public int MaxTimelineEntries { get; init; } = 5;
        public int MaxPlotPoints { get; init; } = 15;
    }

    public sealed class PortableFactSnapshotExtractor
    {
        private readonly IFactSnapshotGuideSource _source;
        private readonly PortableFactSnapshotExtractorOptions _options;

        public PortableFactSnapshotExtractor(
            IFactSnapshotGuideSource source,
            PortableFactSnapshotExtractorOptions? options = null)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _options = options ?? new PortableFactSnapshotExtractorOptions();
        }

        public async Task<FactSnapshot> ExtractSnapshotAsync(
            string chapterId,
            IReadOnlyCollection<string>? characterIds,
            IReadOnlyCollection<string>? locationIds,
            IReadOnlyCollection<string>? conflictIds,
            IReadOnlyCollection<string>? foreshadowingSetupIds,
            IReadOnlyCollection<string>? foreshadowingPayoffIds,
            IReadOnlyCollection<string>? worldRuleIds,
            IReadOnlyCollection<string>? factionIds = null,
            CancellationToken cancellationToken = default)
        {
            var previousChapterId = GetPreviousChapterId(chapterId);
            var snapshot = new FactSnapshot
            {
                CharacterStates = await ExtractCharacterStatesAsync(characterIds, previousChapterId, allVolumes: false, cancellationToken).ConfigureAwait(false),
                ConflictProgress = await ExtractConflictProgressAsync(conflictIds, previousChapterId, allVolumes: false, cancellationToken).ConfigureAwait(false),
                ForeshadowingStatus = await ExtractForeshadowingStatusAsync(foreshadowingSetupIds, foreshadowingPayoffIds, cancellationToken).ConfigureAwait(false),
                LocationStates = await ExtractLocationStatesAsync(locationIds, previousChapterId, allVolumes: false, cancellationToken).ConfigureAwait(false),
                FactionStates = await ExtractFactionStatesAsync(factionIds, previousChapterId, allVolumes: false, cancellationToken).ConfigureAwait(false),
                Timeline = await ExtractTimelineAsync(previousChapterId, allVolumes: false, cancellationToken).ConfigureAwait(false),
                CharacterLocations = await ExtractCharacterLocationsAsync(characterIds, previousChapterId, allVolumes: false, cancellationToken).ConfigureAwait(false),
                ItemStates = await ExtractItemStatesAsync(characterIds, previousChapterId, allVolumes: false, cancellationToken).ConfigureAwait(false),
                PlotPoints = await ExtractPlotPointsAsync(chapterId, previousChapterId, characterIds, conflictIds, foreshadowingSetupIds, foreshadowingPayoffIds, cancellationToken).ConfigureAwait(false),
                CharacterDescriptions = await ExtractCharacterDescriptionsAsync(characterIds, cancellationToken).ConfigureAwait(false),
                LocationDescriptions = await ExtractLocationDescriptionsAsync(locationIds, cancellationToken).ConfigureAwait(false),
                WorldRuleConstraints = await ExtractWorldRuleConstraintsAsync(worldRuleIds, cancellationToken).ConfigureAwait(false)
            };

            return snapshot;
        }

        public async Task<FactSnapshot> ExtractVolumeEndSnapshotAsync(
            string chapterId,
            CancellationToken cancellationToken = default)
        {
            return new FactSnapshot
            {
                CharacterStates = await ExtractCharacterStatesAsync(null, chapterId, allVolumes: true, cancellationToken).ConfigureAwait(false),
                ConflictProgress = await ExtractConflictProgressAsync(null, chapterId, allVolumes: true, cancellationToken).ConfigureAwait(false),
                ForeshadowingStatus = await ExtractForeshadowingStatusAsync(null, null, cancellationToken, includeAll: true).ConfigureAwait(false),
                LocationStates = await ExtractLocationStatesAsync(null, chapterId, allVolumes: true, cancellationToken).ConfigureAwait(false),
                FactionStates = await ExtractFactionStatesAsync(null, chapterId, allVolumes: true, cancellationToken).ConfigureAwait(false),
                Timeline = await ExtractTimelineAsync(chapterId, allVolumes: true, cancellationToken).ConfigureAwait(false),
                CharacterLocations = await ExtractCharacterLocationsAsync(null, chapterId, allVolumes: true, cancellationToken).ConfigureAwait(false),
                ItemStates = await ExtractItemStatesAsync(null, chapterId, allVolumes: true, cancellationToken).ConfigureAwait(false)
            };
        }

        private async Task<List<PlotPointSnapshot>> ExtractPlotPointsAsync(
            string currentChapterId,
            string previousChapterId,
            IReadOnlyCollection<string>? characterIds,
            IReadOnlyCollection<string>? conflictIds,
            IReadOnlyCollection<string>? foreshadowingSetupIds,
            IReadOnlyCollection<string>? foreshadowingPayoffIds,
            CancellationToken cancellationToken)
        {
            var characterFilter = ToFilter(characterIds) ?? [];
            var otherEntityIds = (conflictIds ?? [])
                .Concat(foreshadowingSetupIds ?? [])
                .Concat(foreshadowingPayoffIds ?? [])
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (characterFilter.Count == 0 && otherEntityIds.Count == 0)
                return [];

            var candidates = await _source
                .GetPlotPointsAsync(currentChapterId, characterFilter, otherEntityIds, cancellationToken)
                .ConfigureAwait(false);

            return candidates
                .Where(point => !string.IsNullOrWhiteSpace(point.Context))
                .Where(point => IsAtOrBefore(point.Chapter, previousChapterId))
                .Where(point => IsRelatedPlotPoint(point, characterFilter, otherEntityIds))
                .OrderByDescending(PlotPointImportanceScore)
                .ThenByDescending(point => point.Chapter, ChapterIdComparer.Instance)
                .Take(Math.Max(0, _options.MaxPlotPoints))
                .Select(point => new PlotPointSnapshot
                {
                    Id = point.Id,
                    Summary = point.Context,
                    ChapterId = point.Chapter,
                    RelatedEntityIds = point.InvolvedCharacters ?? [],
                    Storyline = point.Storyline
                })
                .ToList();
        }

        private async Task<Dictionary<string, CharacterCoreDescription>> ExtractCharacterDescriptionsAsync(
            IReadOnlyCollection<string>? characterIds,
            CancellationToken cancellationToken)
        {
            var filter = ToFilter(characterIds);
            if (filter == null)
                return new Dictionary<string, CharacterCoreDescription>(StringComparer.OrdinalIgnoreCase);

            var characters = await _source.GetCharactersAsync(filter, cancellationToken).ConfigureAwait(false);
            return characters
                .Where(character => !string.IsNullOrWhiteSpace(character.Id))
                .ToDictionary(
                    character => character.Id,
                    character => new CharacterCoreDescription
                    {
                        Id = character.Id,
                        Name = character.Name,
                        HairColor = ExtractHairColor(character.Appearance),
                        Appearance = character.Appearance,
                        PersonalityTags = ParseTags(string.Join(",", character.FlawBelief, character.Identity, character.Want))
                    },
                    StringComparer.OrdinalIgnoreCase);
        }

        private async Task<Dictionary<string, LocationCoreDescription>> ExtractLocationDescriptionsAsync(
            IReadOnlyCollection<string>? locationIds,
            CancellationToken cancellationToken)
        {
            var filter = ToFilter(locationIds);
            if (filter == null)
                return new Dictionary<string, LocationCoreDescription>(StringComparer.OrdinalIgnoreCase);

            var locations = await _source.GetLocationsAsync(filter, cancellationToken).ConfigureAwait(false);
            return locations
                .Where(location => !string.IsNullOrWhiteSpace(location.Id))
                .ToDictionary(
                    location => location.Id,
                    location => new LocationCoreDescription
                    {
                        Id = location.Id,
                        Name = location.Name,
                        Description = location.Description,
                        Features = ParseTags(string.Join(",", location.Description, location.Terrain, string.Join(",", location.Landmarks)))
                    },
                    StringComparer.OrdinalIgnoreCase);
        }

        private async Task<List<WorldRuleConstraint>> ExtractWorldRuleConstraintsAsync(
            IReadOnlyCollection<string>? worldRuleIds,
            CancellationToken cancellationToken)
        {
            var filter = ToFilter(worldRuleIds);
            if (filter == null)
                return [];

            var rules = await _source.GetWorldRulesAsync(filter, cancellationToken).ConfigureAwait(false);
            return rules
                .Where(rule => !string.IsNullOrWhiteSpace(rule.Id) && !string.IsNullOrWhiteSpace(rule.HardRules))
                .Select(rule => new WorldRuleConstraint
                {
                    RuleId = rule.Id,
                    RuleName = rule.Name,
                    Constraint = rule.HardRules,
                    IsHardConstraint = true
                })
                .ToList();
        }

        private async Task<List<CharacterStateSnapshot>> ExtractCharacterStatesAsync(
            IReadOnlyCollection<string>? characterIds,
            string targetChapterId,
            bool allVolumes,
            CancellationToken cancellationToken)
        {
            var guide = await _source.GetCharacterStateGuideAsync(allVolumes, cancellationToken).ConfigureAwait(false);
            var filter = ToFilter(characterIds);
            var result = new List<CharacterStateSnapshot>();
            var existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (id, entry) in guide.Characters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!allVolumes && (filter == null || !filter.Contains(id)))
                    continue;

                var state = LastAtOrBefore(entry.StateHistory, targetChapterId, state => state.Chapter);
                if (state == null)
                    continue;

                result.Add(ToCharacterStateSnapshot(id, entry, state));
                existingIds.Add(id);
            }

            if (!allVolumes)
            {
                var active = new List<CharacterStateSnapshot>();
                foreach (var (id, entry) in guide.Characters.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (existingIds.Contains(id))
                        continue;

                    var state = LastAtOrBefore(entry.StateHistory, targetChapterId, state => state.Chapter);
                    if (state == null || !IsActiveInRecentChapters(state.Chapter, targetChapterId))
                        continue;

                    active.Add(ToCharacterStateSnapshot(id, entry, state));
                }

                result.AddRange(active
                    .OrderByDescending(snapshot => snapshot.ChapterId, ChapterIdComparer.Instance)
                    .Take(Math.Max(0, _options.ActiveEntityWindowMaxCount)));
            }

            return result;
        }

        private async Task<List<ConflictProgressSnapshot>> ExtractConflictProgressAsync(
            IReadOnlyCollection<string>? conflictIds,
            string targetChapterId,
            bool allVolumes,
            CancellationToken cancellationToken)
        {
            var guide = await _source.GetConflictProgressGuideAsync(allVolumes, cancellationToken).ConfigureAwait(false);
            var filter = ToFilter(conflictIds);
            var result = new List<ConflictProgressSnapshot>();

            foreach (var (id, entry) in guide.Conflicts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (filter != null && !filter.Contains(id))
                    continue;

                var points = (entry.ProgressPoints ?? [])
                    .Where(point => IsAtOrBefore(point.Chapter, targetChapterId))
                    .OrderBy(point => point.Chapter, ChapterIdComparer.Instance)
                    .ToList();
                var latest = points.LastOrDefault();

                result.Add(new ConflictProgressSnapshot
                {
                    Id = id,
                    Name = entry.Name,
                    Status = !string.IsNullOrWhiteSpace(latest?.Status) ? latest!.Status : entry.Status,
                    RecentProgress = points
                        .Where(point => !string.IsNullOrWhiteSpace(point.Event))
                        .TakeLast(10)
                        .Select(point => $"{point.Chapter}: {point.Event}")
                        .ToList()
                });
            }

            return result;
        }

        private async Task<List<ForeshadowingStatusSnapshot>> ExtractForeshadowingStatusAsync(
            IReadOnlyCollection<string>? setupIds,
            IReadOnlyCollection<string>? payoffIds,
            CancellationToken cancellationToken,
            bool includeAll = false)
        {
            var guide = await _source.GetForeshadowingStatusGuideAsync(cancellationToken).ConfigureAwait(false);
            var selected = includeAll
                ? guide.Foreshadowings.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase)
                : (setupIds ?? []).Concat(payoffIds ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var result = new List<ForeshadowingStatusSnapshot>();

            foreach (var id in selected.Order(StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!guide.Foreshadowings.TryGetValue(id, out var entry))
                    continue;

                result.Add(new ForeshadowingStatusSnapshot
                {
                    Id = id,
                    Name = entry.Name,
                    IsSetup = entry.IsSetup,
                    IsResolved = entry.IsResolved,
                    IsOverdue = entry.IsOverdue,
                    SetupChapterId = entry.ActualSetupChapter,
                    PayoffChapterId = entry.ActualPayoffChapter
                });
            }

            return result;
        }

        private async Task<List<LocationStateSnapshot>> ExtractLocationStatesAsync(
            IReadOnlyCollection<string>? locationIds,
            string targetChapterId,
            bool allVolumes,
            CancellationToken cancellationToken)
        {
            var guide = await _source.GetLocationStateGuideAsync(allVolumes, cancellationToken).ConfigureAwait(false);
            var filter = ToFilter(locationIds);
            var result = new List<LocationStateSnapshot>();
            var existingIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var (id, entry) in guide.Locations.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!allVolumes && (filter == null || !filter.Contains(id)))
                    continue;

                var state = LastAtOrBefore(entry.StateHistory, targetChapterId, point => point.Chapter);
                if (state == null)
                    continue;

                result.Add(ToLocationStateSnapshot(id, entry, state));
                existingIds.Add(id);
            }

            if (!allVolumes)
            {
                var active = new List<LocationStateSnapshot>();
                foreach (var (id, entry) in guide.Locations.OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (existingIds.Contains(id))
                        continue;

                    var state = LastAtOrBefore(entry.StateHistory, targetChapterId, point => point.Chapter);
                    if (state == null || !IsActiveInRecentChapters(state.Chapter, targetChapterId))
                        continue;

                    active.Add(ToLocationStateSnapshot(id, entry, state));
                }

                result.AddRange(active
                    .OrderByDescending(snapshot => snapshot.ChapterId, ChapterIdComparer.Instance)
                    .Take(Math.Max(0, _options.ActiveEntityWindowMaxCount)));
            }

            return result;
        }

        private async Task<List<FactionStateSnapshot>> ExtractFactionStatesAsync(
            IReadOnlyCollection<string>? factionIds,
            string targetChapterId,
            bool allVolumes,
            CancellationToken cancellationToken)
        {
            var guide = await _source.GetFactionStateGuideAsync(allVolumes, cancellationToken).ConfigureAwait(false);
            var selectedIds = ToOrderedIds(factionIds);
            var selectedSet = selectedIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var activeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<FactionStateSnapshot>();

            foreach (var (id, entry) in guide.Factions.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var state = LastAtOrBefore(entry.StateHistory, targetChapterId, point => point.Chapter);
                if (state == null)
                    continue;

                result.Add(new FactionStateSnapshot
                {
                    Id = id,
                    Name = entry.Name,
                    Status = !string.IsNullOrWhiteSpace(state.Status) ? state.Status : entry.CurrentStatus,
                    ChapterId = state.Chapter
                });

                if (!allVolumes && IsActiveInRecentChapters(state.Chapter, targetChapterId))
                    activeIds.Add(id);
            }

            if (!allVolumes && result.Count > Math.Max(0, _options.MaxFactionStates))
            {
                var selected = selectedIds
                    .Select(id => result.FirstOrDefault(faction => string.Equals(faction.Id, id, StringComparison.OrdinalIgnoreCase)))
                    .Where(faction => faction != null)
                    .Cast<FactionStateSnapshot>()
                    .ToList();
                var remainingAfterSelected = Math.Max(0, _options.MaxFactionStates - selected.Count);
                var active = result
                    .Where(faction => !selectedSet.Contains(faction.Id) && activeIds.Contains(faction.Id))
                    .OrderByDescending(faction => faction.ChapterId, ChapterIdComparer.Instance)
                    .Take(remainingAfterSelected)
                    .ToList();
                var priorityIds = selected.Select(faction => faction.Id)
                    .Concat(active.Select(faction => faction.Id))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var remaining = Math.Max(0, _options.MaxFactionStates - selected.Count - active.Count);
                var others = result
                    .Where(faction => !priorityIds.Contains(faction.Id))
                    .OrderByDescending(faction => faction.ChapterId, ChapterIdComparer.Instance)
                    .Take(remaining)
                    .ToList();
                result = selected.Concat(active).Concat(others).ToList();
            }

            return result;
        }

        private async Task<List<TimelineSnapshot>> ExtractTimelineAsync(
            string targetChapterId,
            bool allVolumes,
            CancellationToken cancellationToken)
        {
            var guide = await _source.GetTimelineGuideAsync(allVolumes, cancellationToken).ConfigureAwait(false);
            return guide.ChapterTimeline
                .Where(entry => IsAtOrBefore(entry.ChapterId, targetChapterId))
                .OrderByDescending(entry => entry.ChapterId, ChapterIdComparer.Instance)
                .Take(Math.Max(0, _options.MaxTimelineEntries))
                .OrderBy(entry => entry.ChapterId, ChapterIdComparer.Instance)
                .Select(entry => new TimelineSnapshot
                {
                    ChapterId = entry.ChapterId,
                    TimePeriod = entry.TimePeriod,
                    ElapsedTime = entry.ElapsedTime,
                    KeyTimeEvent = entry.KeyTimeEvent
                })
                .ToList();
        }

        private async Task<List<CharacterLocationSnapshot>> ExtractCharacterLocationsAsync(
            IReadOnlyCollection<string>? characterIds,
            string targetChapterId,
            bool allVolumes,
            CancellationToken cancellationToken)
        {
            var guide = await _source.GetTimelineGuideAsync(allVolumes, cancellationToken).ConfigureAwait(false);
            var filter = ToFilter(characterIds);
            var result = new List<CharacterLocationSnapshot>();

            foreach (var (id, entry) in guide.CharacterLocations.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsAtOrBefore(entry.LastUpdatedChapter, targetChapterId))
                    continue;
                if (!allVolumes && (filter == null || !filter.Contains(id)) && !IsActiveInRecentChapters(entry.LastUpdatedChapter, targetChapterId))
                    continue;

                result.Add(new CharacterLocationSnapshot
                {
                    CharacterId = id,
                    CharacterName = entry.CharacterName,
                    CurrentLocation = entry.CurrentLocation,
                    ChapterId = entry.LastUpdatedChapter
                });
            }

            return result;
        }

        private async Task<List<ItemStateSnapshot>> ExtractItemStatesAsync(
            IReadOnlyCollection<string>? characterIds,
            string targetChapterId,
            bool allVolumes,
            CancellationToken cancellationToken)
        {
            var guide = await _source.GetItemStateGuideAsync(allVolumes, cancellationToken).ConfigureAwait(false);
            var holderFilter = ToFilter(characterIds);
            var all = new List<ItemStateSnapshot>();

            foreach (var (id, entry) in guide.Items.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var state = LastAtOrBefore(entry.StateHistory, targetChapterId, point => point.Chapter);
                if (state == null)
                    continue;

                all.Add(new ItemStateSnapshot
                {
                    Id = id,
                    Name = entry.Name,
                    CurrentHolder = !string.IsNullOrWhiteSpace(state.Holder) ? state.Holder : entry.CurrentHolder,
                    Status = !string.IsNullOrWhiteSpace(state.Status) ? state.Status : entry.CurrentStatus,
                    ChapterId = state.Chapter
                });
            }

            if (allVolumes)
                return all;

            var max = Math.Max(0, _options.MaxItemStates);
            if (holderFilter != null)
            {
                var related = all
                    .Where(item => holderFilter.Contains(item.CurrentHolder))
                    .ToList();
                var remaining = Math.Max(0, max - related.Count);
                var others = all
                    .Where(item => !holderFilter.Contains(item.CurrentHolder))
                    .OrderByDescending(item => item.ChapterId, ChapterIdComparer.Instance)
                    .Take(remaining)
                    .ToList();
                return related.Concat(others).ToList();
            }

            return all
                .OrderByDescending(item => item.ChapterId, ChapterIdComparer.Instance)
                .Take(max)
                .ToList();
        }

        private static CharacterStateSnapshot ToCharacterStateSnapshot(
            string id,
            CharacterStateEntry entry,
            CharacterState state)
        {
            return new CharacterStateSnapshot
            {
                Id = id,
                Name = entry.Name,
                Stage = state.Level,
                Abilities = string.Join("、", state.Abilities ?? []),
                Relationships = FormatRelationships(state.Relationships),
                ChapterId = state.Chapter
            };
        }

        private static LocationStateSnapshot ToLocationStateSnapshot(
            string id,
            LocationStateEntry entry,
            LocationStatePoint state)
        {
            return new LocationStateSnapshot
            {
                Id = id,
                Name = entry.Name,
                Status = !string.IsNullOrWhiteSpace(state.Status) ? state.Status : entry.CurrentStatus,
                ChapterId = state.Chapter
            };
        }

        private static T? LastAtOrBefore<T>(
            IEnumerable<T>? items,
            string targetChapterId,
            Func<T, string> getChapter)
            where T : class
        {
            return (items ?? [])
                .Where(item => IsAtOrBefore(getChapter(item), targetChapterId))
                .OrderBy(getChapter, ChapterIdComparer.Instance)
                .LastOrDefault();
        }

        private static bool IsAtOrBefore(string chapterId, string targetChapterId)
        {
            if (string.IsNullOrWhiteSpace(chapterId))
                return false;
            if (string.IsNullOrWhiteSpace(targetChapterId))
                return true;

            return ChapterIdComparer.Instance.Compare(chapterId, targetChapterId) <= 0;
        }

        private bool IsActiveInRecentChapters(string lastChapterId, string targetChapterId)
        {
            if (string.IsNullOrWhiteSpace(lastChapterId) || string.IsNullOrWhiteSpace(targetChapterId))
                return false;
            if (!IsAtOrBefore(lastChapterId, targetChapterId))
                return false;

            var current = ParseChapterId(targetChapterId);
            var last = ParseChapterId(lastChapterId);
            if (current == null || last == null)
                return false;

            var chaptersPerVolume = Math.Max(1, _options.ChaptersPerVolume);
            var distance = (current.Value.Volume - last.Value.Volume) * chaptersPerVolume
                + (current.Value.Chapter - last.Value.Chapter);
            return distance >= 0 && distance <= Math.Max(0, _options.ActiveEntityWindowChapters);
        }

        private static HashSet<string>? ToFilter(IReadOnlyCollection<string>? ids)
        {
            return ids == null || ids.Count == 0
                ? null
                : ids.Where(id => !string.IsNullOrWhiteSpace(id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static List<string> ToOrderedIds(IReadOnlyCollection<string>? ids)
        {
            return ids == null || ids.Count == 0
                ? []
                : ids
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }

        private static string FormatRelationships(Dictionary<string, RelationshipState>? relationships)
        {
            if (relationships == null || relationships.Count == 0)
                return string.Empty;

            return string.Join(
                "、",
                relationships
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
                    .Select(pair => string.IsNullOrWhiteSpace(pair.Value.Relation)
                        ? $"{pair.Key}(信任{pair.Value.Trust:+#;-#;0})"
                        : $"{pair.Key}({pair.Value.Relation},{pair.Value.Trust:+#;-#;0})"));
        }

        private static bool IsRelatedPlotPoint(
            PlotPointEntry point,
            IReadOnlySet<string> characterIds,
            IReadOnlySet<string> otherEntityIds)
        {
            var characterMatched = characterIds.Count > 0 &&
                (point.InvolvedCharacters ?? []).Any(characterIds.Contains);
            var otherMatched = otherEntityIds.Count > 0 &&
                (point.Keywords ?? []).Any(otherEntityIds.Contains);
            return characterMatched || otherMatched;
        }

        private static int PlotPointImportanceScore(PlotPointEntry point)
        {
            var score = point.Importance switch
            {
                "critical" => 3,
                "important" => 2,
                _ => 1
            };

            if (string.Equals(point.Storyline, "main", StringComparison.OrdinalIgnoreCase))
                score += 2;

            return score;
        }

        private static List<string> ParseTags(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return [];

            return text
                .Split([',', '，', '、', ';', '；', ' '], StringSplitOptions.RemoveEmptyEntries)
                .Select(tag => tag.Trim())
                .Where(tag => tag.Length > 0 && tag.Length <= 20)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static readonly string[] HairColorKeywords =
        [
            "黑发",
            "金发",
            "白发",
            "红发",
            "银发",
            "棕发",
            "蓝发",
            "紫发",
            "绿发"
        ];

        private static string ExtractHairColor(string? appearance)
        {
            if (string.IsNullOrWhiteSpace(appearance))
                return string.Empty;

            return HairColorKeywords.FirstOrDefault(keyword =>
                appearance.Contains(keyword, StringComparison.OrdinalIgnoreCase)) ?? string.Empty;
        }

        private static string GetPreviousChapterId(string chapterId)
        {
            var parsed = ParseChapterId(chapterId);
            if (parsed == null)
                return string.Empty;

            var (volume, chapter) = parsed.Value;
            if (chapter > 1)
                return BuildChapterId(volume, chapter - 1);

            return volume > 1 ? BuildChapterId(volume - 1, int.MaxValue) : string.Empty;
        }

        private static string BuildChapterId(int volume, int chapter) => $"vol{volume}_ch{chapter}";

        private static (int Volume, int Chapter)? ParseChapterId(string chapterId)
        {
            var match = Regex.Match(
                chapterId ?? string.Empty,
                @"(?:vol|v)(\d+)[_\-]?(?:ch|chapter)?(\d+)",
                RegexOptions.IgnoreCase);
            if (!match.Success)
                return null;

            return int.TryParse(match.Groups[1].Value, out var volume) &&
                int.TryParse(match.Groups[2].Value, out var chapter)
                ? (volume, chapter)
                : null;
        }

        private sealed class ChapterIdComparer : IComparer<string>
        {
            public static readonly ChapterIdComparer Instance = new();

            public int Compare(string? left, string? right)
            {
                var leftParsed = ParseChapterId(left ?? string.Empty);
                var rightParsed = ParseChapterId(right ?? string.Empty);
                if (leftParsed == null || rightParsed == null)
                    return string.Compare(left, right, StringComparison.OrdinalIgnoreCase);

                var volumeCompare = leftParsed.Value.Volume.CompareTo(rightParsed.Value.Volume);
                return volumeCompare != 0
                    ? volumeCompare
                    : leftParsed.Value.Chapter.CompareTo(rightParsed.Value.Chapter);
            }
        }
    }
}
