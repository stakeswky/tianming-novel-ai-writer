using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Guides;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class FileChapterTrackingSink : IChapterTrackingSink
    {
        private readonly string _rootDirectory;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        public FileChapterTrackingSink(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("追踪目录不能为空", nameof(rootDirectory));

            _rootDirectory = rootDirectory;
            Directory.CreateDirectory(_rootDirectory);
        }

        public async Task UpdateCharacterStateAsync(string chapterId, CharacterStateChange change)
        {
            var guide = await LoadAsync<CharacterStateGuide>(VolumeFile("character_state_guide", chapterId)).ConfigureAwait(false);
            if (!guide.Characters.TryGetValue(change.CharacterId, out var entry))
            {
                entry = new CharacterStateEntry { Name = change.CharacterId };
                guide.Characters[change.CharacterId] = entry;
            }

            var last = entry.StateHistory.LastOrDefault();
            entry.StateHistory.Add(new CharacterState
            {
                Chapter = chapterId,
                Phase = GetPhaseFromChapter(chapterId),
                Level = !string.IsNullOrWhiteSpace(change.NewLevel) ? change.NewLevel : last?.Level ?? string.Empty,
                Abilities = MergeAbilities(last?.Abilities, change.NewAbilities, change.LostAbilities),
                Relationships = MergeRelationships(last?.Relationships, change.RelationshipChanges),
                MentalState = !string.IsNullOrWhiteSpace(change.NewMentalState) ? change.NewMentalState : last?.MentalState ?? string.Empty,
                KeyEvent = change.KeyEvent,
                Importance = string.IsNullOrWhiteSpace(change.Importance) ? "normal" : change.Importance
            });
            SortByChapter(entry.StateHistory, state => state.Chapter);

            await SaveAsync(VolumeFile("character_state_guide", chapterId), guide).ConfigureAwait(false);
        }

        public async Task UpdateConflictProgressAsync(string chapterId, ConflictProgressChange change)
        {
            var guide = await LoadAsync<ConflictProgressGuide>(VolumeFile("conflict_progress_guide", chapterId)).ConfigureAwait(false);
            if (!guide.Conflicts.TryGetValue(change.ConflictId, out var entry))
            {
                entry = new ConflictProgressEntry { Name = change.ConflictId, Status = "pending" };
                guide.Conflicts[change.ConflictId] = entry;
            }

            var oldStatus = entry.Status;
            if (!string.IsNullOrWhiteSpace(change.NewStatus))
                entry.Status = change.NewStatus;

            entry.ProgressPoints.Add(new ConflictProgressPoint
            {
                Chapter = chapterId,
                Event = change.Event,
                Status = change.NewStatus,
                Description = $"{oldStatus} → {change.NewStatus}",
                Importance = string.IsNullOrWhiteSpace(change.Importance) ? "normal" : change.Importance
            });
            if (!entry.InvolvedChapters.Contains(chapterId))
                entry.InvolvedChapters.Add(chapterId);

            await SaveAsync(VolumeFile("conflict_progress_guide", chapterId), guide).ConfigureAwait(false);
        }

        public async Task AddPlotPointAsync(string chapterId, PlotPointChange change)
        {
            var index = await LoadAsync<PlotPointsIndex>(VolumeFile("plot_points", chapterId)).ConfigureAwait(false);
            var entry = new PlotPointEntry
            {
                Id = $"D{index.PlotPoints.Count + 1:000000}",
                Chapter = chapterId,
                Keywords = change.Keywords,
                Context = change.Context,
                InvolvedCharacters = change.InvolvedCharacters,
                Importance = change.Importance,
                Storyline = change.Storyline
            };
            index.PlotPoints.Add(entry);
            if (!index.ChapterIndex.TryGetValue(chapterId, out var ids))
            {
                ids = new List<string>();
                index.ChapterIndex[chapterId] = ids;
            }
            ids.Add(entry.Id);

            foreach (var keyword in change.Keywords ?? new List<string>())
            {
                if (!index.Keywords.TryGetValue(keyword, out var keywordEntry))
                {
                    keywordEntry = new KeywordEntry();
                    index.Keywords[keyword] = keywordEntry;
                }
                keywordEntry.Appearances.Add(new KeywordAppearance { Chapter = chapterId, Context = change.Context });
            }

            await SaveAsync(VolumeFile("plot_points", chapterId), index).ConfigureAwait(false);
        }

        public async Task MarkForeshadowingAsSetupAsync(string foreshadowId, string chapterId)
        {
            var guide = await LoadAsync<ForeshadowingStatusGuide>("foreshadowing_status_guide.json").ConfigureAwait(false);
            var entry = GetForeshadowingEntry(guide, foreshadowId);
            entry.IsSetup = true;
            entry.ActualSetupChapter = chapterId;
            await SaveAsync("foreshadowing_status_guide.json", guide).ConfigureAwait(false);
        }

        public async Task MarkForeshadowingAsResolvedAsync(string foreshadowId, string chapterId)
        {
            var guide = await LoadAsync<ForeshadowingStatusGuide>("foreshadowing_status_guide.json").ConfigureAwait(false);
            var entry = GetForeshadowingEntry(guide, foreshadowId);
            entry.IsResolved = true;
            entry.IsOverdue = false;
            entry.ActualPayoffChapter = chapterId;
            await SaveAsync("foreshadowing_status_guide.json", guide).ConfigureAwait(false);
        }

        public async Task RefreshForeshadowingOverdueStatusAsync(string chapterId)
        {
            var path = Path.Combine(_rootDirectory, "foreshadowing_status_guide.json");
            if (!File.Exists(path))
                return;

            var guide = await LoadAsync<ForeshadowingStatusGuide>("foreshadowing_status_guide.json").ConfigureAwait(false);
            foreach (var entry in guide.Foreshadowings.Values)
            {
                if (entry.IsResolved)
                {
                    entry.IsOverdue = false;
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(entry.ExpectedPayoffChapter))
                    entry.IsOverdue = CompareChapterId(chapterId, entry.ExpectedPayoffChapter) > 0;
            }
            await SaveAsync("foreshadowing_status_guide.json", guide).ConfigureAwait(false);
        }

        public async Task UpdateLocationStateAsync(string chapterId, LocationStateChange change)
        {
            var guide = await LoadAsync<LocationStateGuide>(VolumeFile("location_state_guide", chapterId)).ConfigureAwait(false);
            if (!guide.Locations.TryGetValue(change.LocationId, out var entry))
            {
                entry = new LocationStateEntry { Name = change.LocationId };
                guide.Locations[change.LocationId] = entry;
            }
            if (!string.IsNullOrWhiteSpace(change.NewStatus))
                entry.CurrentStatus = change.NewStatus;
            entry.StateHistory.Add(new LocationStatePoint
            {
                Chapter = chapterId,
                Status = change.NewStatus,
                Event = change.Event,
                Importance = string.IsNullOrWhiteSpace(change.Importance) ? "normal" : change.Importance
            });
            SortByChapter(entry.StateHistory, point => point.Chapter);
            await SaveAsync(VolumeFile("location_state_guide", chapterId), guide).ConfigureAwait(false);
        }

        public async Task UpdateFactionStateAsync(string chapterId, FactionStateChange change)
        {
            var guide = await LoadAsync<FactionStateGuide>(VolumeFile("faction_state_guide", chapterId)).ConfigureAwait(false);
            if (!guide.Factions.TryGetValue(change.FactionId, out var entry))
            {
                entry = new FactionStateEntry { Name = change.FactionId };
                guide.Factions[change.FactionId] = entry;
            }
            if (!string.IsNullOrWhiteSpace(change.NewStatus))
                entry.CurrentStatus = change.NewStatus;
            entry.StateHistory.Add(new FactionStatePoint
            {
                Chapter = chapterId,
                Status = change.NewStatus,
                Event = change.Event,
                Importance = string.IsNullOrWhiteSpace(change.Importance) ? "normal" : change.Importance
            });
            SortByChapter(entry.StateHistory, point => point.Chapter);
            await SaveAsync(VolumeFile("faction_state_guide", chapterId), guide).ConfigureAwait(false);
        }

        public async Task UpdateTimeProgressionAsync(string chapterId, TimeProgressionChange change)
        {
            var guide = await LoadAsync<TimelineGuide>(VolumeFile("timeline_guide", chapterId)).ConfigureAwait(false);
            guide.ChapterTimeline.RemoveAll(entry => string.Equals(entry.ChapterId, chapterId, StringComparison.Ordinal));
            guide.ChapterTimeline.Add(new ChapterTimeEntry
            {
                ChapterId = chapterId,
                TimePeriod = change.TimePeriod,
                ElapsedTime = change.ElapsedTime,
                KeyTimeEvent = change.KeyTimeEvent,
                Importance = string.IsNullOrWhiteSpace(change.Importance) ? "normal" : change.Importance
            });
            SortByChapter(guide.ChapterTimeline, entry => entry.ChapterId);
            await SaveAsync(VolumeFile("timeline_guide", chapterId), guide).ConfigureAwait(false);
        }

        public async Task UpdateCharacterMovementsAsync(string chapterId, List<CharacterMovementChange> movements)
        {
            var guide = await LoadAsync<TimelineGuide>(VolumeFile("timeline_guide", chapterId)).ConfigureAwait(false);
            foreach (var move in movements)
            {
                if (string.IsNullOrWhiteSpace(move.CharacterId) || string.IsNullOrWhiteSpace(move.ToLocation))
                    continue;

                if (!guide.CharacterLocations.TryGetValue(move.CharacterId, out var entry))
                {
                    entry = new CharacterLocationEntry { CharacterName = move.CharacterId };
                    guide.CharacterLocations[move.CharacterId] = entry;
                }

                entry.CurrentLocation = move.ToLocation;
                entry.LastUpdatedChapter = chapterId;
                entry.MovementHistory.Add(new MovementRecord
                {
                    Chapter = chapterId,
                    FromLocation = move.FromLocation,
                    ToLocation = move.ToLocation,
                    Importance = string.IsNullOrWhiteSpace(move.Importance) ? "normal" : move.Importance
                });
                SortByChapter(entry.MovementHistory, record => record.Chapter);
            }
            await SaveAsync(VolumeFile("timeline_guide", chapterId), guide).ConfigureAwait(false);
        }

        public async Task UpdateItemStateAsync(string chapterId, ItemTransferChange change)
        {
            var guide = await LoadAsync<ItemStateGuide>(VolumeFile("item_state_guide", chapterId)).ConfigureAwait(false);
            var itemId = string.IsNullOrWhiteSpace(change.ItemId) ? change.ItemName : change.ItemId;
            if (string.IsNullOrWhiteSpace(itemId))
                return;

            if (!guide.Items.TryGetValue(itemId, out var entry))
            {
                entry = new ItemStateEntry { Name = string.IsNullOrWhiteSpace(change.ItemName) ? itemId : change.ItemName };
                guide.Items[itemId] = entry;
            }

            if (!string.IsNullOrWhiteSpace(change.ItemName))
                entry.Name = change.ItemName;
            if (!string.IsNullOrWhiteSpace(change.ToHolder))
                entry.CurrentHolder = change.ToHolder;
            if (!string.IsNullOrWhiteSpace(change.NewStatus))
                entry.CurrentStatus = change.NewStatus;

            entry.StateHistory.Add(new ItemStatePoint
            {
                Chapter = chapterId,
                Holder = !string.IsNullOrWhiteSpace(change.ToHolder) ? change.ToHolder : entry.CurrentHolder,
                Status = !string.IsNullOrWhiteSpace(change.NewStatus) ? change.NewStatus : entry.CurrentStatus,
                Event = change.Event,
                Importance = string.IsNullOrWhiteSpace(change.Importance) ? "normal" : change.Importance
            });
            SortByChapter(entry.StateHistory, point => point.Chapter);
            await SaveAsync(VolumeFile("item_state_guide", chapterId), guide).ConfigureAwait(false);
        }

        public async Task RemoveCharacterStateAsync(string chapterId)
        {
            var guide = await LoadAsync<CharacterStateGuide>(VolumeFile("character_state_guide", chapterId)).ConfigureAwait(false);
            foreach (var entry in guide.Characters.Values)
                entry.StateHistory.RemoveAll(state => state.Chapter == chapterId);
            await SaveAsync(VolumeFile("character_state_guide", chapterId), guide).ConfigureAwait(false);
        }

        public async Task RemoveConflictProgressAsync(string chapterId)
        {
            var guide = await LoadAsync<ConflictProgressGuide>(VolumeFile("conflict_progress_guide", chapterId)).ConfigureAwait(false);
            foreach (var entry in guide.Conflicts.Values)
            {
                entry.ProgressPoints.RemoveAll(point => point.Chapter == chapterId);
                entry.InvolvedChapters.RemoveAll(chapter => chapter == chapterId);
                entry.Status = entry.ProgressPoints.LastOrDefault(point => !string.IsNullOrWhiteSpace(point.Status))?.Status ?? "pending";
            }
            await SaveAsync(VolumeFile("conflict_progress_guide", chapterId), guide).ConfigureAwait(false);
        }

        public async Task RemovePlotPointsAsync(string chapterId)
        {
            var index = await LoadAsync<PlotPointsIndex>(VolumeFile("plot_points", chapterId)).ConfigureAwait(false);
            index.PlotPoints.RemoveAll(point => point.Chapter == chapterId);
            index.ChapterIndex.Remove(chapterId);
            foreach (var keyword in index.Keywords.Values)
                keyword.Appearances.RemoveAll(appearance => appearance.Chapter == chapterId);
            await SaveAsync(VolumeFile("plot_points", chapterId), index).ConfigureAwait(false);
        }

        public async Task RemoveForeshadowingStatusAsync(string chapterId)
        {
            var path = Path.Combine(_rootDirectory, "foreshadowing_status_guide.json");
            if (!File.Exists(path))
                return;

            var guide = await LoadAsync<ForeshadowingStatusGuide>("foreshadowing_status_guide.json").ConfigureAwait(false);
            foreach (var entry in guide.Foreshadowings.Values)
            {
                if (entry.ActualSetupChapter == chapterId)
                {
                    entry.IsSetup = false;
                    entry.ActualSetupChapter = string.Empty;
                }
                if (entry.ActualPayoffChapter == chapterId)
                {
                    entry.IsResolved = false;
                    entry.ActualPayoffChapter = string.Empty;
                }
            }
            await SaveAsync("foreshadowing_status_guide.json", guide).ConfigureAwait(false);
        }

        public async Task RemoveLocationStateAsync(string chapterId)
        {
            var guide = await LoadAsync<LocationStateGuide>(VolumeFile("location_state_guide", chapterId)).ConfigureAwait(false);
            foreach (var entry in guide.Locations.Values)
            {
                entry.StateHistory.RemoveAll(point => point.Chapter == chapterId);
                entry.CurrentStatus = entry.StateHistory.LastOrDefault(point => !string.IsNullOrWhiteSpace(point.Status))?.Status ?? "unknown";
            }
            await SaveAsync(VolumeFile("location_state_guide", chapterId), guide).ConfigureAwait(false);
        }

        public async Task RemoveFactionStateAsync(string chapterId)
        {
            var guide = await LoadAsync<FactionStateGuide>(VolumeFile("faction_state_guide", chapterId)).ConfigureAwait(false);
            foreach (var entry in guide.Factions.Values)
            {
                entry.StateHistory.RemoveAll(point => point.Chapter == chapterId);
                entry.CurrentStatus = entry.StateHistory.LastOrDefault(point => !string.IsNullOrWhiteSpace(point.Status))?.Status ?? "unknown";
            }
            await SaveAsync(VolumeFile("faction_state_guide", chapterId), guide).ConfigureAwait(false);
        }

        public async Task RemoveTimelineAsync(string chapterId)
        {
            var guide = await LoadAsync<TimelineGuide>(VolumeFile("timeline_guide", chapterId)).ConfigureAwait(false);
            guide.ChapterTimeline.RemoveAll(entry => entry.ChapterId == chapterId);
            foreach (var entry in guide.CharacterLocations.Values)
            {
                entry.MovementHistory.RemoveAll(record => record.Chapter == chapterId);
                var lastMove = entry.MovementHistory.LastOrDefault();
                entry.CurrentLocation = lastMove?.ToLocation ?? string.Empty;
                entry.LastUpdatedChapter = lastMove?.Chapter ?? string.Empty;
            }
            await SaveAsync(VolumeFile("timeline_guide", chapterId), guide).ConfigureAwait(false);
        }

        public async Task RemoveItemStateAsync(string chapterId)
        {
            var guide = await LoadAsync<ItemStateGuide>(VolumeFile("item_state_guide", chapterId)).ConfigureAwait(false);
            foreach (var entry in guide.Items.Values)
            {
                entry.StateHistory.RemoveAll(point => point.Chapter == chapterId);
                var last = entry.StateHistory.LastOrDefault();
                entry.CurrentHolder = last?.Holder ?? string.Empty;
                entry.CurrentStatus = last?.Status ?? "unknown";
            }
            await SaveAsync(VolumeFile("item_state_guide", chapterId), guide).ConfigureAwait(false);
        }

        private static ForeshadowingStatusEntry GetForeshadowingEntry(ForeshadowingStatusGuide guide, string foreshadowId)
        {
            if (!guide.Foreshadowings.TryGetValue(foreshadowId, out var entry))
            {
                entry = new ForeshadowingStatusEntry { Name = foreshadowId };
                guide.Foreshadowings[foreshadowId] = entry;
            }
            return entry;
        }

        private async Task<T> LoadAsync<T>(string relativePath) where T : new()
        {
            var path = Path.Combine(_rootDirectory, relativePath);
            if (!File.Exists(path))
                return new T();

            var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
            return JsonSerializer.Deserialize<T>(json, _jsonOptions) ?? new T();
        }

        private async Task SaveAsync<T>(string relativePath, T value)
        {
            var path = Path.Combine(_rootDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var json = JsonSerializer.Serialize(value, _jsonOptions);
            await File.WriteAllTextAsync(path, json).ConfigureAwait(false);
        }

        private static string VolumeFile(string prefix, string chapterId)
        {
            return $"{prefix}_vol{ParseVolume(chapterId)}.json";
        }

        private static int ParseVolume(string chapterId)
        {
            var match = System.Text.RegularExpressions.Regex.Match(chapterId ?? string.Empty, @"(?:vol|v)(\d+)|^(\d+)_", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success)
                return 0;
            var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            return int.TryParse(value, out var volume) ? volume : 0;
        }

        private static int CompareChapterId(string left, string right)
        {
            var a = ParseParts(left);
            var b = ParseParts(right);
            var volumeCompare = a.volume.CompareTo(b.volume);
            return volumeCompare != 0 ? volumeCompare : a.chapter.CompareTo(b.chapter);
        }

        private static (int volume, int chapter) ParseParts(string chapterId)
        {
            var match = System.Text.RegularExpressions.Regex.Match(chapterId ?? string.Empty, @"(?:vol|v)(\d+)_(?:ch|c)(\d+)|^(\d+)_(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (!match.Success)
                return (0, 0);
            var volumeText = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
            var chapterText = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[4].Value;
            return (int.Parse(volumeText), int.Parse(chapterText));
        }

        private static void SortByChapter<T>(List<T> items, Func<T, string> getChapter)
        {
            items.Sort((left, right) => CompareChapterId(getChapter(left), getChapter(right)));
        }

        private static string GetPhaseFromChapter(string chapterId)
        {
            return ParseVolume(chapterId) switch
            {
                1 => "起",
                2 => "承",
                3 => "转",
                _ => "合"
            };
        }

        private static List<string> MergeAbilities(List<string>? existing, List<string>? additions, List<string>? removals)
        {
            var result = new HashSet<string>(existing ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
            foreach (var ability in additions ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(ability))
                    result.Add(ability.Trim());
            }
            foreach (var ability in removals ?? new List<string>())
            {
                if (!string.IsNullOrWhiteSpace(ability))
                    result.Remove(ability.Trim());
            }
            return result.ToList();
        }

        private static Dictionary<string, RelationshipState> MergeRelationships(
            Dictionary<string, RelationshipState>? existing,
            Dictionary<string, RelationshipChange>? changes)
        {
            var result = existing != null
                ? new Dictionary<string, RelationshipState>(existing, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, RelationshipState>(StringComparer.OrdinalIgnoreCase);

            foreach (var (targetId, change) in changes ?? new Dictionary<string, RelationshipChange>())
            {
                if (!result.TryGetValue(targetId, out var state))
                {
                    state = new RelationshipState();
                    result[targetId] = state;
                }
                state.Relation = change.Relation;
                state.Trust += change.TrustDelta;
            }

            return result;
        }
    }
}
