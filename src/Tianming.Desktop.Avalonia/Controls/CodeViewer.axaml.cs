using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;

namespace Tianming.Desktop.Avalonia.Controls;

public partial class CodeViewer : UserControl
{
    private readonly TextEditor _editor;

    public static readonly StyledProperty<string> CodeProperty =
        AvaloniaProperty.Register<CodeViewer, string>(nameof(Code), string.Empty);
    public static readonly StyledProperty<CodeLanguage> LanguageProperty =
        AvaloniaProperty.Register<CodeViewer, CodeLanguage>(nameof(Language), CodeLanguage.Plain);
    public static readonly StyledProperty<bool> ShowLineNumbersProperty =
        AvaloniaProperty.Register<CodeViewer, bool>(nameof(ShowLineNumbers), true);
    public static readonly StyledProperty<bool> WordWrapProperty =
        AvaloniaProperty.Register<CodeViewer, bool>(nameof(WordWrap), false);

    public string Code { get => GetValue(CodeProperty); set => SetValue(CodeProperty, value); }
    public CodeLanguage Language { get => GetValue(LanguageProperty); set => SetValue(LanguageProperty, value); }
    public bool ShowLineNumbers { get => GetValue(ShowLineNumbersProperty); set => SetValue(ShowLineNumbersProperty, value); }
    public bool WordWrap { get => GetValue(WordWrapProperty); set => SetValue(WordWrapProperty, value); }

    public CodeViewer()
    {
        _editor = new TextEditor
        {
            IsReadOnly = true,
            ShowLineNumbers = true,
            WordWrap = false,
            FontFamily = "SF Mono, Menlo, Consolas, monospace",
        };
        Content = _editor;
        Apply();

        CodeProperty.Changed.AddClassHandler<CodeViewer>((c, _) => c.Apply());
        LanguageProperty.Changed.AddClassHandler<CodeViewer>((c, _) => c.Apply());
        ShowLineNumbersProperty.Changed.AddClassHandler<CodeViewer>((c, _) => c._editor.ShowLineNumbers = c.ShowLineNumbers);
        WordWrapProperty.Changed.AddClassHandler<CodeViewer>((c, _) => c._editor.WordWrap = c.WordWrap);
    }

    private void Apply()
    {
        _editor.Text = Code ?? string.Empty;
        _editor.SyntaxHighlighting = Language switch
        {
            CodeLanguage.Json     => HighlightingManager.Instance.GetDefinition("Json"),
            CodeLanguage.Markdown => HighlightingManager.Instance.GetDefinition("MarkDown"),
            _                     => null
        };
    }
}
