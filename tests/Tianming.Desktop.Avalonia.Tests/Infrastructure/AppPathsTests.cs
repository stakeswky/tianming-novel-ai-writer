using System;
using System.IO;
using Tianming.Desktop.Avalonia.Infrastructure;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class AppPathsTests
{
    [Fact]
    public void AppSupport_UnderLibraryApplicationSupport()
    {
        var p = AppPaths.Default.AppSupportDirectory;
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Assert.StartsWith(Path.Combine(home, "Library", "Application Support"), p);
        Assert.EndsWith("Tianming", p);
    }

    [Fact]
    public void CustomRoot_OverridesAllPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tianming-test-{Guid.NewGuid():N}");
        try
        {
            var paths = new AppPaths(root);
            Assert.Equal(Path.Combine(root, "Application Support", "Tianming"), paths.AppSupportDirectory);
            Assert.Equal(Path.Combine(root, "Caches", "Tianming"), paths.CachesDirectory);
            Assert.Equal(Path.Combine(root, "Logs", "Tianming"), paths.LogsDirectory);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EnsureDirectories_CreatesAll()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tianming-test-{Guid.NewGuid():N}");
        try
        {
            var paths = new AppPaths(root);
            paths.EnsureDirectories();
            Assert.True(Directory.Exists(paths.AppSupportDirectory));
            Assert.True(Directory.Exists(paths.CachesDirectory));
            Assert.True(Directory.Exists(paths.LogsDirectory));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
