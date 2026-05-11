using System;
using System.Collections.Generic;
using System.Linq;
using TM.Services.Modules.ProjectData.Models.Guides;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class ChapterValidationDesignContextSource
    {
        public ContextIdCollection? ContextIds { get; init; }
        public List<PromptTemplateContext> Templates { get; init; } = new();
        public List<PromptWorldRuleContext> WorldRules { get; init; } = new();
        public List<PromptCharacterContext> Characters { get; init; } = new();
        public List<PromptFactionContext> Factions { get; init; } = new();
        public List<PromptLocationContext> Locations { get; init; } = new();
        public List<PromptPlotContext> PlotRules { get; init; } = new();
    }

    public sealed class ChapterValidationDesignContextSections
    {
        public List<string> TemplateItems { get; init; } = new();
        public List<string> WorldRuleItems { get; init; } = new();
        public List<string> CharacterItems { get; init; } = new();
        public List<string> FactionItems { get; init; } = new();
        public List<string> LocationItems { get; init; } = new();
        public List<string> PlotItems { get; init; } = new();
    }

    public sealed class PromptTemplateContext
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Genre { get; init; } = string.Empty;
        public string OverallIdea { get; init; } = string.Empty;
        public string WorldBuildingMethod { get; init; } = string.Empty;
        public string ProtagonistDesign { get; init; } = string.Empty;
    }

    public sealed class PromptWorldRuleContext
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string HardRules { get; init; } = string.Empty;
        public string PowerSystem { get; init; } = string.Empty;
    }

    public sealed class PromptCharacterContext
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string Identity { get; init; } = string.Empty;
        public string Race { get; init; } = string.Empty;
        public string FlawBelief { get; init; } = string.Empty;
        public string Want { get; init; } = string.Empty;
        public string GrowthPath { get; init; } = string.Empty;
    }

    public sealed class PromptFactionContext
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string FactionType { get; init; } = string.Empty;
        public string Goal { get; init; } = string.Empty;
        public string Leader { get; init; } = string.Empty;
    }

    public sealed class PromptLocationContext
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string LocationType { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Terrain { get; init; } = string.Empty;
    }

    public sealed class PromptPlotContext
    {
        public string Id { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string StoryPhase { get; init; } = string.Empty;
        public string Goal { get; init; } = string.Empty;
        public string Conflict { get; init; } = string.Empty;
        public string Result { get; init; } = string.Empty;
    }

    public static class ChapterValidationDesignContextResolver
    {
        public static ChapterValidationDesignContextSections Resolve(ChapterValidationDesignContextSource source)
        {
            ArgumentNullException.ThrowIfNull(source);

            var characters = ResolveCharacters(source);
            return new ChapterValidationDesignContextSections
            {
                TemplateItems = ResolveTemplates(source),
                WorldRuleItems = ResolveWorldRules(source),
                CharacterItems = characters.Items,
                FactionItems = ResolveFactions(source, characters.IdToName),
                LocationItems = ResolveLocations(source),
                PlotItems = ResolvePlotRules(source)
            };
        }

        private static List<string> ResolveTemplates(ChapterValidationDesignContextSource source)
        {
            var ids = source.ContextIds?.TemplateIds;
            if (ids == null || ids.Count == 0)
                return new List<string>();

            var idSet = new HashSet<string>(ids);
            return source.Templates
                .Where(item => idSet.Contains(item.Id))
                .Take(3)
                .Select(item => $"{item.Name}: 类型={item.Genre}, 构思={TruncateString(item.OverallIdea, 60)}, 世界观构建={TruncateString(item.WorldBuildingMethod, 40)}, 主角塑造={TruncateString(item.ProtagonistDesign, 40)}")
                .ToList();
        }

        private static List<string> ResolveWorldRules(ChapterValidationDesignContextSource source)
        {
            var ids = source.ContextIds?.WorldRuleIds;
            if (ids == null || ids.Count == 0)
                return new List<string>();

            var idSet = new HashSet<string>(ids);
            return source.WorldRules
                .Where(item => idSet.Contains(item.Id))
                .Take(5)
                .Select(item => $"{item.Name}: 硬规则={TruncateString(item.HardRules, 60)}, 力量体系={TruncateString(item.PowerSystem, 40)}")
                .ToList();
        }

        private static (List<string> Items, Dictionary<string, string> IdToName) ResolveCharacters(ChapterValidationDesignContextSource source)
        {
            var ids = source.ContextIds?.Characters;
            if (ids == null || ids.Count == 0)
                return (new List<string>(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

            var idSet = new HashSet<string>(ids);
            var selected = source.Characters
                .Where(item => idSet.Contains(item.Id))
                .ToList();
            var idToName = selected
                .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Name))
                .ToDictionary(item => item.Id, item => item.Name, StringComparer.OrdinalIgnoreCase);
            var items = selected
                .Take(10)
                .Select(item => $"{item.Name}: 身份={item.Identity}, 种族={item.Race}, 核心缺陷={TruncateString(item.FlawBelief, 30)}, 外在目标={TruncateString(item.Want, 30)}, 成长路径={TruncateString(item.GrowthPath, 30)}")
                .ToList();

            return (items, idToName);
        }

        private static List<string> ResolveFactions(ChapterValidationDesignContextSource source, Dictionary<string, string> characterIdToName)
        {
            var ids = source.ContextIds?.Factions;
            if (ids == null || ids.Count == 0)
                return new List<string>();

            var idSet = new HashSet<string>(ids);
            return source.Factions
                .Where(item => idSet.Contains(item.Id))
                .Take(8)
                .Select(item =>
                {
                    var leader = string.IsNullOrWhiteSpace(item.Leader)
                        ? string.Empty
                        : characterIdToName.TryGetValue(item.Leader, out var name) ? name : item.Leader;
                    return $"{item.Name}: 类型={item.FactionType}, 目标={TruncateString(item.Goal, 40)}, 领袖={leader}";
                })
                .ToList();
        }

        private static List<string> ResolveLocations(ChapterValidationDesignContextSource source)
        {
            var ids = source.ContextIds?.Locations;
            if (ids == null || ids.Count == 0)
                return new List<string>();

            var idSet = new HashSet<string>(ids);
            return source.Locations
                .Where(item => idSet.Contains(item.Id))
                .Take(8)
                .Select(item => $"{item.Name}: 类型={item.LocationType}, 描述={TruncateString(item.Description, 40)}, 地形={TruncateString(item.Terrain, 30)}")
                .ToList();
        }

        private static List<string> ResolvePlotRules(ChapterValidationDesignContextSource source)
        {
            var ids = source.ContextIds?.PlotRules;
            if (ids == null || ids.Count == 0)
                return new List<string>();

            var idSet = new HashSet<string>(ids);
            return source.PlotRules
                .Where(item => idSet.Contains(item.Id))
                .Take(8)
                .Select(item => $"{item.Name}: 阶段={item.StoryPhase}, 目标={TruncateString(item.Goal, 40)}, 冲突={TruncateString(item.Conflict, 40)}, 结果={TruncateString(item.Result, 40)}")
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
