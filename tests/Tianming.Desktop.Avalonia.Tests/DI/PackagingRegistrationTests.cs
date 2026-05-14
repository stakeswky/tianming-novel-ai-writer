using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using TM.Services.Modules.ProjectData.Backup;
using TM.Services.Modules.ProjectData.Packaging;
using TM.Services.Modules.ProjectData.Packaging.Preflight;
using Tianming.Desktop.Avalonia.Infrastructure;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.ViewModels.Packaging;
using Tianming.Desktop.Avalonia.ViewModels.Shell;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.DI;

public class PackagingRegistrationTests
{
    [Fact]
    public void AddAvaloniaShell_registers_packaging_services_page_and_enabled_nav()
    {
        using var workspace = new TempDirectory();
        var services = new ServiceCollection();
        services.AddAvaloniaShell();
        services.AddSingleton<ICurrentProjectService>(new StubCurrentProjectService(workspace.Path));

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IPreflightChecker>());
        Assert.NotNull(provider.GetRequiredService<IBookExporter>());
        Assert.NotNull(provider.GetRequiredService<IProjectBackupService>());
        Assert.NotNull(provider.GetRequiredService<PackagingViewModel>());

        var registry = provider.GetRequiredService<PageRegistry>();
        Assert.Contains(PageKeys.Packaging, registry.Keys);

        var leftNav = provider.GetRequiredService<LeftNavViewModel>();
        var packagingItem = Assert.Single(
            Assert.Single(leftNav.Groups, group => group.Title == "工具").Items,
            item => item.Key == PageKeys.Packaging);
        Assert.True(packagingItem.IsEnabled);
    }

    private sealed class StubCurrentProjectService(string projectRoot) : ICurrentProjectService
    {
        public string ProjectRoot { get; } = projectRoot;
    }

    private sealed class TempDirectory : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"tm-pkg-di-{Guid.NewGuid():N}");

        public TempDirectory()
        {
            Directory.CreateDirectory(Path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
