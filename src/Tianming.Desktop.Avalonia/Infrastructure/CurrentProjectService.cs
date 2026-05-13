using System.IO;

namespace Tianming.Desktop.Avalonia.Infrastructure;

/// <summary>
/// M4.1 项目根目录契约。当前用 AppPaths/Projects/Default；M4 后续接 FileProjectManager 时换实现即可。
/// </summary>
public interface ICurrentProjectService
{
    string ProjectRoot { get; }
}

public sealed class CurrentProjectService : ICurrentProjectService
{
    public CurrentProjectService(AppPaths paths)
    {
        ProjectRoot = Path.Combine(paths.AppSupportDirectory, "Projects", "Default");
    }

    public string ProjectRoot { get; }
}
