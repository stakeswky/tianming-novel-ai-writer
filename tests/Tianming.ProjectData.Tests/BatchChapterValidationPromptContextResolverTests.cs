using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Guides;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class BatchChapterValidationPromptContextResolverTests
{
    [Fact]
    public void Resolve_formats_batch_inline_context_like_original_batch_prompt()
    {
        var context = BatchChapterValidationPromptContextResolver.Resolve(new BatchChapterValidationPromptContextSource
        {
            ContextIds = new ContextIdCollection
            {
                Characters = ["char-1", "char-2"],
                Factions = ["faction-1"],
                PlotRules = ["plot-1"]
            },
            Characters =
            [
                new PromptCharacterContext { Id = "char-1", Name = "沈天命", Identity = "试炼弟子" },
                new PromptCharacterContext { Id = "char-2", Name = "林青", Identity = "师姐" }
            ],
            Factions =
            [
                new PromptFactionContext { Id = "faction-1", Name = "青岚宗" }
            ],
            PlotRules =
            [
                new PromptPlotContext { Id = "plot-1", Name = "命火试炼", Goal = "点燃命火并取得内门资格" }
            ]
        });

        Assert.Equal(["沈天命(试炼弟子)", "林青(师姐)"], context.Characters);
        Assert.Equal(["青岚宗"], context.Factions);
        Assert.Equal(["命火试炼:点燃命火并取得内门资格"], context.PlotRules);
    }

    [Fact]
    public void Resolve_applies_original_batch_limits_and_goal_truncation()
    {
        var context = BatchChapterValidationPromptContextResolver.Resolve(new BatchChapterValidationPromptContextSource
        {
            ContextIds = new ContextIdCollection
            {
                Characters = Enumerable.Range(1, 6).Select(i => $"char-{i}").ToList(),
                Factions = Enumerable.Range(1, 6).Select(i => $"faction-{i}").ToList(),
                PlotRules = Enumerable.Range(1, 4).Select(i => $"plot-{i}").ToList()
            },
            Characters = Enumerable.Range(1, 6)
                .Select(i => new PromptCharacterContext { Id = $"char-{i}", Name = $"角色{i}", Identity = "身份" })
                .ToList(),
            Factions = Enumerable.Range(1, 6)
                .Select(i => new PromptFactionContext { Id = $"faction-{i}", Name = $"势力{i}" })
                .ToList(),
            PlotRules = Enumerable.Range(1, 4)
                .Select(i => new PromptPlotContext { Id = $"plot-{i}", Name = $"剧情{i}", Goal = new string((char)('A' + i), 31) })
                .ToList()
        });

        Assert.Equal(5, context.Characters.Count);
        Assert.DoesNotContain(context.Characters, value => value.StartsWith("角色6", StringComparison.Ordinal));
        Assert.Equal(5, context.Factions.Count);
        Assert.DoesNotContain("势力6", context.Factions);
        Assert.Equal(3, context.PlotRules.Count);
        Assert.Equal("剧情1:" + new string('B', 30) + "...", context.PlotRules[0]);
        Assert.DoesNotContain(context.PlotRules, value => value.StartsWith("剧情4", StringComparison.Ordinal));
    }

    [Fact]
    public void Resolve_returns_empty_lists_without_context_ids()
    {
        var context = BatchChapterValidationPromptContextResolver.Resolve(new BatchChapterValidationPromptContextSource
        {
            ContextIds = null,
            Characters = [new PromptCharacterContext { Id = "char-1", Name = "不会出现" }]
        });

        Assert.Empty(context.Characters);
        Assert.Empty(context.Factions);
        Assert.Empty(context.PlotRules);
    }
}
