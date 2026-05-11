using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TM.Services.Framework.AI.SemanticKernel.References;

public sealed class Reference
{
    public string FullMatch { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Name { get; set; }
    public int StartIndex { get; set; }
    public int Length { get; set; }
}

public static class ReferenceParser
{
    private static readonly Regex ReferencePattern = new(
        @"@(续写|continue|重写|rewrite|仿写|imitate)(?:[:：]\s*|\s+)?([^\s@，,。！？!?；;]+)?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<Reference> Parse(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return Array.Empty<Reference>();
        }

        return ReferencePattern
            .Matches(text)
            .Cast<Match>()
            .Select(match => new Reference
            {
                FullMatch = match.Value,
                Type = NormalizeType(match.Groups[1].Value),
                Name = match.Groups[2].Success ? match.Groups[2].Value : null,
                StartIndex = match.Index,
                Length = match.Length
            })
            .ToList();
    }

    public static string ReplaceReferences(string text, Func<Reference, string?> resolve)
    {
        ArgumentNullException.ThrowIfNull(resolve);
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var references = Parse(text).OrderByDescending(r => r.StartIndex);
        foreach (var reference in references)
        {
            var replacement = resolve(reference);
            if (string.IsNullOrEmpty(replacement))
            {
                continue;
            }

            text = text.Remove(reference.StartIndex, reference.Length)
                .Insert(reference.StartIndex, replacement);
        }

        return text;
    }

    private static string NormalizeType(string type) =>
        type.ToLowerInvariant() switch
        {
            "续写" or "continue" => "chapter",
            "重写" or "rewrite" => "rewrite",
            "仿写" or "imitate" => "imitate",
            _ => type.ToLowerInvariant()
        };
}
