using Avalonia.Headless.XUnit;
using Tianming.Desktop.Avalonia.Controls;
using Xunit;

namespace Tianming.Desktop.Avalonia.Tests.Controls;

public class CodeViewerTests
{
    [AvaloniaFact]
    public void Defaults_CodeEmpty_LanguagePlain_ShowLineNumbersTrue()
    {
        var v = new CodeViewer();
        Assert.Equal(string.Empty, v.Code);
        Assert.Equal(CodeLanguage.Plain, v.Language);
        Assert.True(v.ShowLineNumbers);
        Assert.False(v.WordWrap);
    }

    [AvaloniaFact]
    public void SetCodeAndLanguage_Persists()
    {
        var v = new CodeViewer { Code = "{ \"x\": 1 }", Language = CodeLanguage.Json };
        Assert.Contains("\"x\"", v.Code);
        Assert.Equal(CodeLanguage.Json, v.Language);
    }

    [AvaloniaFact]
    public void ToggleWordWrap_Persists()
    {
        var v = new CodeViewer { WordWrap = true };
        Assert.True(v.WordWrap);
    }
}
