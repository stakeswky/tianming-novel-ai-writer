using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Tianming.Desktop.Avalonia;
using TM.Services.Framework.AI.Core.Routing;
using TM.Services.Framework.AI.SemanticKernel;
using TM.Services.Framework.AI.SemanticKernel.Conversation;
using TM.Services.Modules.ProjectData.StagedChanges;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.DI;

public class ConversationRegistrationTests
{
    [Fact]
    public void Build_ResolvesConversationOrchestrator()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var orchestrator = sp.GetRequiredService<ConversationOrchestrator>();
        Assert.NotNull(orchestrator);
    }

    [Fact]
    public void Build_ResolvesAllSixConversationTools()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var tools = sp.GetServices<IConversationTool>();
        Assert.Equal(6, tools.Count());
        Assert.Contains(tools, tool => tool.Name == "content_edit");
        Assert.Contains(tools, tool => tool.Name == "data_edit");
        Assert.Contains(tools, tool => tool.Name == "workspace_edit");
    }

    [Fact]
    public void Build_ResolvesFileSessionStore()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var store = sp.GetRequiredService<IFileSessionStore>();
        Assert.NotNull(store);
    }

    [Fact]
    public void Build_WiresRouterIntoConversationOrchestrator()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        var orchestrator = sp.GetRequiredService<ConversationOrchestrator>();
        var routerField = typeof(ConversationOrchestrator)
            .GetField("_router", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(routerField);
        Assert.Same(
            sp.GetRequiredService<IAIModelRouter>(),
            routerField!.GetValue(orchestrator));
    }

    [Fact]
    public void Build_ResolvesStagedChangeServices()
    {
        using var sp = (ServiceProvider)AppHost.Build();
        Assert.NotNull(sp.GetRequiredService<IStagedChangeStore>());
        Assert.NotNull(sp.GetRequiredService<IStagedChangeApprover>());
    }
}
