using System;
using System.Collections.Generic;
using TM.Services.Modules.ProjectData.Interfaces;

namespace TM.Services.Modules.ProjectData.Implementations
{
    public sealed class ChapterValidationPromptInputBuildRequest
    {
        public string ChapterId { get; init; } = string.Empty;
        public string ChapterTitle { get; init; } = string.Empty;
        public int VolumeNumber { get; init; }
        public int ChapterNumber { get; init; }
        public string VolumeName { get; init; } = string.Empty;
        public string ChapterContent { get; init; } = string.Empty;
        public ChapterValidationDesignContextSource? DesignContextSource { get; init; }
        public ChapterValidationPromptContextSource? PromptContextSource { get; init; }
        public IReadOnlyList<ValidationIssue>? KnownStructuralIssues { get; init; }
    }

    public sealed class ChapterValidationPromptInputBuilder
    {
        public ChapterValidationPromptInput Build(ChapterValidationPromptInputBuildRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            var designSections = request.DesignContextSource == null
                ? new ChapterValidationDesignContextSections()
                : ChapterValidationDesignContextResolver.Resolve(request.DesignContextSource);
            var promptSections = request.PromptContextSource == null
                ? new ChapterValidationPromptContextSections()
                : ChapterValidationPromptContextResolver.Resolve(request.PromptContextSource);

            return new ChapterValidationPromptInput
            {
                ChapterId = request.ChapterId,
                ChapterTitle = request.ChapterTitle,
                VolumeNumber = request.VolumeNumber,
                ChapterNumber = request.ChapterNumber,
                VolumeName = request.VolumeName,
                ChapterContent = request.ChapterContent,
                TemplateItems = designSections.TemplateItems,
                WorldRuleItems = designSections.WorldRuleItems,
                CharacterItems = designSections.CharacterItems,
                FactionItems = designSections.FactionItems,
                LocationItems = designSections.LocationItems,
                PlotItems = designSections.PlotItems,
                OutlineItems = promptSections.OutlineItems,
                ChapterPlanItems = promptSections.ChapterPlanItems,
                BlueprintItems = promptSections.BlueprintItems,
                VolumeDesignItems = promptSections.VolumeDesignItems,
                KnownStructuralIssues = request.KnownStructuralIssues == null
                    ? new List<ValidationIssue>()
                    : new List<ValidationIssue>(request.KnownStructuralIssues)
            };
        }

        public string BuildPrompt(ChapterValidationPromptInputBuildRequest request)
        {
            return ChapterValidationPromptComposer.Build(Build(request));
        }
    }
}
