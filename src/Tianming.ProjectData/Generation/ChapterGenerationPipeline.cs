using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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

        public ChapterGenerationPipeline(
            ContentGenerationPreparer preparer,
            ChapterContentStore contentStore,
            GenerationStatisticsRecorder statisticsRecorder,
            ChapterTrackingDispatcher? trackingDispatcher = null,
            FileChapterKeywordIndex? keywordIndex = null,
            IReadOnlyList<IChapterDerivedIndex>? derivedIndexes = null)
        {
            _preparer = preparer;
            _contentStore = contentStore;
            _statisticsRecorder = statisticsRecorder;
            _trackingDispatcher = trackingDispatcher;
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

            var prepared = await _preparer.PrepareStrictAsync(
                chapterId,
                rawContent,
                factSnapshot,
                packagedTitle,
                entityNameMap,
                designElements: designElements).ConfigureAwait(false);

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
                var isRewrite = _contentStore.ChapterExists(chapterId);
                var saveResult = await _contentStore.SaveChapterAsync(chapterId, prepared.PersistedContent).ConfigureAwait(false);
                if (prepared.ParsedChanges != null)
                {
                    if (isRewrite)
                        await RemoveDerivedDataForChapterAsync(chapterId).ConfigureAwait(false);

                    if (_trackingDispatcher != null)
                        await _trackingDispatcher.DispatchAsync(chapterId, prepared.ParsedChanges).ConfigureAwait(false);
                }
                await IndexDerivedDataForChapterAsync(
                    chapterId,
                    saveResult.FilePath,
                    prepared.PersistedContent,
                    prepared.ParsedChanges).ConfigureAwait(false);

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
    }
}
