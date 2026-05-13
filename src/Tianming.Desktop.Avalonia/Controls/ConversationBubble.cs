using System;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls.Primitives;

namespace Tianming.Desktop.Avalonia.Controls;

public class ConversationBubble : TemplatedControl
{
    public static readonly StyledProperty<ConversationRole> RoleProperty =
        AvaloniaProperty.Register<ConversationBubble, ConversationRole>(nameof(Role), ConversationRole.User);
    public static readonly StyledProperty<string> ContentTextProperty =
        AvaloniaProperty.Register<ConversationBubble, string>(nameof(ContentText), string.Empty);
    public static readonly StyledProperty<string?> ThinkingBlockProperty =
        AvaloniaProperty.Register<ConversationBubble, string?>(nameof(ThinkingBlock));
    public static readonly StyledProperty<ObservableCollection<ReferenceTag>?> ReferencesProperty =
        AvaloniaProperty.Register<ConversationBubble, ObservableCollection<ReferenceTag>?>(nameof(References));
    public static readonly StyledProperty<bool> IsStreamingProperty =
        AvaloniaProperty.Register<ConversationBubble, bool>(nameof(IsStreaming), false);
    public static readonly StyledProperty<DateTime> TimestampProperty =
        AvaloniaProperty.Register<ConversationBubble, DateTime>(nameof(Timestamp));

    public ConversationRole Role { get => GetValue(RoleProperty); set => SetValue(RoleProperty, value); }
    public string ContentText { get => GetValue(ContentTextProperty); set => SetValue(ContentTextProperty, value); }
    public string? ThinkingBlock { get => GetValue(ThinkingBlockProperty); set => SetValue(ThinkingBlockProperty, value); }
    public ObservableCollection<ReferenceTag>? References { get => GetValue(ReferencesProperty); set => SetValue(ReferencesProperty, value); }
    public bool IsStreaming { get => GetValue(IsStreamingProperty); set => SetValue(IsStreamingProperty, value); }
    public DateTime Timestamp { get => GetValue(TimestampProperty); set => SetValue(TimestampProperty, value); }
}
