using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using TM.Services.Modules.ProjectData.Generation.Wal;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Infrastructure;

public class AvaloniaShellServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAvaloniaShell_registers_generation_wal_services()
    {
        var services = new ServiceCollection();

        services.AddAvaloniaShell();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(IGenerationJournal));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(GenerationRecoveryService));
    }
}
