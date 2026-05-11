using System;
using System.IO;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Framework.AI.SemanticKernel;

public sealed class FileVectorChapterIndexAdapter : IChapterDerivedIndex
{
    private readonly FileVectorSearchService _vectorSearchService;

    public FileVectorChapterIndexAdapter(FileVectorSearchService vectorSearchService)
    {
        _vectorSearchService = vectorSearchService ?? throw new ArgumentNullException(nameof(vectorSearchService));
    }

    public async Task IndexChapterAsync(
        string chapterId,
        string chapterFilePath,
        string persistedContent,
        ChapterChanges? changes)
    {
        if (string.IsNullOrWhiteSpace(chapterId))
            return;

        await _vectorSearchService.RemoveChapterAsync(chapterId).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(chapterFilePath) || !File.Exists(chapterFilePath))
            return;

        await _vectorSearchService.SearchByChapterAsync(chapterId, topK: 1).ConfigureAwait(false);
    }

    public Task RemoveChapterAsync(string chapterId)
    {
        return _vectorSearchService.RemoveChapterAsync(chapterId);
    }
}
