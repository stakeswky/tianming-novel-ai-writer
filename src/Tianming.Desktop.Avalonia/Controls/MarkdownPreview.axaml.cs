using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace Tianming.Desktop.Avalonia.Controls;

/// <summary>
/// Markdown 预览占位实现：用 SelectableTextBlock 显示原 markdown 文本（monospace + wrap）。
/// TODO M4.3 后续：引入 Markdig 0.45.0 → MarkdownDocument visitor → 真正的 H1/H2/bold/list 渲染。
/// 当前先满足 M4.3 control + binding 契约，Gate 阶段（M4.3 Gate）再升级。
/// </summary>
public partial class MarkdownPreview : UserControl
{
    private readonly SelectableTextBlock _block;

    public static readonly StyledProperty<string> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownPreview, string>(nameof(Markdown), string.Empty);

    public string Markdown { get => GetValue(MarkdownProperty); set => SetValue(MarkdownProperty, value); }

    public MarkdownPreview()
    {
        _block = new SelectableTextBlock
        {
            FontFamily = "SF Mono, Menlo, Consolas, monospace",
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(12),
        };
        Content = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _block,
        };
        MarkdownProperty.Changed.AddClassHandler<MarkdownPreview>((c, _) => c._block.Text = c.Markdown ?? string.Empty);
    }
}
