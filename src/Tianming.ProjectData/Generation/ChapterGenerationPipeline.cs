using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Generation.Wal;
using TM.Services.Modules.ProjectData.Implementations.Tracking.Debts;
using TM.Services.Modules.ProjectData.Models.Guides;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class ChapterGenerationPipeline
    {
        private readonly ContentGenerationPreparer _preparer;
        private readonly ChapterContentStore _contentStore;
        private readonly GenerationStatisticsRecorder _statisticsRecorder;
        private readonly ChapterTrackingDispatcher? _trackingDispatcher;
        private readonly List<IChapterDerivedIndex> _derivedIndexes;
        private readonly IFactSnapshotGuideSource? _factSnapshotGuideSource;
        private readonly TrackingDebtRegistry? _debtRegistry;
        private readonly IGenerationJournal? _journal;

        public ChapterGenerationPipeline(
            ContentGenerationPreparer preparer,
            ChapterContentStore contentStore,
            GenerationStatisticsRecorder statisticsRecorder,
            ChapterTrackingDispatcher? trackingDispatcher = null,
            FileChapterKeywordIndex? keywordIndex = null,
            IReadOnlyList<IChapterDerivedIndex>? derivedIndexes = null,
            IFactSnapshotGuideSource? factSnapshotGuideSource = null,
            TrackingDebtRegistry? debtRegistry = null,
            IGenerationJournal? journal = null)
        {
            _preparer = preparer;
            _contentStore = contentStore;
            _statisticsRecorder = statisticsRecorder;
            _trackingDispatcher = trackingDispatcher;
            _factSnapshotGuideSource = factSnapshotGuideSource;
            _debtRegistry = debtRegistry;
            _journal = journal;
            _derivedIndexes = new List<IChapterDerivedIndex>();
            if (keywordIndex != null)
                _derivedIndexes.Add(keywordIndex);
            if (derivedIndexes != null)
                _derivedIndexes.AddRange(derivedIndexes);
        }

        public async Task<GenerationResult> SaveGeneratedChapterStrictAsync(
            string chapterId,
            string rawContent,
            FactSnapshot factSnapshot,
            string? packagedTitle = null,
            IReadOnlyDictionary<string, string>? entityNameMap = null,
            DesignElementNames? designElements = null)
        {
            var result = new GenerationResult { ChapterId = chapterId };

            await AppendJournalStepAsync(chapterId, GenerationStep.PrepareStart).ConfigureAwait(false);
            var prepared = await _preparer.PrepareStrictAsync(
                chapterId,
                rawContent,
                factSnapshot,
                packagedTitle,
                entityNameMap,
                designElements: designElements).ConfigureAwait(false);
            await AppendJournalStepAsync(chapterId, GenerationStep.PrepareDone).ConfigureAwait(false);

            if (!prepared.GateResult.Success)
            {
                var failures = prepared.GateResult.GetTopFailures(5);
                result.Success = false;
                result.RequiresManualIntervention = true;
                result.GateResult = prepared.GateResult;
                result.ErrorMessage = string.Join("; ", failures);
                result.InterventionHint = "生成内容未通过门禁，请修正协议或一致性问题后重试。";
                result.AddAttempt(1, false, "门禁失败", failures);
                _statisticsRecorder.RecordGeneration(result);
                return result;
            }

            try
            {
                await AppendJournalStepAsync(chapterId, GenerationStep.GateDone).ConfigureAwait(false);
                var isRewrite = _contentStore.ChapterExists(chapterId);
                var saveResult = await _contentStore.SaveChapterAsync(chapterId, prepared.PersistedContent).ConfigureAwait(false);
                await AppendJournalStepAsync(chapterId, GenerationStep.ContentSaved).ConfigureAwait(false);
                if (prepared.ParsedChanges != null)
                {
                    if (isRewrite)
                        await RemoveDerivedDataForChapterAsync(chapterId).ConfigureAwait(false);

                    if (_trackingDispatcher != null)
                    {
                        await _trackingDispatcher.DispatchAsync(chapterId, prepared.ParsedChanges).ConfigureAwait(false);

                        if (_debtRegistry != null)
                        {
                            var context = new TrackingDebtDetectionContext
                            {
                                Foreshadowings = await LoadForeshadowingGuideAsync().ConfigureAwait(false),
                                Pledges = await LoadPledgeGuideAsync(chapterId).ConfigureAwait(false),
                                Secrets = await LoadSecretGuideAsync(chapterId).ConfigureAwait(false),
                            };
                            var debts = await _debtRegistry
                                .DetectAllAsync(chapterId, prepared.ParsedChanges, factSnapshot, context)
                                .ConfigureAwait(false);
                            if (debts.Count > 0)
                                await _trackingDispatcher.RecordTrackingDebtsAsync(chapterId, debts).ConfigureAwait(false);
                        }
                    }
                }
                await AppendJournalStepAsync(chapterId, GenerationStep.TrackingDone).ConfigureAwait(false);
                await IndexDerivedDataForChapterAsync(
                    chapterId,
                    saveResult.FilePath,
                    prepared.PersistedContent,
                    prepared.ParsedChanges).ConfigureAwait(false);
                await AppendJournalStepAsync(chapterId, GenerationStep.Done).ConfigureAwait(false);
                await ClearJournalAsync(chapterId).ConfigureAwait(false);

                result.Success = true;
                result.Content = prepared.PersistedContent;
                result.ParsedChanges = prepared.ParsedChanges;
                result.GateResult = prepared.GateResult;
                result.DesignElements = designElements;
                result.AddAttempt(1, true, "生成内容已校验并保存");
                _statisticsRecorder.RecordGeneration(result);
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.RequiresManualIntervention = true;
                result.GateResult = prepared.GateResult;
                result.ErrorMessage = ex.Message;
                result.InterventionHint = "内容已通过门禁，但写入章节文件失败。";
                result.AddAttempt(1, false, "保存失败", new List<string> { ex.Message });
                _statisticsRecorder.RecordGeneration(result);
                return result;
            }
        }

        private Task AppendJournalStepAsync(string chapterId, GenerationStep step)
        {
            if (_journal == null)
                return Task.CompletedTask;

            return _journal.AppendAsync(new GenerationJournalEntry
            {
                ChapterId = chapterId,
                Step = step
            });
        }

        private Task ClearJournalAsync(string chapterId)
        {
            if (_journal == null)
                return Task.CompletedTask;

            return _journal.ClearAsync(chapterId);
        }

        public async Task<bool> DeleteChapterAsync(string chapterId)
        {
            if (!_contentStore.ChapterExists(chapterId))
                return false;

            await RemoveDerivedDataForChapterAsync(chapterId).ConfigureAwait(false);

            return await _contentStore.DeleteChapterAsync(chapterId).ConfigureAwait(false);
        }

        private async Task RemoveDerivedDataForChapterAsync(string chapterId)
        {
            if (_trackingDispatcher != null)
            {
                try
                {
                    await _trackingDispatcher.RemoveChapterDataAsync(chapterId).ConfigureAwait(false);
                }
                catch
                {
                    // Original WPF flow treats each derived cleanup as non-fatal
                    // so the primary chapter file can still be saved or removed.
                }
            }

            await RemoveKeywordIndexForChapterAsync(chapterId).ConfigureAwait(false);
        }

        private async Task RemoveKeywordIndexForChapterAsync(string chapterId)
        {
            if (_derivedIndexes.Count == 0)
                return;

            foreach (var derivedIndex in _derivedIndexes)
            {
                try
                {
                    await derivedIndex.RemoveChapterAsync(chapterId).ConfigureAwait(false);
                }
                catch
                {
                    // Derived indexes are rebuildable and should not block the chapter write path.
                }
            }
        }

        private async Task IndexDerivedDataForChapterAsync(
            string chapterId,
            string chapterFilePath,
            string persistedContent,
            ChapterChanges? changes)
        {
            foreach (var derivedIndex in _derivedIndexes)
            {
                try
                {
                    await derivedIndex.IndexChapterAsync(chapterId, chapterFilePath, persistedContent, changes).ConfigureAwait(false);
                }
                catch
                {
                    // Matches the original flow: derived recall indexes may degrade without failing the saved chapter.
                }
            }
        }

        private async Task<ForeshadowingStatusGuide?> LoadForeshadowingGuideAsync()
        {
            if (_factSnapshotGuideSource == null)
                return null;

            return await _factSnapshotGuideSource.GetForeshadowingStatusGuideAsync().ConfigureAwait(false);
        }

        private Task<PledgeGuide?> LoadPledgeGuideAsync(string chapterId)
        {
            return LoadVolumeGuideAsync<PledgeGuide>(chapterId, "PledgeGuide.json");
        }

        private Task<SecretGuide?> LoadSecretGuideAsync(string chapterId)
        {
            return LoadVolumeGuideAsync<SecretGuide>(chapterId, "SecretGuide.json");
        }

        private async Task<T?> LoadVolumeGuideAsync<T>(string chapterId, string fileName)
        {
            var volume = ParseVolume(chapterId);
            if (volume <= 0)
                return default;

            var path = Path.Combine(_contentStore.ChaptersDirectory, $"vol{volume}", "guides", fileName);
            if (!File.Exists(path))
                return default;

            try
            {
                var json = await File.ReadAllTextAsync(path).ConfigureAwait(false);
                return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
            }
            catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
            {
                Console.Error.WriteLine($"Warning: failed to load tracking guide {path}: {ex.Message}");
                return default;
            }
        }

        private static int ParseVolume(string chapterId)
        {
            var match = Regex.Match(
                chapterId ?? string.Empty,
                @"(?:vol|v)(\d+)[_\-]?(?:ch|c|chapter)?\d+|^(\d+)_\d+",
                RegexOptions.IgnoreCase);
            if (!match.Success)
                return 0;

            var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            return int.TryParse(value, out var volume) ? volume : 0;
        }
    }
}
