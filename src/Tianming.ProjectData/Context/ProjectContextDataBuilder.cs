using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TM.Services.Modules.ProjectData.Models.Design.Characters;
using TM.Services.Modules.ProjectData.Models.Design.Factions;
using TM.Services.Modules.ProjectData.Models.Design.Location;
using TM.Services.Modules.ProjectData.Models.Design.Plot;
using TM.Services.Modules.ProjectData.Models.Design.Worldview;
using TM.Services.Modules.ProjectData.Models.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Models.Tracking;
using TM.Services.Modules.ProjectData.Modules.Design.CharacterRules;
using TM.Services.Modules.ProjectData.Modules.Design.FactionRules;
using TM.Services.Modules.ProjectData.Modules.Design.LocationRules;
using TM.Services.Modules.ProjectData.Modules.Design.PlotRules;
using TM.Services.Modules.ProjectData.Modules.Design.WorldRules;
using TM.Services.Modules.ProjectData.Modules.Generate.ChapterPlanning;
using TM.Services.Modules.ProjectData.Modules.Schema;
using TM.Services.Modules.ProjectData.Implementations;

namespace TM.Services.Modules.ProjectData.Context;

public sealed class ProjectContextDataBuilder
{
    private readonly string _projectRoot;
    private readonly ModuleDataAdapter<ChapterCategory, ChapterData> _chapterAdapter;
    private readonly ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData> _characterAdapter;
    private readonly ModuleDataAdapter<FactionRulesCategory, FactionRulesData> _factionAdapter;
    private readonly ModuleDataAdapter<LocationRulesCategory, LocationRulesData> _locationAdapter;
    private readonly ModuleDataAdapter<PlotRulesCategory, PlotRulesData>? _plotAdapter;
    private readonly ModuleDataAdapter<WorldRulesCategory, WorldRulesData>? _worldRuleAdapter;

    public ProjectContextDataBuilder(
        string projectRoot,
        ModuleDataAdapter<ChapterCategory, ChapterData> chapterAdapter,
        ModuleDataAdapter<CharacterRulesCategory, CharacterRulesData> characterAdapter,
        ModuleDataAdapter<FactionRulesCategory, FactionRulesData> factionAdapter,
        ModuleDataAdapter<LocationRulesCategory, LocationRulesData> locationAdapter,
        ModuleDataAdapter<PlotRulesCategory, PlotRulesData>? plotAdapter = null,
        ModuleDataAdapter<WorldRulesCategory, WorldRulesData>? worldRuleAdapter = null)
    {
        _projectRoot = projectRoot;
        _chapterAdapter = chapterAdapter;
        _characterAdapter = characterAdapter;
        _factionAdapter = factionAdapter;
        _locationAdapter = locationAdapter;
        _plotAdapter = plotAdapter;
        _worldRuleAdapter = worldRuleAdapter;
    }

    public async Task<FactSnapshot> BuildFactSnapshotAsync(string chapterId, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        var chapter = _chapterAdapter.GetData().FirstOrDefault(x => string.Equals(x.Id, chapterId, StringComparison.Ordinal));
        var characterIds = MapIdsByName(_characterAdapter.GetData(), chapter?.ReferencedCharacterNames, x => x.Id, x => x.Name);
        var factionIds = MapIdsByName(_factionAdapter.GetData(), chapter?.ReferencedFactionNames, x => x.Id, x => x.Name);
        var locationIds = MapIdsByName(_locationAdapter.GetData(), chapter?.ReferencedLocationNames, x => x.Id, x => x.Name);

        var source = new FileFactSnapshotGuideSource(_projectRoot, _projectRoot);
        var extractor = new PortableFactSnapshotExtractor(source);
        var snapshot = await extractor
            .ExtractSnapshotAsync(
                chapterId,
                characterIds,
                locationIds,
                null,
                null,
                null,
                null,
                factionIds,
                ct)
            .ConfigureAwait(false);

        snapshot.CharacterDescriptions = BuildCharacterDescriptions(characterIds);
        snapshot.LocationDescriptions = BuildLocationDescriptions(locationIds);
        snapshot.WorldRuleConstraints = BuildWorldRuleConstraints();
        return snapshot;
    }

    public async Task<DesignElementNames> BuildDesignElementNamesAsync(CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        var characterNames = DistinctNames(_characterAdapter.GetData().Select(x => x.Name));
        return new DesignElementNames
        {
            CharacterNames = characterNames,
            FactionNames = DistinctNames(_factionAdapter.GetData().Select(x => x.Name)),
            LocationNames = DistinctNames(_locationAdapter.GetData().Select(x => x.Name)),
            PlotKeyNames = _plotAdapter == null
                ? []
                : DistinctNames(_plotAdapter.GetData().Select(x => x.Name)),
            PovCharacterNames = characterNames,
        };
    }

    public async Task<string> BuildPreviousChaptersSummaryAsync(string chapterId, CancellationToken ct = default)
    {
        await EnsureLoadedAsync(ct).ConfigureAwait(false);

        var chapters = _chapterAdapter.GetData()
            .OrderBy(x => x.ChapterNumber)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToList();
        var current = chapters.FirstOrDefault(x => string.Equals(x.Id, chapterId, StringComparison.Ordinal));
        if (current is null)
            return string.Empty;

        var previous = chapters
            .Where(x => x.ChapterNumber > 0 && x.ChapterNumber < current.ChapterNumber)
            .TakeLast(3)
            .Select(x => $"第{x.ChapterNumber}章 {x.ChapterTitle}: {x.GetCoreSummary()}")
            .ToList();

        return previous.Count == 0 ? string.Empty : string.Join("\n", previous);
    }

    private async Task EnsureLoadedAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await _chapterAdapter.LoadAsync().ConfigureAwait(false);
        await _characterAdapter.LoadAsync().ConfigureAwait(false);
        await _factionAdapter.LoadAsync().ConfigureAwait(false);
        await _locationAdapter.LoadAsync().ConfigureAwait(false);
        if (_plotAdapter != null)
            await _plotAdapter.LoadAsync().ConfigureAwait(false);
        if (_worldRuleAdapter != null)
            await _worldRuleAdapter.LoadAsync().ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();
    }

    private Dictionary<string, CharacterCoreDescription> BuildCharacterDescriptions(IReadOnlyCollection<string>? characterIds)
    {
        if (characterIds == null || characterIds.Count == 0)
            return new Dictionary<string, CharacterCoreDescription>(StringComparer.OrdinalIgnoreCase);

        return _characterAdapter.GetData()
            .Where(character => characterIds.Contains(character.Id, StringComparer.OrdinalIgnoreCase))
            .Where(character => !string.IsNullOrWhiteSpace(character.Id))
            .ToDictionary(
                character => character.Id,
                character => new CharacterCoreDescription
                {
                    Id = character.Id,
                    Name = character.Name,
                    HairColor = ExtractHairColor(character.Appearance),
                    Appearance = character.Appearance,
                    PersonalityTags = ParseTags(string.Join(",", character.FlawBelief, character.Identity, character.Want))
                },
                StringComparer.OrdinalIgnoreCase);
    }

    private Dictionary<string, LocationCoreDescription> BuildLocationDescriptions(IReadOnlyCollection<string>? locationIds)
    {
        if (locationIds == null || locationIds.Count == 0)
            return new Dictionary<string, LocationCoreDescription>(StringComparer.OrdinalIgnoreCase);

        return _locationAdapter.GetData()
            .Where(location => locationIds.Contains(location.Id, StringComparer.OrdinalIgnoreCase))
            .Where(location => !string.IsNullOrWhiteSpace(location.Id))
            .ToDictionary(
                location => location.Id,
                location => new LocationCoreDescription
                {
                    Id = location.Id,
                    Name = location.Name,
                    Description = location.Description,
                    Features = ParseTags(string.Join(",", location.Description, location.Terrain, string.Join(",", location.Landmarks)))
                },
                StringComparer.OrdinalIgnoreCase);
    }

    private List<WorldRuleConstraint> BuildWorldRuleConstraints()
    {
        if (_worldRuleAdapter == null)
            return [];

        return _worldRuleAdapter.GetData()
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Id) && !string.IsNullOrWhiteSpace(rule.HardRules))
            .Select(rule => new WorldRuleConstraint
            {
                RuleId = rule.Id,
                RuleName = rule.Name,
                Constraint = rule.HardRules,
                IsHardConstraint = true
            })
            .ToList();
    }

    private static List<string> DistinctNames(IEnumerable<string> names)
    {
        return names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyCollection<string>? MapIdsByName<T>(
        IEnumerable<T> items,
        IReadOnlyCollection<string>? names,
        Func<T, string> idSelector,
        Func<T, string> nameSelector)
    {
        if (names == null || names.Count == 0)
            return null;

        var wanted = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (wanted.Count == 0)
            return null;

        var ids = items
            .Where(item => wanted.Contains(nameSelector(item)))
            .Select(idSelector)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ids.Count == 0 ? null : ids;
    }

    private static string ExtractHairColor(string? appearance)
    {
        if (string.IsNullOrWhiteSpace(appearance))
            return string.Empty;

        var markers = new[] { "黑发", "白发", "银发", "金发", "蓝发", "红发", "褐发", "灰发", "青发", "紫发" };
        return markers.FirstOrDefault(appearance.Contains, string.Empty);
    }

    private static List<string> ParseTags(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([',', '，', '、', ';', '；'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }
}
