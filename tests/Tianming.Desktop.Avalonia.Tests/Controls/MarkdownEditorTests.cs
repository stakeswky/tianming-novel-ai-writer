using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class MarkdownEditorTests
{
    [AvaloniaFact]
    public void Defaults_text_empty_writable_wrap_true()
    {
        var e = new MarkdownEditor();
        Assert.Equal(string.Empty, e.Text);
        Assert.False(e.IsReadOnly);
        Assert.True(e.WordWrap);
    }

    [AvaloniaFact]
    public void Set_text_persists_to_property()
    {
        var e = new MarkdownEditor { Text = "# hello" };
        Assert.Equal("# hello", e.Text);
    }

    [AvaloniaFact]
    public void Set_text_pushes_into_inner_editor()
    {
        var e = new MarkdownEditor { Text = "abc" };
        Assert.Equal("abc", e.InnerEditorText);
    }

    [AvaloniaFact]
    public void Typing_in_inner_editor_updates_Text_property()
    {
        var e = new MarkdownEditor();
        e.SetInnerEditorTextForTest("typed-by-user");
        Assert.Equal("typed-by-user", e.Text);
    }

    [AvaloniaFact]
    public void Toggle_readonly_persists()
    {
        var e = new MarkdownEditor { IsReadOnly = true };
        Assert.True(e.IsReadOnly);
    }
}
