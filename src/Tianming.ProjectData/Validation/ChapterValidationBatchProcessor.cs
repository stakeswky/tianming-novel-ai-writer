using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Interfaces;
using TM.Services.Modules.ProjectData.Models.Generated;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class ChapterValidationBatchProcessor
    {
        private readonly Func<string, CancellationToken, Task<string?>> _loadChapterContentAsync;
        private readonly Func<ChapterValidationResult, string, CancellationToken, Task> _validateSingleChapterAsync;
        private readonly Func<IReadOnlyList<ChapterInfo>, IReadOnlyList<int>, IReadOnlyList<string?>, IReadOnlyList<ChapterValidationResult>, CancellationToken, Task<string?>> _validateBatchWithAIAsync;

        public ChapterValidationBatchProcessor(
            Func<string, CancellationToken, Task<string?>> loadChapterContentAsync,
            Func<ChapterValidationResult, string, CancellationToken, Task> validateSingleChapterAsync,
            Func<IReadOnlyList<ChapterInfo>, IReadOnlyList<int>, IReadOnlyList<string?>, IReadOnlyList<ChapterValidationResult>, CancellationToken, Task<string?>> validateBatchWithAIAsync)
        {
            _loadChapterContentAsync = loadChapterContentAsync;
            _validateSingleChapterAsync = validateSingleChapterAsync;
            _validateBatchWithAIAsync = validateBatchWithAIAsync;
        }

        public async Task<List<ChapterValidationResult>> ProcessAsync(
            IReadOnlyList<ChapterInfo> batch,
            string volumeName,
            CancellationToken cancellationToken)
        {
            var results = new List<ChapterValidationResult>();
            var contents = new List<string?>();

            foreach (var chapter in batch)
            {
                var content = await _loadChapterContentAsync(chapter.Id, cancellationToken);
                if (string.IsNullOrEmpty(content))
                {
                    results.Add(CreateErrorResult(chapter, volumeName, "章节正文不存在"));
                    contents.Add(null);
                    continue;
                }

                results.Add(new ChapterValidationResult
                {
                    ChapterId = chapter.Id,
                    ChapterTitle = ExtractChapterTitle(content),
                    VolumeNumber = chapter.VolumeNumber,
                    ChapterNumber = chapter.ChapterNumber,
                    VolumeName = volumeName,
                    ValidatedTime = DateTime.Now
                });
                contents.Add(content);
            }

            var pendingIndices = contents
                .Select((content, index) => (content, index))
                .Where(item => item.content != null)
                .Select(item => item.index)
                .ToList();

            if (pendingIndices.Count == 0)
                return results;

            cancellationToken.ThrowIfCancellationRequested();

            if (pendingIndices.Count == 1)
            {
                var index = pendingIndices[0];
                await ValidateSingleAsync(results[index], contents[index]!, cancellationToken);
                return results;
            }

            var aiContent = await _validateBatchWithAIAsync(batch, pendingIndices, contents, results, cancellationToken);
            if (string.IsNullOrWhiteSpace(aiContent))
            {
                foreach (var index in pendingIndices)
                {
                    await ValidateSingleAsync(results[index], contents[index]!, cancellationToken);
                }
            }
            else
            {
                var pendingResults = pendingIndices.Select(index => results[index]).ToList();
                BatchChapterValidationResultParser.ApplyAIContent(pendingResults, aiContent);
            }

            foreach (var index in pendingIndices)
            {
                results[index].OverallResult = ChapterValidationResultParser.DetermineOverallResult(results[index]);
            }

            return results;
        }

        private async Task ValidateSingleAsync(
            ChapterValidationResult result,
            string content,
            CancellationToken cancellationToken)
        {
            await _validateSingleChapterAsync(result, content, cancellationToken);
            result.OverallResult = ChapterValidationResultParser.DetermineOverallResult(result);
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

        private static string ExtractChapterTitle(string content)
        {
            if (string.IsNullOrEmpty(content))
                return "未命名章节";

            foreach (var line in content.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("# "))
                    return trimmed.Substring(2).Trim();
                if (trimmed.StartsWith("## "))
                    return trimmed.Substring(3).Trim();
            }

            return "未命名章节";
        }
    }
}
