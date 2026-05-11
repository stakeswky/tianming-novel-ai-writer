using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Interfaces;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class ChapterValidationProcessor
    {
        private readonly Func<string, CancellationToken, Task<string?>> _loadChapterContentAsync;
        private readonly Func<int, CancellationToken, Task<string>> _loadVolumeNameAsync;
        private readonly Func<ChapterValidationResult, string, CancellationToken, Task> _runGateChecksAsync;
        private readonly Func<ChapterValidationResult, string, CancellationToken, Task> _runAIValidationAsync;

        public ChapterValidationProcessor(
            Func<string, CancellationToken, Task<string?>> loadChapterContentAsync,
            Func<int, CancellationToken, Task<string>> loadVolumeNameAsync,
            Func<ChapterValidationResult, string, CancellationToken, Task> runGateChecksAsync,
            Func<ChapterValidationResult, string, CancellationToken, Task> runAIValidationAsync)
        {
            _loadChapterContentAsync = loadChapterContentAsync;
            _loadVolumeNameAsync = loadVolumeNameAsync;
            _runGateChecksAsync = runGateChecksAsync;
            _runAIValidationAsync = runAIValidationAsync;
        }

        public async Task<ChapterValidationResult> ProcessAsync(
            string chapterId,
            CancellationToken cancellationToken)
        {
            var (volumeNumber, chapterNumber) = ParseChapterIdOrDefault(chapterId);
            if (volumeNumber == 0 || chapterNumber == 0)
                return CreateErrorResult(chapterId, "无法解析章节ID");

            cancellationToken.ThrowIfCancellationRequested();

            var chapterContent = await _loadChapterContentAsync(chapterId, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(chapterContent))
            {
                var volumeNameForError = await _loadVolumeNameAsync(volumeNumber, cancellationToken).ConfigureAwait(false);
                return CreateErrorResult(chapterId, "章节正文不存在", volumeNumber, chapterNumber, volumeNameForError);
            }

            var volumeName = await _loadVolumeNameAsync(volumeNumber, cancellationToken).ConfigureAwait(false);
            var result = new ChapterValidationResult
            {
                ChapterId = chapterId,
                ChapterTitle = ExtractChapterTitle(chapterContent),
                VolumeNumber = volumeNumber,
                ChapterNumber = chapterNumber,
                VolumeName = volumeName,
                ValidatedTime = DateTime.Now
            };

            cancellationToken.ThrowIfCancellationRequested();
            await _runGateChecksAsync(result, chapterContent, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            await _runAIValidationAsync(result, chapterContent, cancellationToken).ConfigureAwait(false);

            result.OverallResult = ChapterValidationResultParser.DetermineOverallResult(result);
            return result;
        }

        private static (int VolumeNumber, int ChapterNumber) ParseChapterIdOrDefault(string chapterId)
        {
            if (string.IsNullOrWhiteSpace(chapterId))
                return (0, 0);

            var patterns = new[]
            {
                @"(?:vol|v)(\d+)_(?:ch|c)(\d+)",
                @"^(\d+)_(\d+)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(chapterId, pattern, RegexOptions.IgnoreCase);
                if (!match.Success)
                    continue;

                return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
            }

            return (0, 0);
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

        private static ChapterValidationResult CreateErrorResult(
            string chapterId,
            string message,
            int volumeNumber = 0,
            int chapterNumber = 0,
            string volumeName = "")
        {
            return new ChapterValidationResult
            {
                ChapterId = chapterId,
                VolumeNumber = volumeNumber,
                ChapterNumber = chapterNumber,
                VolumeName = volumeName,
                OverallResult = "失败",
                ValidatedTime = DateTime.Now,
                IssuesByModule = new Dictionary<string, List<ValidationIssue>>
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
