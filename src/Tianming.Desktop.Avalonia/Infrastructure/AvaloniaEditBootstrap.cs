using AvaloniaEdit.Highlighting;

namespace Tianming.Desktop.Avalonia.Infrastructure;

/// <summary>
/// 在 App.OnFrameworkInitializationCompleted 早期调用一次。
/// 确保 AvaloniaEdit 高亮 / 字体在 CodeViewer / 章节编辑器 (M4.3) 渲染前就绪。
/// </summary>
internal static class AvaloniaEditBootstrap
{
    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;

        // AvaloniaEdit 自带 JSON / MarkDown / XML / C# / JS 等高亮，HighlightingManager.Instance 默认已注册。
        // 这里仅触发一次 GetDefinition() 来确认 manager 起来；缺失则 fail-fast。
        var json = HighlightingManager.Instance.GetDefinition("Json")
                   ?? throw new System.InvalidOperationException("AvaloniaEdit 缺少 Json highlighting");
        var md = HighlightingManager.Instance.GetDefinition("MarkDown")
                 ?? throw new System.InvalidOperationException("AvaloniaEdit 缺少 MarkDown highlighting");
        _ = json;
        _ = md;
    }
}
