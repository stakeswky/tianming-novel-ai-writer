using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Interfaces;
using TM.Services.Modules.ProjectData.Models.Generated;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class VolumeValidationProcessOutput
    {
        public VolumeValidationResult Result { get; init; } = new();
        public List<ChapterInfo> SampledChapters { get; init; } = new();
        public ValidationSummaryData? Summary { get; init; }
    }

    public sealed class VolumeValidationProcessor
    {
        private const int ValidationBatchSize = 2;

        private readonly Func<int, CancellationToken, Task<string>> _loadVolumeNameAsync;
        private readonly Func<CancellationToken, Task<IReadOnlyList<ChapterInfo>>> _loadGeneratedChaptersAsync;
        private readonly Func<IReadOnlyList<ChapterInfo>, string, CancellationToken, Task<IReadOnlyList<ChapterValidationResult>>> _processBatchAsync;
        private readonly IDictionary<string, int>? _dependencyModuleVersions;

        public VolumeValidationProcessor(
            Func<int, CancellationToken, Task<string>> loadVolumeNameAsync,
            Func<CancellationToken, Task<IReadOnlyList<ChapterInfo>>> loadGeneratedChaptersAsync,
            Func<IReadOnlyList<ChapterInfo>, string, CancellationToken, Task<IReadOnlyList<ChapterValidationResult>>> processBatchAsync,
            IDictionary<string, int>? dependencyModuleVersions = null)
        {
            _loadVolumeNameAsync = loadVolumeNameAsync;
            _loadGeneratedChaptersAsync = loadGeneratedChaptersAsync;
            _processBatchAsync = processBatchAsync;
            _dependencyModuleVersions = dependencyModuleVersions;
        }

        public async Task<VolumeValidationProcessOutput> ProcessAsync(
            int volumeNumber,
            CancellationToken cancellationToken)
        {
            var volumeName = await _loadVolumeNameAsync(volumeNumber, cancellationToken).ConfigureAwait(false);
            var result = new VolumeValidationResult
            {
                VolumeNumber = volumeNumber,
                VolumeName = volumeName,
                ValidatedTime = DateTime.Now
            };

            var chapters = await _loadGeneratedChaptersAsync(cancellationToken).ConfigureAwait(false);
            var volumeChapters = chapters
                .Where(chapter => chapter.VolumeNumber == volumeNumber)
                .OrderBy(chapter => chapter.ChapterNumber)
                .ToList();

            if (volumeChapters.Count == 0)
            {
                return new VolumeValidationProcessOutput { Result = result };
            }

            var sampleCount = VolumeValidationChapterSampler.CalculateSampleCount(volumeChapters.Count);
            var sampledChapters = VolumeValidationChapterSampler.SampleChapters(volumeChapters, sampleCount);
            var batches = sampledChapters
                .Select((chapter, index) => new { chapter, index })
                .GroupBy(item => item.index / ValidationBatchSize)
                .Select(group => group.Select(item => item.chapter).ToList())
                .ToList();
            var chapterResults = new List<ChapterValidationResult>();

            foreach (var batch in batches)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var batchResults = await _processBatchAsync(batch, volumeName, cancellationToken).ConfigureAwait(false);
                    chapterResults.AddRange(batchResults);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    chapterResults.AddRange(batch.Select(chapter =>
                        CreateErrorResult(chapter, volumeName, $"校验异常: {ex.Message}")));
                }
            }

            foreach (var chapterResult in chapterResults.OrderBy(chapter => chapter.ChapterNumber))
            {
                result.ChapterResults.Add(chapterResult);
            }

            var summary = VolumeValidationSummaryBuilder.Build(
                volumeNumber,
                volumeName,
                sampledChapters,
                result.ChapterResults,
                _dependencyModuleVersions);

            return new VolumeValidationProcessOutput
            {
                Result = result,
                SampledChapters = sampledChapters,
                Summary = summary
            };
        }

        private static ChapterValidationResult CreateErrorResult(
            ChapterInfo chapter,
            string volumeName,
            string message)
        {
            return new ChapterValidationResult
            {
                ChapterId = chapter.Id,
                VolumeNumber = chapter.VolumeNumber,
                ChapterNumber = chapter.ChapterNumber,
                VolumeName = volumeName,
                OverallResult = "失败",
                ValidatedTime = DateTime.Now,
                IssuesByModule =
                {
                    ["System"] =
                    [
                        new ValidationIssue
                        {
                            Type = "SystemError",
                            Severity = "Error",
                            Message = message
                        }
                    ]
                }
            };
        }
    }
}
