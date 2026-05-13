using System;
using System.Collections.ObjectModel;
using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class ConversationBubbleTests
{
    [AvaloniaFact]
    public void Defaults_RoleUser_ContentEmpty_NotStreaming()
    {
        var b = new ConversationBubble();
        Assert.Equal(ConversationRole.User, b.Role);
        Assert.Equal(string.Empty, b.ContentText);
        Assert.Null(b.ThinkingBlock);
        Assert.Null(b.References);
        Assert.False(b.IsStreaming);
    }

    [AvaloniaFact]
    public void SetAssistantContent_Persists()
    {
        var b = new ConversationBubble
        {
            Role = ConversationRole.Assistant,
            ContentText = "好的，根据剧情设计……",
            IsStreaming = true,
            Timestamp = new DateTime(2026, 5, 13, 14, 30, 0)
        };
        Assert.Equal(ConversationRole.Assistant, b.Role);
        Assert.True(b.IsStreaming);
        Assert.Contains("剧情", b.ContentText);
    }

    [AvaloniaFact]
    public void References_CanBeSet()
    {
        var refs = new ObservableCollection<ReferenceTag>
        {
            new("第 12 章 · 盟约之城"),
            new("世界规则 · 灵气体系"),
        };
        var b = new ConversationBubble { References = refs };
        Assert.Equal(2, b.References!.Count);
    }
}
