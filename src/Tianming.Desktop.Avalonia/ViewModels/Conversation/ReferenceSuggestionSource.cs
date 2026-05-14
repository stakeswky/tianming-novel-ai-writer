using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Tianming.Desktop.Avalonia.Infrastructure;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Design.CharacterRules;
using TM.Services.Modules.ProjectData.Modules.Design.WorldRules;
using TM.Services.Modules.ProjectData.Modules.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Schema;

namespace Tianming.Desktop.Avalonia.ViewModels.Conversation;

public sealed class ReferenceSuggestionSource : IReferenceSuggestionSource
{
    private readonly ModuleDataAdapter<ChapterCategory, ChapterData> _chapterAdapter;
    private readonly ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData> _characterAdapter;
    private readonly ModuleDataAdapter<WorldRulesCategory, WorldRulesData> _worldAdapter;

    public ReferenceSuggestionSource(ICurrentProjectService currentProjectService)
    {
        ArgumentNullException.ThrowIfNull(currentProjectService);

        var projectRoot = currentProjectService.ProjectRoot;
        _chapterAdapter = new ModuleDataAdapter<ChapterCategory, ChapterData>(new ChapterPlanningSchema(), projectRoot);
        _characterAdapter = new ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData>(new CharacterRulesSchema(), projectRoot);
        _worldAdapter = new ModuleDataAdapter<WorldRulesCategory, WorldRulesData>(new WorldRulesSchema(), projectRoot);
    }

    public async Task<IReadOnlyList<ReferenceItemVm>> SuggestAsync(string query, CancellationToken ct = default)
    {
        var trimmed = query?.Trim();
        if (string.IsNullOrEmpty(trimmed))
            return [];

        ct.ThrowIfCancellationRequested();
        await _chapterAdapter.LoadAsync().ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        await _characterAdapter.LoadAsync().ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
        await _worldAdapter.LoadAsync().ConfigureAwait(false);

        return EnumerateCandidates()
            .Where(item => item.Name.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .ToList();
    }

    private IEnumerable<ReferenceItemVm> EnumerateCandidates()
    {
        foreach (var chapter in _chapterAdapter.GetData())
        {
            var name = GetChapterDisplayName(chapter);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            yield return new ReferenceItemVm
            {
                Id = chapter.Id,
                Name = name,
                Category = "Chapter",
            };
        }

        foreach (var character in _characterAdapter.GetData().Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            yield return new ReferenceItemVm
            {
                Id = character.Id,
                Name = character.Name,
                Category = "Character",
            };
        }

        foreach (var world in _worldAdapter.GetData().Where(item => !string.IsNullOrWhiteSpace(item.Name)))
        {
            yield return new ReferenceItemVm
            {
                Id = world.Id,
                Name = world.Name,
                Category = "World",
            };
        }
    }

    private static string GetChapterDisplayName(ChapterData chapter)
    {
        if (!string.IsNullOrWhiteSpace(chapter.Name))
            return chapter.Name;

        if (chapter.ChapterNumber > 0 && !string.IsNullOrWhiteSpace(chapter.ChapterTitle))
            return $"第 {chapter.ChapterNumber} 章 {chapter.ChapterTitle}";

        return chapter.ChapterTitle;
    }
}
