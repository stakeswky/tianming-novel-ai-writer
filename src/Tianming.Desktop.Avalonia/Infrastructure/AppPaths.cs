using System;
using System.IO;

namespace Tianming.Desktop.Avalonia.Infrastructure;

public sealed class AppPaths
{
    public string AppSupportDirectory { get; }
    public string CachesDirectory { get; }
    public string LogsDirectory { get; }

    public AppPaths(string libraryRoot)
    {
        AppSupportDirectory = Path.Combine(libraryRoot, "Application Support", "Tianming");
        CachesDirectory     = Path.Combine(libraryRoot, "Caches", "Tianming");
        LogsDirectory       = Path.Combine(libraryRoot, "Logs", "Tianming");
    }

    public static AppPaths Default { get; } = Create();

    private static AppPaths Create()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return new AppPaths(Path.Combine(home, "Library"));
    }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(AppSupportDirectory);
        Directory.CreateDirectory(CachesDirectory);
        Directory.CreateDirectory(LogsDirectory);
    }
}
