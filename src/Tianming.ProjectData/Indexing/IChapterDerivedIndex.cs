using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public interface IChapterDerivedIndex
    {
        Task IndexChapterAsync(string chapterId, string chapterFilePath, string persistedContent, ChapterChanges? changes);
        Task RemoveChapterAsync(string chapterId);
    }
}
