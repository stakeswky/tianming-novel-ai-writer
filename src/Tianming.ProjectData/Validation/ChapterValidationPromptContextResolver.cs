using System;
using System.Collections.Generic;
using System.Linq;
using TM.Services.Modules.ProjectData.Models.Guides;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class ChapterValidationPromptContextSource
    {
        public ContextIdCollection? ContextIds { get; init; }
        public List<PromptOutlineContext> Outlines { get; init; } = new();
        public List<PromptChapterPlanContext> ChapterPlans { get; init; } = new();
        public List<PromptBlueprintContext> Blueprints { get; init; } = new();
        public List<PromptVolumeDesignContext> VolumeDesigns { get; init; } = new();
    }

    public sealed class ChapterValidationPromptContextSections
    {
        public List<string> OutlineItems { get; init; } = new();
        public List<string> ChapterPlanItems { get; init; } = new();
        public List<string> BlueprintItems { get; init; } = new();
        public List<string> VolumeDesignItems { get; init; } = new();
    }

    public sealed class PromptOutlineContext
    {
        public string Id { get; init; } = string.Empty;
        public string OneLineOutline { get; init; } = string.Empty;
        public string CoreConflict { get; init; } = string.Empty;
        public string Theme { get; init; } = string.Empty;
        public string EndingState { get; init; } = string.Empty;
    }

    public sealed class PromptChapterPlanContext
    {
        public string Id { get; init; } = string.Empty;
        public string ChapterTitle { get; init; } = string.Empty;
        public string ChapterTheme { get; init; } = string.Empty;
        public string MainGoal { get; init; } = string.Empty;
        public string KeyTurn { get; init; } = string.Empty;
        public string Hook { get; init; } = string.Empty;
        public string Foreshadowing { get; init; } = string.Empty;
    }

    public sealed class PromptBlueprintContext
    {
        public string Id { get; init; } = string.Empty;
        public string OneLineStructure { get; init; } = string.Empty;
        public string PacingCurve { get; init; } = string.Empty;
        public string Cast { get; init; } = string.Empty;
        public string Locations { get; init; } = string.Empty;
    }

    public sealed class PromptVolumeDesignContext
    {
        public string Id { get; init; } = string.Empty;
        public string VolumeTitle { get; init; } = string.Empty;
        public string VolumeTheme { get; init; } = string.Empty;
        public string StageGoal { get; init; } = string.Empty;
        public string MainConflict { get; init; } = string.Empty;
        public string KeyEvents { get; init; } = string.Empty;
    }

    public static class ChapterValidationPromptContextResolver
    {
        public static ChapterValidationPromptContextSections Resolve(ChapterValidationPromptContextSource source)
        {
            ArgumentNullException.ThrowIfNull(source);

            return new ChapterValidationPromptContextSections
            {
                OutlineItems = ResolveOutlineItems(source),
                ChapterPlanItems = ResolveChapterPlanItems(source),
                BlueprintItems = ResolveBlueprintItems(source),
                VolumeDesignItems = ResolveVolumeDesignItems(source)
            };
        }

        private static List<string> ResolveOutlineItems(ChapterValidationPromptContextSource source)
        {
            var outlineId = source.ContextIds?.VolumeOutline;
            if (string.IsNullOrWhiteSpace(outlineId))
                return new List<string>();

            var outline = source.Outlines.FirstOrDefault(item => item.Id == outlineId);
            if (outline == null)
                return new List<string>();

            return
            [
                $"一句话大纲={TruncateString(outline.OneLineOutline, 80)}",
                $"核心冲突={TruncateString(outline.CoreConflict, 60)}",
                $"主题={TruncateString(outline.Theme, 60)}",
                $"结局状态={TruncateString(outline.EndingState, 60)}"
            ];
        }

        private static List<string> ResolveChapterPlanItems(ChapterValidationPromptContextSource source)
        {
            var chapterPlanId = source.ContextIds?.ChapterPlanId;
            if (string.IsNullOrWhiteSpace(chapterPlanId))
                return new List<string>();

            var chapterPlan = source.ChapterPlans.FirstOrDefault(item => string.Equals(item.Id, chapterPlanId, StringComparison.Ordinal));
            if (chapterPlan == null)
                return new List<string>();

            return
            [
                $"标题={chapterPlan.ChapterTitle}",
                $"主题={TruncateString(chapterPlan.ChapterTheme, 60)}",
                $"主目标={TruncateString(chapterPlan.MainGoal, 60)}",
                $"关键转折={TruncateString(chapterPlan.KeyTurn, 60)}",
                $"结尾钩子={TruncateString(chapterPlan.Hook, 60)}",
                $"伏笔={TruncateString(chapterPlan.Foreshadowing, 60)}"
            ];
        }

        private static List<string> ResolveBlueprintItems(ChapterValidationPromptContextSource source)
        {
            var blueprintIds = source.ContextIds?.BlueprintIds;
            if (blueprintIds == null || blueprintIds.Count == 0)
                return new List<string>();

            var blueprintIdSet = new HashSet<string>(blueprintIds);
            return source.Blueprints
                .Where(item => blueprintIdSet.Contains(item.Id))
                .Take(5)
                .Select(item => $"结构={TruncateString(item.OneLineStructure, 60)}, 节奏={TruncateString(item.PacingCurve, 40)}, 角色={TruncateString(item.Cast, 40)}, 地点={TruncateString(item.Locations, 40)}")
                .ToList();
        }

        private static List<string> ResolveVolumeDesignItems(ChapterValidationPromptContextSource source)
        {
            var volumeDesignId = source.ContextIds?.VolumeDesignId;
            if (string.IsNullOrWhiteSpace(volumeDesignId))
                return new List<string>();

            var volumeDesign = source.VolumeDesigns.FirstOrDefault(item => string.Equals(item.Id, volumeDesignId, StringComparison.Ordinal));
            if (volumeDesign == null)
                return new List<string>();

            return
            [
                $"卷标题={volumeDesign.VolumeTitle}",
                $"卷主题={TruncateString(volumeDesign.VolumeTheme, 60)}",
                $"阶段目标={TruncateString(volumeDesign.StageGoal, 60)}",
                $"主冲突={TruncateString(volumeDesign.MainConflict, 60)}",
                $"关键事件={TruncateString(volumeDesign.KeyEvents, 60)}"
            ];
        }

        private static string TruncateString(string? text, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;
            return text.Length <= maxLength ? text : text[..maxLength] + "...";
        }
    }
}
