using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Framework.AI.SemanticKernel;

public sealed class FileVectorSearchService
{
    private const int FallbackChapterCacheSize = 12;
    private const int ChunkTargetSize = 500;
    private const int ChunkOverlap = 50;

    private readonly string _chaptersDirectory;
    private readonly Dictionary<string, List<ChapterChunk>> _chunkCache = new(StringComparer.Ordinal);
    private readonly LinkedList<string> _lru = new();
    private readonly Dictionary<string, LinkedListNode<string>> _lruNodes = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    private readonly ITextEmbedder? _embedder;
    private bool _isInitialized;

    public FileVectorSearchService(string chaptersDirectory, ITextEmbedder? embedder = null)
    {
        if (string.IsNullOrWhiteSpace(chaptersDirectory))
            throw new ArgumentException("章节目录不能为空", nameof(chaptersDirectory));

        _chaptersDirectory = chaptersDirectory;
        _embedder = embedder;
    }

    public bool IsAvailable => _isInitialized;
    public VectorSearchMode CurrentMode { get; private set; } = VectorSearchMode.None;

    public Task InitializeAsync()
    {
        CurrentMode = _embedder == null ? VectorSearchMode.Keyword : VectorSearchMode.LocalEmbedding;
        _isInitialized = true;
        return Task.CompletedTask;
    }

    public async Task<List<VectorSearchResult>> SearchAsync(string query, int topK = 5)
    {
        if (!_isInitialized)
            await InitializeAsync().ConfigureAwait(false);

        if (!Directory.Exists(_chaptersDirectory) || topK <= 0)
            return new List<VectorSearchResult>();

        if (_embedder != null)
            return await SearchWithEmbedderAsync(query, topK).ConfigureAwait(false);

        var queryTerms = SplitQueryTerms(query);
        if (queryTerms.Length == 0)
            return new List<VectorSearchResult>();

        var topResults = new List<VectorSearchResult>(topK);
        foreach (var file in Directory.GetFiles(_chaptersDirectory, "*.md", SearchOption.TopDirectoryOnly))
        {
            var chapterId = Path.GetFileNameWithoutExtension(file);
            var chunks = await GetOrLoadChunksAsync(chapterId, file).ConfigureAwait(false);
            foreach (var chunk in chunks)
            {
                var score = CalculateRelevance(queryTerms, chunk.Content);
                if (score <= 0)
                    continue;

                if (topResults.Count == topK && score <= topResults[^1].Score)
                    continue;

                var result = new VectorSearchResult
                {
                    ChapterId = chunk.ChapterId,
                    Position = chunk.Position,
                    Content = chunk.Content,
                    Score = score
                };

                var insertIndex = 0;
                while (insertIndex < topResults.Count && topResults[insertIndex].Score >= result.Score)
                    insertIndex++;

                topResults.Insert(insertIndex, result);
                if (topResults.Count > topK)
                    topResults.RemoveAt(topResults.Count - 1);
            }
        }

        return topResults;
    }

    private async Task<List<VectorSearchResult>> SearchWithEmbedderAsync(string query, int topK)
    {
        if (_embedder == null || string.IsNullOrWhiteSpace(query))
            return new List<VectorSearchResult>();

        var queryVector = _embedder.Embed(query);
        if (queryVector.Length == 0)
            return new List<VectorSearchResult>();

        var results = new List<VectorSearchResult>();
        foreach (var file in Directory.GetFiles(_chaptersDirectory, "*.md", SearchOption.TopDirectoryOnly))
        {
            var chapterId = Path.GetFileNameWithoutExtension(file);
            var chunks = await GetOrLoadChunksAsync(chapterId, file).ConfigureAwait(false);
            foreach (var chunk in chunks)
            {
                var score = _embedder.Similarity(queryVector, _embedder.Embed(chunk.Content));
                if (score <= 0)
                    continue;

                results.Add(new VectorSearchResult
                {
                    ChapterId = chunk.ChapterId,
                    Position = chunk.Position,
                    Content = chunk.Content,
                    Score = score
                });
            }
        }

        return results
            .OrderByDescending(result => result.Score)
            .Take(topK)
            .ToList();
    }

    public async Task<List<VectorSearchResult>> SearchByChapterAsync(string chapterId, int topK = 2)
    {
        if (!_isInitialized)
            await InitializeAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(chapterId) || topK <= 0)
            return new List<VectorSearchResult>();

        var filePath = Path.Combine(_chaptersDirectory, $"{chapterId}.md");
        if (!File.Exists(filePath))
            return new List<VectorSearchResult>();

        var chunks = await GetOrLoadChunksAsync(chapterId, filePath).ConfigureAwait(false);
        return chunks
            .OrderBy(chunk => chunk.Position)
            .Take(topK)
            .Select(chunk => new VectorSearchResult
            {
                ChapterId = chunk.ChapterId,
                Position = chunk.Position,
                Content = chunk.Content,
                Score = 1.0
            })
            .ToList();
    }

    public async Task RemoveChapterAsync(string chapterId)
    {
        if (string.IsNullOrWhiteSpace(chapterId))
            return;

        await _cacheLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _chunkCache.Remove(chapterId);
            if (_lruNodes.TryGetValue(chapterId, out var node))
            {
                _lru.Remove(node);
                _lruNodes.Remove(chapterId);
            }
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    public void ClearIndex()
    {
        _cacheLock.Wait();
        try
        {
            _chunkCache.Clear();
            _lru.Clear();
            _lruNodes.Clear();
            _isInitialized = false;
            CurrentMode = VectorSearchMode.None;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task<List<ChapterChunk>> GetOrLoadChunksAsync(string chapterId, string filePath)
    {
        await _cacheLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_chunkCache.TryGetValue(chapterId, out var cached))
            {
                TouchLru(chapterId);
                return cached;
            }
        }
        finally
        {
            _cacheLock.Release();
        }

        var content = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        var chunks = ChunkContent(chapterId, content);

        await _cacheLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_chunkCache.TryGetValue(chapterId, out var cached))
            {
                TouchLru(chapterId);
                return cached;
            }

            _chunkCache[chapterId] = chunks;
            TouchLru(chapterId);
            TrimCache();
            return chunks;
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private void TouchLru(string chapterId)
    {
        if (_lruNodes.TryGetValue(chapterId, out var node))
            _lru.Remove(node);

        _lruNodes[chapterId] = _lru.AddLast(chapterId);
    }

    private void TrimCache()
    {
        while (_lru.Count > FallbackChapterCacheSize)
        {
            var chapterId = _lru.First?.Value;
            if (string.IsNullOrEmpty(chapterId))
                break;

            _lru.RemoveFirst();
            _lruNodes.Remove(chapterId);
            _chunkCache.Remove(chapterId);
        }
    }

    private static List<ChapterChunk> ChunkContent(string chapterId, string content)
    {
        var chunks = new List<ChapterChunk>();
        if (string.IsNullOrEmpty(content))
            return chunks;

        var paragraphs = content.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var position = 0;
        var buffer = string.Empty;
        foreach (var paragraph in paragraphs)
        {
            var trimmed = paragraph.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            buffer += (buffer.Length > 0 ? "\n" : string.Empty) + trimmed;
            if (buffer.Length >= ChunkTargetSize)
            {
                chunks.Add(new ChapterChunk
                {
                    ChapterId = chapterId,
                    Position = position++,
                    Content = buffer
                });

                var overlapStart = Math.Max(0, buffer.Length - ChunkOverlap);
                buffer = buffer[overlapStart..];
            }
        }

        if (!string.IsNullOrEmpty(buffer))
        {
            chunks.Add(new ChapterChunk
            {
                ChapterId = chapterId,
                Position = position,
                Content = buffer
            });
        }

        return chunks;
    }

    private static string[] SplitQueryTerms(string query)
    {
        return (query ?? string.Empty)
            .Split(new[] { ' ', '，', ',', '、' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static double CalculateRelevance(string[] queryTerms, string content)
    {
        if (queryTerms.Length == 0 || string.IsNullOrEmpty(content))
            return 0;

        var matchCount = queryTerms.Count(term => content.Contains(term, StringComparison.OrdinalIgnoreCase));
        return (double)matchCount / queryTerms.Length;
    }
}

public sealed class ChapterChunk
{
    public string ChapterId { get; set; } = string.Empty;
    public int Position { get; set; }
    public string Content { get; set; } = string.Empty;
}

public sealed class VectorSearchResult
{
    public string ChapterId { get; set; } = string.Empty;
    public int Position { get; set; }
    public string Content { get; set; } = string.Empty;
    public double Score { get; set; }
}

public enum VectorSearchMode
{
    None,
    Keyword,
    LocalEmbedding,
    Hybrid
}
