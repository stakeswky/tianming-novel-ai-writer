using System;
using System.Linq;
using TM.Services.Modules.ProjectData.Models.Contexts;
using TM.Services.Modules.ProjectData.Models.Guides;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public static class ValidationContextPromptSourceMapper
    {
        public static ChapterValidationPromptSources Map(
            ValidationContext? context,
            ContextIdCollection? contextIds)
        {
            var ids = contextIds ?? new ContextIdCollection();
            if (context == null)
            {
                return new ChapterValidationPromptSources
                {
                    DesignContextSource = new ChapterValidationDesignContextSource { ContextIds = ids },
                    PromptContextSource = new ChapterValidationPromptContextSource { ContextIds = ids }
                };
            }

            return new ChapterValidationPromptSources
            {
                DesignContextSource = new ChapterValidationDesignContextSource
                {
                    ContextIds = ids,
                    Templates = context.Design.Templates.CreativeMaterials.Select(item => new PromptTemplateContext
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Genre = item.Genre,
                        OverallIdea = item.OverallIdea,
                        WorldBuildingMethod = item.WorldBuildingMethod,
                        ProtagonistDesign = item.ProtagonistDesign
                    }).ToList(),
                    WorldRules = context.Design.Worldview.WorldRules.Select(item => new PromptWorldRuleContext
                    {
                        Id = item.Id,
                        Name = item.Name,
                        HardRules = item.HardRules,
                        PowerSystem = item.PowerSystem
                    }).ToList(),
                    Characters = context.Design.Characters.CharacterRules.Select(item => new PromptCharacterContext
                    {
                        Id = item.Id,
                        Name = item.Name,
                        Identity = item.Identity,
                        Race = item.Race,
                        FlawBelief = item.FlawBelief,
                        Want = item.Want,
                        GrowthPath = item.GrowthPath
                    }).ToList(),
                    Factions = context.Design.Factions.FactionRules.Select(item => new PromptFactionContext
                    {
                        Id = item.Id,
                        Name = item.Name,
                        FactionType = item.FactionType,
                        Goal = item.Goal,
                        Leader = item.Leader
                    }).ToList(),
                    Locations = context.Design.Locations.LocationRules.Select(item => new PromptLocationContext
                    {
                        Id = item.Id,
                        Name = item.Name,
                        LocationType = item.LocationType,
                        Description = item.Description,
                        Terrain = item.Terrain
                    }).ToList(),
                    PlotRules = context.Design.Plot.PlotRules.Select(item => new PromptPlotContext
                    {
                        Id = item.Id,
                        Name = item.Name,
                        StoryPhase = item.StoryPhase,
                        Goal = item.Goal,
                        Conflict = item.Conflict,
                        Result = item.Result
                    }).ToList()
                },
                PromptContextSource = new ChapterValidationPromptContextSource
                {
                    ContextIds = ids,
                    Outlines = context.Generate.Outline.Outlines.Select(item => new PromptOutlineContext
                    {
                        Id = item.Id,
                        OneLineOutline = item.OneLineOutline,
                        CoreConflict = item.CoreConflict,
                        Theme = item.Theme,
                        EndingState = item.EndingState
                    }).ToList(),
                    ChapterPlans = context.Generate.Planning.Chapters.Select(item => new PromptChapterPlanContext
                    {
                        Id = item.Id,
                        ChapterTitle = item.ChapterTitle,
                        ChapterTheme = item.ChapterTheme,
                        MainGoal = item.MainGoal,
                        KeyTurn = item.KeyTurn,
                        Hook = item.Hook,
                        Foreshadowing = item.Foreshadowing
                    }).ToList(),
                    Blueprints = context.Generate.Blueprint.Blueprints.Select(item => new PromptBlueprintContext
                    {
                        Id = item.Id,
                        OneLineStructure = item.OneLineStructure,
                        PacingCurve = item.PacingCurve,
                        Cast = item.Cast,
                        Locations = item.Locations
                    }).ToList(),
                    VolumeDesigns = context.Generate.VolumeDesign.VolumeDesigns.Select(item => new PromptVolumeDesignContext
                    {
                        Id = item.Id,
                        VolumeTitle = item.VolumeTitle,
                        VolumeTheme = item.VolumeTheme,
                        StageGoal = item.StageGoal,
                        MainConflict = item.MainConflict,
                        KeyEvents = item.KeyEvents
                    }).ToList()
                }
            };
        }
    }
}
