using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TM.Framework.Common.Models;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Models.Design.Location;
using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Models.Guides;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class FileFactSnapshotGuideSource : IFactSnapshotGuideSource
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly string _trackingDirectory;
        private readonly string _projectRootDirectory;
        private readonly int _recentVolumeCount;

        public FileFactSnapshotGuideSource(
            string trackingDirectory,
            string projectRootDirectory,
            int recentVolumeCount = 5)
        {
            if (string.IsNullOrWhiteSpace(trackingDirectory))
                throw new ArgumentException("追踪目录不能为空", nameof(trackingDirectory));
            if (string.IsNullOrWhiteSpace(projectRootDirectory))
                throw new ArgumentException("项目根目录不能为空", nameof(projectRootDirectory));

            _trackingDirectory = trackingDirectory;
            _projectRootDirectory = projectRootDirectory;
            _recentVolumeCount = Math.Max(1, recentVolumeCount);
        }

        public Task<CharacterStateGuide> GetCharacterStateGuideAsync(bool allVolumes, CancellationToken cancellationToken = default)
            => AggregateVolumeGuidesAsync<CharacterStateGuide>(
                "character_state_guide",
                allVolumes,
                MergeCharacterStateGuide,
                SortCharacterStateGuide,
                cancellationToken);

        public Task<ConflictProgressGuide> GetConflictProgressGuideAsync(bool allVolumes, CancellationToken cancellationToken = default)
            => AggregateVolumeGuidesAsync<ConflictProgressGuide>(
                "conflict_progress_guide",
                allVolumes,
                MergeConflictProgressGuide,
                SortConflictProgressGuide,
                cancellationToken);

        public Task<ForeshadowingStatusGuide> GetForeshadowingStatusGuideAsync(CancellationToken cancellationToken = default)
            => ReadJsonAsync<ForeshadowingStatusGuide>(
                Path.Combine(_trackingDirectory, "foreshadowing_status_guide.json"),
                cancellationToken);

        public Task<LocationStateGuide> GetLocationStateGuideAsync(bool allVolumes, CancellationToken cancellationToken = default)
            => AggregateVolumeGuidesAsync<LocationStateGuide>(
                "location_state_guide",
                allVolumes,
                MergeLocationStateGuide,
                SortLocationStateGuide,
                cancellationToken);

        public Task<FactionStateGuide> GetFactionStateGuideAsync(bool allVolumes, CancellationToken cancellationToken = default)
            => AggregateVolumeGuidesAsync<FactionStateGuide>(
                "faction_state_guide",
                allVolumes,
                MergeFactionStateGuide,
                SortFactionStateGuide,
                cancellationToken);

        public Task<TimelineGuide> GetTimelineGuideAsync(bool allVolumes, CancellationToken cancellationToken = default)
            => AggregateVolumeGuidesAsync<TimelineGuide>(
                "timeline_guide",
                allVolumes,
                MergeTimelineGuide,
                SortTimelineGuide,
                cancellationToken);

        public Task<ItemStateGuide> GetItemStateGuideAsync(bool allVolumes, CancellationToken cancellationToken = default)
            => AggregateVolumeGuidesAsync<ItemStateGuide>(
                "item_state_guide",
                allVolumes,
                MergeItemStateGuide,
                SortItemStateGuide,
                cancellationToken);

        public async Task<IReadOnlyList<PlotPointEntry>> GetPlotPointsAsync(
            string currentChapterId,
            IReadOnlyCollection<string> characterIds,
            IReadOnlyCollection<string> otherEntityIds,
            CancellationToken cancellationToken = default)
        {
            var characterFilter = characterIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var otherFilter = otherEntityIds
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (characterFilter.Count == 0 && otherFilter.Count == 0)
                return [];

            var currentVolume = ParseVolume(currentChapterId);
            var files = GetVolumeFiles("plot_points")
                .Where(file => currentVolume == null ||
                    (file.Volume <= currentVolume && file.Volume > currentVolume - _recentVolumeCount))
                .OrderBy(file => file.Volume)
                .ToList();

            var result = new List<PlotPointEntry>();
            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var index = await ReadJsonAsync<PlotPointsIndex>(file.Path, cancellationToken).ConfigureAwait(false);
                result.AddRange(index.PlotPoints.Where(point =>
                    (point.InvolvedCharacters ?? []).Any(characterFilter.Contains) ||
                    (point.Keywords ?? []).Any(otherFilter.Contains)));
            }

            return result;
        }

        public async Task<IReadOnlyList<CharacterRulesData>> GetCharactersAsync(
            IReadOnlyCollection<string> characterIds,
            CancellationToken cancellationToken = default)
        {
            var characters = await ReadListAsync<CharacterRulesData>(
                Path.Combine(_projectRootDirectory, "Modules", "Design", "Elements", "CharacterRules", "character_rules.json"),
                cancellationToken).ConfigureAwait(false);
            return FilterById(characters, characterIds);
        }

        public async Task<IReadOnlyList<LocationRulesData>> GetLocationsAsync(
            IReadOnlyCollection<string> locationIds,
            CancellationToken cancellationToken = default)
        {
            var locations = await ReadListAsync<LocationRulesData>(
                Path.Combine(_projectRootDirectory, "Modules", "Design", "Elements", "LocationRules", "location_rules.json"),
                cancellationToken).ConfigureAwait(false);
            return FilterById(locations, locationIds);
        }

        public async Task<IReadOnlyList<WorldRulesData>> GetWorldRulesAsync(
            IReadOnlyCollection<string> worldRuleIds,
            CancellationToken cancellationToken = default)
        {
            var rules = await ReadListAsync<WorldRulesData>(
                Path.Combine(_projectRootDirectory, "Modules", "Design", "GlobalSettings", "WorldRules", "world_rules.json"),
                cancellationToken).ConfigureAwait(false);
            return FilterById(rules, worldRuleIds);
        }

        private async Task<T> AggregateVolumeGuidesAsync<T>(
            string prefix,
            bool allVolumes,
            Action<T, T> merge,
            Action<T> sort,
            CancellationToken cancellationToken)
            where T : new()
        {
            var result = new T();
            var files = GetVolumeFiles(prefix).OrderBy(file => file.Volume).ToList();
            if (!allVolumes)
                files = files.TakeLast(_recentVolumeCount).ToList();

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var guide = await ReadJsonAsync<T>(file.Path, cancellationToken).ConfigureAwait(false);
                merge(result, guide);
            }

            sort(result);
            return result;
        }

        private List<(int Volume, string Path)> GetVolumeFiles(string prefix)
        {
            if (!Directory.Exists(_trackingDirectory))
                return [];

            return Directory
                .EnumerateFiles(_trackingDirectory, $"{prefix}_vol*.json", SearchOption.TopDirectoryOnly)
                .Select(path => (Volume: ParseVolume(Path.GetFileName(path)) ?? -1, Path: path))
                .Where(file => file.Volume >= 0)
                .OrderBy(file => file.Volume)
                .ToList();
        }

        private static async Task<T> ReadJsonAsync<T>(string path, CancellationToken cancellationToken)
            where T : new()
        {
            if (!File.Exists(path))
                return new T();

            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken).ConfigureAwait(false) ?? new T();
        }

        private static async Task<List<T>> ReadListAsync<T>(string path, CancellationToken cancellationToken)
        {
            if (!File.Exists(path))
                return [];

            await using var stream = File.OpenRead(path);
            return await JsonSerializer.DeserializeAsync<List<T>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false) ?? [];
        }

        private static IReadOnlyList<T> FilterById<T>(IEnumerable<T> items, IReadOnlyCollection<string> ids)
            where T : class, IDataItem
        {
            var filter = ids
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            return items
                .Where(item => filter.Contains(item.Id))
                .ToList();
        }

        private static void MergeCharacterStateGuide(CharacterStateGuide target, CharacterStateGuide source)
        {
            foreach (var (id, sourceEntry) in source.Characters)
            {
                if (!target.Characters.TryGetValue(id, out var targetEntry))
                {
                    targetEntry = new CharacterStateEntry { Name = sourceEntry.Name, BaseProfile = sourceEntry.BaseProfile };
                    target.Characters[id] = targetEntry;
                }
                if (!string.IsNullOrWhiteSpace(sourceEntry.Name))
                    targetEntry.Name = sourceEntry.Name;
                if (!string.IsNullOrWhiteSpace(sourceEntry.BaseProfile))
                    targetEntry.BaseProfile = sourceEntry.BaseProfile;
                targetEntry.StateHistory.AddRange(sourceEntry.StateHistory);
                targetEntry.DriftWarnings.AddRange(sourceEntry.DriftWarnings);
            }
        }

        private static void MergeConflictProgressGuide(ConflictProgressGuide target, ConflictProgressGuide source)
        {
            foreach (var (id, sourceEntry) in source.Conflicts)
            {
                if (!target.Conflicts.TryGetValue(id, out var targetEntry))
                {
                    targetEntry = new ConflictProgressEntry
                    {
                        Name = sourceEntry.Name,
                        Type = sourceEntry.Type,
                        Tier = sourceEntry.Tier,
                        Status = sourceEntry.Status
                    };
                    target.Conflicts[id] = targetEntry;
                }
                if (!string.IsNullOrWhiteSpace(sourceEntry.Name))
                    targetEntry.Name = sourceEntry.Name;
                if (!string.IsNullOrWhiteSpace(sourceEntry.Status))
                    targetEntry.Status = sourceEntry.Status;
                targetEntry.ProgressPoints.AddRange(sourceEntry.ProgressPoints);
                targetEntry.InvolvedChapters = targetEntry.InvolvedChapters
                    .Concat(sourceEntry.InvolvedChapters)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                targetEntry.InvolvedCharacters = targetEntry.InvolvedCharacters
                    .Concat(sourceEntry.InvolvedCharacters)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }

        private static void MergeLocationStateGuide(LocationStateGuide target, LocationStateGuide source)
        {
            foreach (var (id, sourceEntry) in source.Locations)
            {
                if (!target.Locations.TryGetValue(id, out var targetEntry))
                {
                    targetEntry = new LocationStateEntry { Name = sourceEntry.Name, CurrentStatus = sourceEntry.CurrentStatus };
                    target.Locations[id] = targetEntry;
                }
                if (!string.IsNullOrWhiteSpace(sourceEntry.Name))
                    targetEntry.Name = sourceEntry.Name;
                if (!string.IsNullOrWhiteSpace(sourceEntry.CurrentStatus))
                    targetEntry.CurrentStatus = sourceEntry.CurrentStatus;
                targetEntry.StateHistory.AddRange(sourceEntry.StateHistory);
            }
        }

        private static void MergeFactionStateGuide(FactionStateGuide target, FactionStateGuide source)
        {
            foreach (var (id, sourceEntry) in source.Factions)
            {
                if (!target.Factions.TryGetValue(id, out var targetEntry))
                {
                    targetEntry = new FactionStateEntry { Name = sourceEntry.Name, CurrentStatus = sourceEntry.CurrentStatus };
                    target.Factions[id] = targetEntry;
                }
                if (!string.IsNullOrWhiteSpace(sourceEntry.Name))
                    targetEntry.Name = sourceEntry.Name;
                if (!string.IsNullOrWhiteSpace(sourceEntry.CurrentStatus))
                    targetEntry.CurrentStatus = sourceEntry.CurrentStatus;
                targetEntry.StateHistory.AddRange(sourceEntry.StateHistory);
            }
        }

        private static void MergeTimelineGuide(TimelineGuide target, TimelineGuide source)
        {
            target.ChapterTimeline.AddRange(source.ChapterTimeline);
            foreach (var (id, sourceEntry) in source.CharacterLocations)
            {
                if (!target.CharacterLocations.TryGetValue(id, out var targetEntry))
                {
                    targetEntry = new CharacterLocationEntry { CharacterName = sourceEntry.CharacterName };
                    target.CharacterLocations[id] = targetEntry;
                }
                if (!string.IsNullOrWhiteSpace(sourceEntry.CharacterName))
                    targetEntry.CharacterName = sourceEntry.CharacterName;
                targetEntry.MovementHistory.AddRange(sourceEntry.MovementHistory);
                if (!string.IsNullOrWhiteSpace(sourceEntry.LastUpdatedChapter) &&
                    (string.IsNullOrWhiteSpace(targetEntry.LastUpdatedChapter) ||
                     CompareChapterId(sourceEntry.LastUpdatedChapter, targetEntry.LastUpdatedChapter) >= 0))
                {
                    targetEntry.CurrentLocation = sourceEntry.CurrentLocation;
                    targetEntry.LastUpdatedChapter = sourceEntry.LastUpdatedChapter;
                }
            }
        }

        private static void MergeItemStateGuide(ItemStateGuide target, ItemStateGuide source)
        {
            foreach (var (id, sourceEntry) in source.Items)
            {
                if (!target.Items.TryGetValue(id, out var targetEntry))
                {
                    targetEntry = new ItemStateEntry { Name = sourceEntry.Name };
                    target.Items[id] = targetEntry;
                }
                if (!string.IsNullOrWhiteSpace(sourceEntry.Name))
                    targetEntry.Name = sourceEntry.Name;
                if (!string.IsNullOrWhiteSpace(sourceEntry.Description))
                    targetEntry.Description = sourceEntry.Description;
                if (!string.IsNullOrWhiteSpace(sourceEntry.CurrentHolder))
                    targetEntry.CurrentHolder = sourceEntry.CurrentHolder;
                if (!string.IsNullOrWhiteSpace(sourceEntry.CurrentStatus))
                    targetEntry.CurrentStatus = sourceEntry.CurrentStatus;
                targetEntry.StateHistory.AddRange(sourceEntry.StateHistory);
            }
        }

        private static void SortCharacterStateGuide(CharacterStateGuide guide)
        {
            foreach (var entry in guide.Characters.Values)
                entry.StateHistory.Sort((left, right) => CompareChapterId(left.Chapter, right.Chapter));
        }

        private static void SortConflictProgressGuide(ConflictProgressGuide guide)
        {
            foreach (var entry in guide.Conflicts.Values)
                entry.ProgressPoints.Sort((left, right) => CompareChapterId(left.Chapter, right.Chapter));
        }

        private static void SortLocationStateGuide(LocationStateGuide guide)
        {
            foreach (var entry in guide.Locations.Values)
                entry.StateHistory.Sort((left, right) => CompareChapterId(left.Chapter, right.Chapter));
        }

        private static void SortFactionStateGuide(FactionStateGuide guide)
        {
            foreach (var entry in guide.Factions.Values)
                entry.StateHistory.Sort((left, right) => CompareChapterId(left.Chapter, right.Chapter));
        }

        private static void SortTimelineGuide(TimelineGuide guide)
        {
            guide.ChapterTimeline.Sort((left, right) => CompareChapterId(left.ChapterId, right.ChapterId));
            foreach (var entry in guide.CharacterLocations.Values)
                entry.MovementHistory.Sort((left, right) => CompareChapterId(left.Chapter, right.Chapter));
        }

        private static void SortItemStateGuide(ItemStateGuide guide)
        {
            foreach (var entry in guide.Items.Values)
                entry.StateHistory.Sort((left, right) => CompareChapterId(left.Chapter, right.Chapter));
        }

        private static int? ParseVolume(string? text)
        {
            var match = Regex.Match(text ?? string.Empty, @"(?:vol|v)(\d+)|^(\d+)_", RegexOptions.IgnoreCase);
            if (!match.Success)
                return null;

            var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            return int.TryParse(value, out var volume) ? volume : null;
        }

        private static int CompareChapterId(string? left, string? right)
        {
            var leftParts = ParseChapterParts(left);
            var rightParts = ParseChapterParts(right);
            var volumeCompare = leftParts.Volume.CompareTo(rightParts.Volume);
            return volumeCompare != 0
                ? volumeCompare
                : leftParts.Chapter.CompareTo(rightParts.Chapter);
        }

        private static (int Volume, int Chapter) ParseChapterParts(string? chapterId)
        {
            var match = Regex.Match(
                chapterId ?? string.Empty,
                @"(?:vol|v)(\d+)[_\-]?(?:ch|c|chapter)?(\d+)|^(\d+)_(\d+)",
                RegexOptions.IgnoreCase);
            if (!match.Success)
                return (0, 0);

            var volumeText = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
            var chapterText = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value;
            return (
                int.TryParse(volumeText, out var volume) ? volume : 0,
                int.TryParse(chapterText, out var chapter) ? chapter : 0);
        }
    }
}
