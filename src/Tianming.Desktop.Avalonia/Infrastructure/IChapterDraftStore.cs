using System.Threading.Tasks;

namespace Tianming.Desktop.Avalonia.Infrastructure;

/// <summary>
/// 章节草稿存储。M4.3 用文件实现；M6 之后可换 SQLite / WAL 实现而不影响 VM。
/// </summary>
public interface IChapterDraftStore
{
    Task SaveDraftAsync(string projectId, string chapterId, string content);
    Task<string?> LoadDraftAsync(string projectId, string chapterId);
}
