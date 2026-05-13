using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class MarkdownPreviewTests
{
    [AvaloniaFact]
    public void Defaults_markdown_empty()
    {
        var p = new MarkdownPreview();
        Assert.Equal(string.Empty, p.Markdown);
    }

    [AvaloniaFact]
    public void Set_markdown_persists()
    {
        var p = new MarkdownPreview { Markdown = "# title\nbody" };
        Assert.Equal("# title\nbody", p.Markdown);
    }

    [AvaloniaFact]
    public void Update_markdown_raises_property_changed()
    {
        var p = new MarkdownPreview();
        var changed = 0;
        p.PropertyChanged += (_, e) => { if (e.Property == MarkdownPreview.MarkdownProperty) changed++; };
        p.Markdown = "x";
        Assert.Equal(1, changed);
    }
}
