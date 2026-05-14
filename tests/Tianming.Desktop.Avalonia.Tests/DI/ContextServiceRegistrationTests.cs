using Microsoft.Extensions.DependencyInjection;
using TM.Services.Modules.ProjectData.Context;
using Tianming.Desktop.Avalonia;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.DI;

public class ContextServiceRegistrationTests
{
    [Fact]
    public void Build_resolves_all_four_context_services()
    {
        using var sp = (ServiceProvider)AppHost.Build();

        Assert.NotNull(sp.GetRequiredService<IDesignContextService>());
        Assert.NotNull(sp.GetRequiredService<IGenerationContextService>());
        Assert.NotNull(sp.GetRequiredService<IValidationContextService>());
        Assert.NotNull(sp.GetRequiredService<IPackagingContextService>());
    }
}
