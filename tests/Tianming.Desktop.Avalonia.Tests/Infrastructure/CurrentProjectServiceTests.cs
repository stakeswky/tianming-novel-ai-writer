using System.IO;
using Tianming.Desktop.Avalonia.Infrastructure;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class CurrentProjectServiceTests
{
    [Fact]
    public void ProjectRoot_combines_app_support_with_Projects_Default()
    {
        var paths = new AppPaths(libraryRoot: "/tmp/test-lib");
        var svc = new CurrentProjectService(paths);

        var expected = Path.Combine("/tmp/test-lib", "Application Support", "Tianming", "Projects", "Default");
        Assert.Equal(expected, svc.ProjectRoot);
    }

    [Fact]
    public void ProjectRoot_is_non_empty()
    {
        var paths = new AppPaths(libraryRoot: "/var/folders/xxx");
        var svc = new CurrentProjectService(paths);

        Assert.False(string.IsNullOrWhiteSpace(svc.ProjectRoot));
    }
}
