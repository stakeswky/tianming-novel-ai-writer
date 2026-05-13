using System;
using System.IO;
using System.Threading.Tasks;

namespace Tianming.Desktop.Avalonia.Infrastructure;

/// <summary>
/// 文件草稿存储：root/{projectId}/{chapterId}.md。
/// 直接写文件（atomic via temp + rename 留给 M6.3 WAL，本任务先简单覆盖写）。
/// </summary>
public sealed class FileChapterDraftStore : IChapterDraftStore
{
    private readonly string _root;

    public FileChapterDraftStore(string root)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("root", nameof(root));
        _root = root;
    }

    public async Task SaveDraftAsync(string projectId, string chapterId, string content)
    {
        if (string.IsNullOrWhiteSpace(projectId)) throw new ArgumentException("projectId", nameof(projectId));
        if (string.IsNullOrWhiteSpace(chapterId)) throw new ArgumentException("chapterId", nameof(chapterId));

        var dir = Path.Combine(_root, projectId);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{chapterId}.md");
        await File.WriteAllTextAsync(path, content ?? string.Empty).ConfigureAwait(false);
    }

    public async Task<string?> LoadDraftAsync(string projectId, string chapterId)
    {
        if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(chapterId)) return null;
        var path = Path.Combine(_root, projectId, $"{chapterId}.md");
        if (!File.Exists(path)) return null;
        return await File.ReadAllTextAsync(path).ConfigureAwait(false);
    }
}
