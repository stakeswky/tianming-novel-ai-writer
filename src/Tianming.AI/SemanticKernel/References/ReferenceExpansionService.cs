using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TM.Services.Framework.AI.SemanticKernel.References;

public sealed class ReferenceChapterContext
{
    public string ChapterId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

public sealed class ReferenceSnippet
{
    public string ChapterId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

public sealed class ReferenceExpansionService
{
    private const int DefaultSnippetLimit = 400;
    private const int DefaultTopK = 2;

    private readonly Func<string, CancellationToken, Task<ReferenceChapterContext?>> _loadChapterContextAsync;
    private readonly Func<string, int, CancellationToken, Task<IReadOnlyList<ReferenceSnippet>>>? _searchSnippetsAsync;

    public ReferenceExpansionService(
        Func<string, CancellationToken, Task<ReferenceChapterContext?>> loadChapterContextAsync,
        Func<string, int, CancellationToken, Task<IReadOnlyList<ReferenceSnippet>>>? searchSnippetsAsync = null)
    {
        _loadChapterContextAsync = loadChapterContextAsync ?? throw new ArgumentNullException(nameof(loadChapterContextAsync));
        _searchSnippetsAsync = searchSnippetsAsync;
    }

    public async Task<string> ExpandReferencesAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var references = ReferenceParser.Parse(text).OrderByDescending(r => r.StartIndex);
        foreach (var reference in references)
        {
            var replacement = await ResolveReferenceAsync(reference, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(replacement))
            {
                continue;
            }

            text = text.Remove(reference.StartIndex, reference.Length)
                .Insert(reference.StartIndex, replacement);
        }

        return text;
    }

    private async Task<string> ResolveReferenceAsync(Reference reference, CancellationToken cancellationToken)
    {
        return reference.Type switch
        {
            "chapter" or "rewrite" => await ResolveChapterAsync(reference.Name, cancellationToken).ConfigureAwait(false),
            _ => reference.FullMatch
        };
    }

    private async Task<string> ResolveChapterAsync(string? chapterId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(chapterId))
        {
            return "[请指定章节ID]";
        }

        var context = await _loadChapterContextAsync(chapterId, cancellationToken).ConfigureAwait(false);
        if (context == null)
        {
            return $"[未找到章节: {chapterId}]";
        }

        var builder = new StringBuilder();
        var safeTitle = WebUtility.HtmlEncode(context.Title ?? string.Empty);
        builder.AppendLine($"<context_block type=\"chapter_reference\" title=\"{safeTitle}\">{context.Summary}</context_block>");

        if (_searchSnippetsAsync != null)
        {
            var snippets = await _searchSnippetsAsync(chapterId, DefaultTopK, cancellationToken).ConfigureAwait(false);
            var usable = snippets
                .Where(s => !string.IsNullOrWhiteSpace(s.Content))
                .Take(DefaultTopK)
                .ToList();

            if (usable.Count > 0)
            {
                builder.AppendLine("<context_block type=\"key_excerpts\">");
                foreach (var snippet in usable)
                {
                    builder.AppendLine(TruncateSnippet(snippet.Content));
                }
                builder.AppendLine("</context_block>");
            }
        }

        return builder.ToString().Trim();
    }

    private static string TruncateSnippet(string content) =>
        content.Length > DefaultSnippetLimit
            ? content[..DefaultSnippetLimit] + "…"
            : content;
}
