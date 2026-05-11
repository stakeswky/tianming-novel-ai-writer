using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;

namespace TM.Services.Modules.ProjectData.Implementations;

public sealed class ChapterRepairProblem
{
    public string ModuleName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? Suggestion { get; set; }
    public string? ChapterId { get; set; }
    public string? ChapterTitle { get; set; }
    public bool HasChapterLocation => !string.IsNullOrWhiteSpace(ChapterId);
}

public sealed class ChapterRepairProblemHintBuilder
{
    private static readonly HashSet<string> ActionableResults = new(StringComparer.Ordinal)
    {
        "警告",
        "失败",
        "未校验"
    };

    public IReadOnlyList<ChapterRepairProblem> FlattenProblems(ValidationSummaryData? summary)
    {
        if (summary == null)
        {
            return Array.Empty<ChapterRepairProblem>();
        }

        var problems = new List<ChapterRepairProblem>();
        foreach (var module in summary.ModuleResults.Where(m => ActionableResults.Contains(m.Result)))
        {
            foreach (var item in ReadProblemItems(module.ProblemItemsJson))
            {
                problems.Add(new ChapterRepairProblem
                {
                    ModuleName = module.DisplayName,
                    Summary = item.Summary,
                    Reason = item.Reason,
                    Details = item.Details,
                    Suggestion = item.Suggestion,
                    ChapterId = item.ChapterId,
                    ChapterTitle = item.ChapterTitle
                });
            }
        }

        return problems;
    }

    public IReadOnlyList<string> BuildHintsForChapter(IEnumerable<ChapterRepairProblem>? problems, string chapterId)
    {
        if (problems == null || string.IsNullOrWhiteSpace(chapterId))
        {
            return Array.Empty<string>();
        }

        return problems
            .Where(p => string.Equals(p.ChapterId, chapterId, StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Summary?.Trim() ?? string.Empty)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static IReadOnlyList<ProblemItem> ReadProblemItems(string problemItemsJson)
    {
        if (string.IsNullOrWhiteSpace(problemItemsJson))
        {
            return Array.Empty<ProblemItem>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<ProblemItem>>(problemItemsJson) ?? [];
        }
        catch (JsonException)
        {
            return Array.Empty<ProblemItem>();
        }
    }
}
