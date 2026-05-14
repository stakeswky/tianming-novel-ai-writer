using System.Collections.Generic;
using TM.Services.Framework.AI.Core;
using TM.Services.Framework.AI.Core.Routing;
using Xunit;

namespace Tianming.AI.Tests.Core.Routing;

public class DefaultAIModelRouterTests
{
    private static List<UserConfiguration> SampleConfigs() =>
    [
        new() { Id = "c1", Purpose = "Chat", IsEnabled = true, ModelId = "haiku" },
        new() { Id = "c2", Purpose = "Writing", IsEnabled = true, ModelId = "opus" },
        new() { Id = "c3", Purpose = "Default", IsEnabled = true, IsActive = true, ModelId = "sonnet" },
    ];

    [Fact]
    public void Routes_chat_to_chat_config()
    {
        var router = new DefaultAIModelRouter(SampleConfigs);

        var cfg = router.Resolve(AITaskPurpose.Chat);

        Assert.Equal("haiku", cfg.ModelId);
    }

    [Fact]
    public void Falls_back_to_active_default_when_purpose_not_found()
    {
        var router = new DefaultAIModelRouter(SampleConfigs);

        var cfg = router.Resolve(AITaskPurpose.Polish);

        Assert.Equal("sonnet", cfg.ModelId);
    }

    [Fact]
    public void Throws_when_no_default_and_purpose_missing()
    {
        var router = new DefaultAIModelRouter(() => new List<UserConfiguration>());

        Assert.Throws<InvalidOperationException>(() => router.Resolve(AITaskPurpose.Chat));
    }
}
