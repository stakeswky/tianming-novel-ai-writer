namespace Tianming.Desktop.Avalonia.Messaging;

/// <summary>
/// M4.4：章节内容已应用到编辑器 — 用于通知其他页面（如 ChapterPlanning）章节已生成。
/// </summary>
public sealed record ChapterAppliedEvent(string ChapterId);
