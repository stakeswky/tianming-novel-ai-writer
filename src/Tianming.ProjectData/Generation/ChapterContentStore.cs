using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Generated;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class ChapterContentStore
    {
        private readonly string _chaptersDirectory;

        public ChapterContentStore(string chaptersDirectory)
        {
            if (string.IsNullOrWhiteSpace(chaptersDirectory))
                throw new ArgumentException("章节目录不能为空", nameof(chaptersDirectory));

            _chaptersDirectory = chaptersDirectory;
            Directory.CreateDirectory(_chaptersDirectory);
        }

        public async Task<ChapterSaveResult> SaveChapterAsync(string chapterId, string content)
        {
            if (string.IsNullOrWhiteSpace(chapterId))
                throw new ArgumentException("章节ID不能为空", nameof(chapterId));

            var stagingPath = Path.Combine(_chaptersDirectory, ".staging");
            var chapterFile = GetChapterPath(chapterId);
            var stagingFile = Path.Combine(stagingPath, $"{chapterId}.md");
            var backupFile = chapterFile + ".bak";
            var hadExistingFile = File.Exists(chapterFile);
            var contentChanged = true;

            if (hadExistingFile)
            {
                var oldContent = await File.ReadAllTextAsync(chapterFile).ConfigureAwait(false);
                contentChanged = !string.Equals(oldContent, content, StringComparison.Ordinal);
            }

            Directory.CreateDirectory(stagingPath);
            await File.WriteAllTextAsync(stagingFile, content).ConfigureAwait(false);

            try
            {
                if (hadExistingFile)
                    File.Copy(chapterFile, backupFile, overwrite: true);

                File.Move(stagingFile, chapterFile, overwrite: true);

                if (File.Exists(backupFile))
                    File.Delete(backupFile);

                return new ChapterSaveResult
                {
                    ChapterId = chapterId,
                    FilePath = chapterFile,
                    HadExistingFile = hadExistingFile,
                    ContentChanged = contentChanged
                };
            }
            catch
            {
                if (File.Exists(stagingFile))
                    File.Delete(stagingFile);

                if (hadExistingFile && File.Exists(backupFile))
                {
                    File.Copy(backupFile, chapterFile, overwrite: true);
                    File.Delete(backupFile);
                }
                else if (!hadExistingFile && File.Exists(chapterFile))
                {
                    File.Delete(chapterFile);
                }

                throw;
            }
        }

        public async Task<string?> GetChapterAsync(string chapterId)
        {
            if (string.IsNullOrWhiteSpace(chapterId))
                return null;

            var filePath = GetChapterPath(chapterId);
            if (!File.Exists(filePath))
                return null;

            return await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        }

        public async Task<List<ChapterInfo>> GetGeneratedChaptersAsync()
        {
            var chapters = new List<ChapterInfo>();
            if (!Directory.Exists(_chaptersDirectory))
                return chapters;

            foreach (var file in Directory.GetFiles(_chaptersDirectory, "*.md", SearchOption.TopDirectoryOnly))
            {
                var fileInfo = new FileInfo(file);
                var chapterId = Path.GetFileNameWithoutExtension(file);
                var (volumeNumber, chapterNumber) = ParseChapterIdOrDefault(chapterId);
                var (title, wordCount) = await ReadChapterMetaAsync(file).ConfigureAwait(false);

                chapters.Add(new ChapterInfo
                {
                    Id = chapterId,
                    Title = title,
                    VolumeNumber = volumeNumber,
                    ChapterNumber = chapterNumber,
                    WordCount = wordCount,
                    CreatedTime = fileInfo.CreationTime,
                    ModifiedTime = fileInfo.LastWriteTime,
                    FilePath = file
                });
            }

            return chapters
                .OrderBy(c => c.VolumeNumber)
                .ThenBy(c => c.ChapterNumber)
                .ThenBy(c => c.Id, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public Task<bool> DeleteChapterAsync(string chapterId)
        {
            if (string.IsNullOrWhiteSpace(chapterId))
                return Task.FromResult(false);

            var filePath = GetChapterPath(chapterId);
            if (!File.Exists(filePath))
                return Task.FromResult(false);

            File.Delete(filePath);
            return Task.FromResult(true);
        }

        public bool ChapterExists(string chapterId)
        {
            return !string.IsNullOrWhiteSpace(chapterId) && File.Exists(GetChapterPath(chapterId));
        }

        public Task<bool> VolumeExistsAsync(int volumeNumber)
        {
            if (volumeNumber <= 0 || !Directory.Exists(_chaptersDirectory))
                return Task.FromResult(false);

            return Task.FromResult(Directory
                .GetFiles(_chaptersDirectory, "*.md", SearchOption.TopDirectoryOnly)
                .Any(file => ParseChapterIdOrDefault(Path.GetFileNameWithoutExtension(file)).volumeNumber == volumeNumber));
        }

        public async Task<string> GenerateNextChapterIdFromSourceAsync(
            string sourceChapterId,
            IReadOnlyList<ChapterVolumeRange>? volumeRanges = null)
        {
            if (string.IsNullOrWhiteSpace(sourceChapterId))
                throw new ArgumentException("章节ID不能为空", nameof(sourceChapterId));

            var parsed = ParseChapterId(sourceChapterId);
            if (parsed == null)
                throw new ArgumentException($"章节ID格式无效: {sourceChapterId}", nameof(sourceChapterId));

            var (volumeNumber, chapterNumber) = parsed.Value;
            if (!await VolumeExistsAsync(volumeNumber).ConfigureAwait(false))
                throw new InvalidOperationException($"卷 {volumeNumber} 不存在");

            if (!ChapterExists(sourceChapterId))
                throw new InvalidOperationException($"来源章节 {sourceChapterId} 不存在");

            var targetChapterId = BuildChapterId(volumeNumber, chapterNumber + 1);
            var currentRange = volumeRanges?
                .FirstOrDefault(range => range.VolumeNumber == volumeNumber);
            if (currentRange != null && currentRange.EndChapter > 0 && chapterNumber >= currentRange.EndChapter)
            {
                var nextRange = volumeRanges!
                    .Where(range => range.VolumeNumber > volumeNumber)
                    .OrderBy(range => range.VolumeNumber)
                    .FirstOrDefault();
                if (nextRange != null)
                {
                    var nextStart = nextRange.StartChapter > 0 ? nextRange.StartChapter : 1;
                    targetChapterId = BuildChapterId(nextRange.VolumeNumber, nextStart);
                }
            }

            if (ChapterExists(targetChapterId))
                throw new InvalidOperationException($"目标章节 {targetChapterId} 已存在，请使用 @重写:{targetChapterId} 指令");

            return targetChapterId;
        }

        public string GetChapterPath(string chapterId)
        {
            return Path.Combine(_chaptersDirectory, $"{chapterId}.md");
        }

        private static async Task<(string Title, int WordCount)> ReadChapterMetaAsync(string filePath)
        {
            using var reader = new StreamReader(filePath, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            string? firstNonEmptyLine = null;
            string? title = null;
            var chineseCount = 0;
            var englishWords = 0;
            var lineIndex = 0;

            while (true)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line == null)
                    break;

                var trimmed = line.Trim();
                if (firstNonEmptyLine == null && !string.IsNullOrWhiteSpace(trimmed))
                    firstNonEmptyLine = trimmed;

                if (title == null && lineIndex < 10)
                {
                    if (trimmed.StartsWith("# ", StringComparison.Ordinal))
                        title = NormalizeChapterTitle(trimmed[2..].Trim());
                    else if (trimmed.StartsWith("## ", StringComparison.Ordinal))
                        title = NormalizeChapterTitle(trimmed[3..].Trim());
                }

                AccumulateWordCounts(line, ref chineseCount, ref englishWords);
                lineIndex++;
            }

            if (string.IsNullOrEmpty(title))
            {
                title = !string.IsNullOrEmpty(firstNonEmptyLine)
                    ? firstNonEmptyLine.Length > 50 ? firstNonEmptyLine[..50] + "..." : firstNonEmptyLine
                    : "未命名章节";
            }

            return (title, chineseCount + englishWords);
        }

        private static void AccumulateWordCounts(string line, ref int chineseCount, ref int englishWords)
        {
            foreach (var ch in line)
            {
                if (IsCjk(ch))
                    chineseCount++;
            }

            englishWords += Regex.Matches(line, @"[A-Za-z]+(?:['-][A-Za-z]+)?").Count;
        }

        private static bool IsCjk(char ch)
        {
            return ch >= '\u4e00' && ch <= '\u9fff';
        }

        private static (int volumeNumber, int chapterNumber) ParseChapterIdOrDefault(string chapterId)
        {
            return ParseChapterId(chapterId) ?? (0, 0);
        }

        private static (int volumeNumber, int chapterNumber)? ParseChapterId(string chapterId)
        {
            var patterns = new[]
            {
                @"vol(\d+)_ch(\d+)",
                @"v(\d+)_c(\d+)",
                @"(\d+)_(\d+)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(chapterId, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                    return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
            }

            return null;
        }

        private static string BuildChapterId(int volumeNumber, int chapterNumber)
        {
            return $"vol{volumeNumber}_ch{chapterNumber}";
        }

        private static string NormalizeChapterTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return title;

            var match = Regex.Match(title.Trim(), @"^第(\d+)(?:章节|章)[：:.]?\s*(.*)");
            if (!match.Success)
                return title.Trim();

            var chapterName = match.Groups[2].Value.Trim();
            return string.IsNullOrEmpty(chapterName)
                ? $"第{match.Groups[1].Value}章"
                : $"第{match.Groups[1].Value}章 {chapterName}";
        }
    }

    public sealed class ChapterSaveResult
    {
        public string ChapterId { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public bool HadExistingFile { get; set; }
        public bool ContentChanged { get; set; }
    }

    public sealed class ChapterVolumeRange
    {
        public int VolumeNumber { get; set; }
        public int StartChapter { get; set; }
        public int EndChapter { get; set; }
    }
}
