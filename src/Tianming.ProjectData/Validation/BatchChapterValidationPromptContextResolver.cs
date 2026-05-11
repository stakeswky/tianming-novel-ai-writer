using System;
using System.Collections.Generic;
using System.Linq;
using TM.Services.Modules.ProjectData.Models.Guides;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class BatchChapterValidationPromptContextSource
    {
        public ContextIdCollection? ContextIds { get; init; }
        public List<PromptCharacterContext> Characters { get; init; } = new();
        public List<PromptFactionContext> Factions { get; init; } = new();
        public List<PromptPlotContext> PlotRules { get; init; } = new();
    }

    public sealed class BatchChapterValidationPromptContext
    {
        public List<string> Characters { get; init; } = new();
        public List<string> Factions { get; init; } = new();
        public List<string> PlotRules { get; init; } = new();
    }

    public static class BatchChapterValidationPromptContextResolver
    {
        public static BatchChapterValidationPromptContext Resolve(BatchChapterValidationPromptContextSource source)
        {
            ArgumentNullException.ThrowIfNull(source);

            return new BatchChapterValidationPromptContext
            {
                Characters = ResolveCharacters(source),
                Factions = ResolveFactions(source),
                PlotRules = ResolvePlotRules(source)
            };
        }

        private static List<string> ResolveCharacters(BatchChapterValidationPromptContextSource source)
        {
            var ids = source.ContextIds?.Characters;
            if (ids == null || ids.Count == 0)
                return new List<string>();

            var idSet = new HashSet<string>(ids);
            return source.Characters
                .Where(item => idSet.Contains(item.Id))
                .Take(5)
                .Select(item => $"{item.Name}({item.Identity})")
                .ToList();
        }

        private static List<string> ResolveFactions(BatchChapterValidationPromptContextSource source)
        {
            var ids = source.ContextIds?.Factions;
            if (ids == null || ids.Count == 0)
                return new List<string>();

            var idSet = new HashSet<string>(ids);
            return source.Factions
                .Where(item => idSet.Contains(item.Id))
                .Take(5)
                .Select(item => item.Name)
                .ToList();
        }

        private static List<string> ResolvePlotRules(BatchChapterValidationPromptContextSource source)
        {
            var ids = source.ContextIds?.PlotRules;
            if (ids == null || ids.Count == 0)
                return new List<string>();

            var idSet = new HashSet<string>(ids);
            return source.PlotRules
                .Where(item => idSet.Contains(item.Id))
                .Take(3)
                .Select(item => $"{item.Name}:{TruncateString(item.Goal, 30)}")
                .ToList();
        }

        private static string TruncateString(string? text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            return text.Length <= maxLength ? text : text[..maxLength] + "...";
        }
    }
}
