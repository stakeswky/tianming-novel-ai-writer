namespace Tianming.Desktop.Avalonia.Controls;

/// <summary>顶部章节 tab 一项。VM 暴露给 ChapterTabBar 的 immutable 投影。</summary>
public sealed record ChapterTabItem(string ChapterId, string Title, bool IsDirty, bool IsActive);
