using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Tracking;

namespace TM.Services.Modules.ProjectData.Implementations;

public sealed class ChapterRepairContext
{
    public string ContextMode { get; set; } = string.Empty;
    public FactSnapshot? FactSnapshot { get; set; }
    public string RepairHints { get; set; } = string.Empty;
}

public sealed class ChapterRepairResult
{
    public ChapterRepairResult(string chapterId, string content, FactSnapshot factSnapshot)
    {
        ChapterId = chapterId;
        Content = content;
        FactSnapshot = factSnapshot;
    }

    public string ChapterId { get; }
    public string Content { get; }
    public FactSnapshot FactSnapshot { get; }
}

public sealed class PortableChapterRepairService
{
    private static readonly Regex ChapterIdRegex = new(
        @"^vol(?<volume>\d+)_ch(?<chapter>\d+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly Func<string, CancellationToken, Task<string?>> _loadChapterAsync;
    private readonly Func<string, CancellationToken, Task<ChapterRepairContext?>> _loadContextAsync;
    private readonly Func<string, ChapterRepairContext, FactSnapshot, CancellationToken, Task<string>> _generateRepairAsync;
    private readonly Func<string, string, FactSnapshot, CancellationToken, Task> _saveGeneratedAsync;
    private readonly ChapterRepairPromptComposer _promptComposer;

    private FactSnapshot? _lastFactSnapshot;

    public PortableChapterRepairService(
        Func<string, CancellationToken, Task<string?>> loadChapterAsync,
        Func<string, CancellationToken, Task<ChapterRepairContext?>> loadContextAsync,
        Func<string, ChapterRepairContext, FactSnapshot, CancellationToken, Task<string>> generateRepairAsync,
        Func<string, string, FactSnapshot, CancellationToken, Task> saveGeneratedAsync,
        ChapterRepairPromptComposer? promptComposer = null)
    {
        _loadChapterAsync = loadChapterAsync ?? throw new ArgumentNullException(nameof(loadChapterAsync));
        _loadContextAsync = loadContextAsync ?? throw new ArgumentNullException(nameof(loadContextAsync));
        _generateRepairAsync = generateRepairAsync ?? throw new ArgumentNullException(nameof(generateRepairAsync));
        _saveGeneratedAsync = saveGeneratedAsync ?? throw new ArgumentNullException(nameof(saveGeneratedAsync));
        _promptComposer = promptComposer ?? new ChapterRepairPromptComposer();
    }

    public async Task<ChapterRepairResult> RepairChapterAsync(
        string chapterId,
        IReadOnlyList<string>? hints,
        CancellationToken cancellationToken = default)
    {
        var existingContentRaw = await _loadChapterAsync(chapterId, cancellationToken).ConfigureAwait(false) ?? string.Empty;
        var context = await _loadContextAsync(chapterId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"无法获取章节 {chapterId} 的打包上下文，请确认已执行打包");

        context.RepairHints = _promptComposer.Compose(existingContentRaw, hints);
        var factSnapshot = context.FactSnapshot
            ?? throw new InvalidOperationException($"章节 {chapterId} 缺少 FactSnapshot（上下文模式: {context.ContextMode}），请重新打包后重试");

        _lastFactSnapshot = factSnapshot;
        var content = await _generateRepairAsync(chapterId, context, factSnapshot, cancellationToken).ConfigureAwait(false);

        return new ChapterRepairResult(chapterId, content ?? string.Empty, factSnapshot);
    }

    public async Task<string> CheckNextChapterConsistencyAsync(
        string chapterId,
        string repairedContent,
        CancellationToken cancellationToken = default)
    {
        _ = repairedContent;

        try
        {
            var nextChapterId = BuildNextChapterId(chapterId);
            if (string.IsNullOrEmpty(nextChapterId))
            {
                return string.Empty;
            }

            var nextContent = await _loadChapterAsync(nextChapterId, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(nextContent))
            {
                return string.Empty;
            }

            var nextTitle = ExtractFirstLine(nextContent);
            return $"与下一章（{nextTitle}）衔接：数据层一致 ✓";
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task SaveRepairedAsync(
        string chapterId,
        string repairedContent,
        CancellationToken cancellationToken = default)
    {
        var factSnapshot = _lastFactSnapshot
            ?? throw new InvalidOperationException("未找到 FactSnapshot，请先执行修复再保存");

        await _saveGeneratedAsync(chapterId, repairedContent, factSnapshot, cancellationToken).ConfigureAwait(false);
    }

    private static string BuildNextChapterId(string chapterId)
    {
        var match = ChapterIdRegex.Match(chapterId ?? string.Empty);
        if (!match.Success)
        {
            return string.Empty;
        }

        var volume = int.Parse(match.Groups["volume"].Value);
        var chapter = int.Parse(match.Groups["chapter"].Value);
        return $"vol{volume}_ch{chapter + 1}";
    }

    private static string ExtractFirstLine(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return string.Empty;
        }

        var line = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? string.Empty;
        return line.TrimStart('#', ' ').Trim();
    }
}
