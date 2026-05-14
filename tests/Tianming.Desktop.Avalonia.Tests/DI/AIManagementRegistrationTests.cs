using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia;
using Tianming.Desktop.Avalonia.Navigation;
using Tianming.Desktop.Avalonia.ViewModels.AI;
using Xunit;
using TM.Services.Framework.AI.Core.Routing;

namespace Tianming.Desktop.Avalonia.Tests.DI;

public class AIManagementRegistrationTests
{
    [Fact]
    public void Build_RegistersAllAIManagementPages()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var reg = sp.GetRequiredService<PageRegistry>();

        Assert.Contains(PageKeys.AIModels, reg.Keys);
        Assert.Contains(PageKeys.AIKeys, reg.Keys);
        Assert.Contains(PageKeys.AIPrompts, reg.Keys);
        Assert.Contains(PageKeys.AIUsage, reg.Keys);
    }

    [Fact]
    public void Build_ResolvesAllAIManagementViewModels()
    {
        using var sp = (ServiceProvider)AppHost.Build();

        Assert.NotNull(sp.GetRequiredService<ModelManagementViewModel>());
        Assert.NotNull(sp.GetRequiredService<ApiKeysViewModel>());
        Assert.NotNull(sp.GetRequiredService<PromptManagementViewModel>());
        Assert.NotNull(sp.GetRequiredService<UsageStatisticsViewModel>());
    }

    [Fact]
    public void Build_RegistersRouterServices()
    {
        using var sp = (ServiceProvider)AppHost.Build();

        Assert.NotNull(sp.GetRequiredService<IAIModelRouter>());
        Assert.NotNull(sp.GetRequiredService<RoutedChatClient>());
    }
}
