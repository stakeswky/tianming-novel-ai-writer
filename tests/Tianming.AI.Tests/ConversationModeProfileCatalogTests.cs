using TM.Framework.UI.Workspace.RightPanel.Modes;
using TM.Services.Framework.AI.SemanticKernel.Conversation.Config;
using Xunit;

namespace Tianming.AI.Tests;

public class ConversationModeProfileCatalogTests
{
    [Fact]
    public void Catalog_contains_ask_plan_and_agent_profiles()
    {
        var profiles = ConversationModeProfileCatalog.All;

        Assert.Equal([ChatMode.Ask, ChatMode.Agent, ChatMode.Plan], profiles.Keys.OrderBy(mode => (int)mode).ToArray());
        Assert.False(profiles[ChatMode.Ask].RequiresExecutionEngine);
        Assert.True(profiles[ChatMode.Plan].RequiresExecutionEngine);
        Assert.True(profiles[ChatMode.Agent].RequiresExecutionEngine);
    }

    [Fact]
    public void Plan_profile_targets_execution_plan_and_hides_raw_content()
    {
        var profile = ConversationModeProfileCatalog.GetProfile(ChatMode.Plan);

        Assert.Equal("ExecutionPlan", profile.DisplayPolicy.DefaultPayloadTarget);
        Assert.True(profile.DisplayPolicy.HideRawContentInBubble);
        Assert.True(profile.DisplayPolicy.ShowAnalysis);
        Assert.Contains("计划模式", profile.Description);
    }

    [Fact]
    public void Agent_profile_targets_execution_panel_and_keeps_raw_content_visible()
    {
        var profile = ConversationModeProfileCatalog.GetProfile(ChatMode.Agent);

        Assert.Equal("ExecutionPanel", profile.DisplayPolicy.DefaultPayloadTarget);
        Assert.False(profile.DisplayPolicy.HideRawContentInBubble);
        Assert.True(profile.RequiresExecutionEngine);
        Assert.Contains("代理模式", profile.Description);
    }

    [Fact]
    public void Unknown_mode_falls_back_to_ask_profile()
    {
        var profile = ConversationModeProfileCatalog.GetProfile((ChatMode)99);

        Assert.Equal(ChatMode.Ask, profile.Mode);
        Assert.Null(profile.DisplayPolicy.DefaultPayloadTarget);
        Assert.False(profile.RequiresExecutionEngine);
    }
}
