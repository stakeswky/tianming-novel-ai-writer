using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;

namespace Tianming.Desktop.Avalonia.Controls;

/// <summary>
/// AvaloniaEdit TextEditor 可写包装；Markdown 语法高亮；Text TwoWay binding 友好。
/// 注：AvaloniaEdit 11.0.6 的 TextEditor.TextProperty 是 StyledProperty&lt;string&gt;，
/// TextChanged 事件在用户输入后触发。这里手工双向桥接以确保 Avalonia binding 看见 update。
/// </summary>
public partial class MarkdownEditor : UserControl
{
    private readonly TextEditor _editor;
    private bool _suppressSync;

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<MarkdownEditor, string>(
            nameof(Text), string.Empty,
            defaultBindingMode: global::Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<MarkdownEditor, bool>(nameof(IsReadOnly), false);

    public static readonly StyledProperty<bool> WordWrapProperty =
        AvaloniaProperty.Register<MarkdownEditor, bool>(nameof(WordWrap), true);

    public string Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public bool IsReadOnly { get => GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }
    public bool WordWrap { get => GetValue(WordWrapProperty); set => SetValue(WordWrapProperty, value); }

    /// <summary>测试用（public 是因为测试程序集没配 InternalsVisibleTo）：读取内部 editor 当前文本。</summary>
    public string InnerEditorText => _editor.Text ?? string.Empty;

    /// <summary>测试用：模拟用户在 editor 里输入，验证 binding 出口。</summary>
    public void SetInnerEditorTextForTest(string value)
    {
        _editor.Text = value;
    }

    public MarkdownEditor()
    {
        _editor = new TextEditor
        {
            IsReadOnly = false,
            ShowLineNumbers = false,
            WordWrap = true,
            FontFamily = "SF Mono, Menlo, Consolas, monospace",
            SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("MarkDown"),
        };
        Content = _editor;

        // editor → Text
        _editor.TextChanged += (_, _) =>
        {
            if (_suppressSync) return;
            _suppressSync = true;
            try { SetCurrentValue(TextProperty, _editor.Text ?? string.Empty); }
            finally { _suppressSync = false; }
        };

        // Text → editor
        TextProperty.Changed.AddClassHandler<MarkdownEditor>((c, _) =>
        {
            if (c._suppressSync) return;
            c._suppressSync = true;
            try
            {
                var v = c.Text ?? string.Empty;
                if (c._editor.Text != v) c._editor.Text = v;
            }
            finally { c._suppressSync = false; }
        });

        IsReadOnlyProperty.Changed.AddClassHandler<MarkdownEditor>((c, _) => c._editor.IsReadOnly = c.IsReadOnly);
        WordWrapProperty.Changed.AddClassHandler<MarkdownEditor>((c, _) => c._editor.WordWrap = c.WordWrap);
    }
}
