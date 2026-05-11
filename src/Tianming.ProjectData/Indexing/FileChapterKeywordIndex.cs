using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class FileChapterKeywordIndex : IChapterDerivedIndex
    {
        private const int MaxChaptersPerKeyword = 50;
        private readonly string _indexPath;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
        private Dictionary<string, List<string>>? _index;

        public FileChapterKeywordIndex(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("关键词索引目录不能为空", nameof(rootDirectory));

            Directory.CreateDirectory(rootDirectory);
            _indexPath = Path.Combine(rootDirectory, "keyword_chapter_index.json");
        }

        public async Task IndexChapterAsync(string chapterId, ChapterChanges changes)
        {
            if (string.IsNullOrWhiteSpace(chapterId) || changes == null)
                return;

            await IndexChapterFromKeywordsAsync(chapterId, ExtractKeywords(changes)).ConfigureAwait(false);
        }

        public Task IndexChapterAsync(string chapterId, string chapterFilePath, string persistedContent, ChapterChanges? changes)
        {
            return changes == null
                ? Task.CompletedTask
                : IndexChapterAsync(chapterId, changes);
        }

        public async Task IndexChapterFromKeywordsAsync(string chapterId, IEnumerable<string> keywords)
        {
            if (string.IsNullOrWhiteSpace(chapterId) || keywords == null)
                return;

            var normalizedKeywords = keywords
                .Select(NormalizeKeyword)
                .Where(keyword => !string.IsNullOrEmpty(keyword))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (normalizedKeywords.Count == 0)
                return;

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var index = await LoadAsync().ConfigureAwait(false);
                foreach (var keyword in normalizedKeywords)
                {
                    if (!index.TryGetValue(keyword, out var chapters))
                    {
                        chapters = new List<string>();
                        index[keyword] = chapters;
                    }

                    if (chapters.Contains(chapterId, StringComparer.Ordinal))
                        continue;

                    chapters.Add(chapterId);
                    if (chapters.Count > MaxChaptersPerKeyword)
                        chapters.RemoveRange(0, chapters.Count - MaxChaptersPerKeyword);
                }

                await SaveAsync(index).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<List<string>> SearchAsync(IEnumerable<string> keywords, int topK = 5)
        {
            var normalizedKeywords = (keywords ?? Array.Empty<string>())
                .Select(NormalizeKeyword)
                .Where(keyword => !string.IsNullOrEmpty(keyword))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (normalizedKeywords.Count == 0)
                return new List<string>();

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var index = await LoadAsync().ConfigureAwait(false);
                var hitCount = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var keyword in normalizedKeywords)
                {
                    if (!index.TryGetValue(keyword, out var chapters))
                        continue;

                    foreach (var chapterId in chapters)
                        hitCount[chapterId] = hitCount.GetValueOrDefault(chapterId) + 1;
                }

                return hitCount
                    .OrderByDescending(hit => hit.Value)
                    .ThenBy(hit => hit.Key, StringComparer.Ordinal)
                    .Take(topK)
                    .Select(hit => hit.Key)
                    .ToList();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task RemoveChapterAsync(string chapterId)
        {
            if (string.IsNullOrWhiteSpace(chapterId))
                return;

            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var index = await LoadAsync().ConfigureAwait(false);
                var modified = false;
                foreach (var chapters in index.Values)
                    modified |= chapters.RemoveAll(id => string.Equals(id, chapterId, StringComparison.Ordinal)) > 0;

                if (modified)
                    await SaveAsync(index).ConfigureAwait(false);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<HashSet<string>> GetIndexedChapterIdsAsync()
        {
            await _lock.WaitAsync().ConfigureAwait(false);
            try
            {
                var index = await LoadAsync().ConfigureAwait(false);
                var result = new HashSet<string>(StringComparer.Ordinal);
                foreach (var chapters in index.Values)
                    result.UnionWith(chapters);
                return result;
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<Dictionary<string, List<string>>> LoadAsync()
        {
            if (_index != null)
                return _index;

            if (!File.Exists(_indexPath))
            {
                _index = new Dictionary<string, List<string>>(StringComparer.Ordinal);
                return _index;
            }

            var json = await File.ReadAllTextAsync(_indexPath).ConfigureAwait(false);
            _index = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(json, _jsonOptions)
                     ?? new Dictionary<string, List<string>>(StringComparer.Ordinal);
            _index = new Dictionary<string, List<string>>(_index, StringComparer.Ordinal);
            return _index;
        }

        private async Task SaveAsync(Dictionary<string, List<string>> index)
        {
            var json = JsonSerializer.Serialize(index, _jsonOptions);
            await File.WriteAllTextAsync(_indexPath, json).ConfigureAwait(false);
            _index = index;
        }

        private static List<string> ExtractKeywords(ChapterChanges changes)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var change in changes.CharacterStateChanges ?? new())
                AddIfNotBlank(result, change.CharacterId);

            foreach (var point in changes.NewPlotPoints ?? new())
            {
                foreach (var keyword in point.Keywords ?? new())
                    AddIfNotBlank(result, keyword);
            }

            foreach (var action in changes.ForeshadowingActions ?? new())
                AddIfNotBlank(result, action.ForeshadowId);

            foreach (var transfer in changes.ItemTransfers ?? new())
                AddIfNotBlank(result, transfer.ItemName);

            return result.ToList();
        }

        private static void AddIfNotBlank(HashSet<string> result, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                result.Add(value);
        }

        private static string NormalizeKeyword(string? keyword)
        {
            return keyword?.Trim().ToLowerInvariant() ?? string.Empty;
        }
    }
}
