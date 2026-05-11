using System.Text.Json;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ChapterRepairProblemHintBuilderTests
{
    [Fact]
    public void FlattenProblems_filters_actionable_modules_and_preserves_problem_fields()
    {
        var summary = new ValidationSummaryData
        {
            ModuleResults =
            {
                new ModuleValidationResult
                {
                    DisplayName = "结构一致性",
                    Result = "警告",
                    ProblemItemsJson = JsonSerializer.Serialize(new[]
                    {
                        new ProblemItem
                        {
                            Summary = "角色动机不足",
                            Reason = "缺少铺垫",
                            Details = "第二幕转折突兀",
                            Suggestion = "补一段犹豫",
                            ChapterId = "vol1_ch3",
                            ChapterTitle = "第三章"
                        }
                    })
                },
                new ModuleValidationResult
                {
                    DisplayName = "文风一致性",
                    Result = "通过",
                    ProblemItemsJson = JsonSerializer.Serialize(new[]
                    {
                        new ProblemItem { Summary = "通过项不应进入修复" }
                    })
                }
            }
        };

        var problem = Assert.Single(new ChapterRepairProblemHintBuilder().FlattenProblems(summary));

        Assert.Equal("结构一致性", problem.ModuleName);
        Assert.Equal("角色动机不足", problem.Summary);
        Assert.Equal("缺少铺垫", problem.Reason);
        Assert.Equal("第二幕转折突兀", problem.Details);
        Assert.Equal("补一段犹豫", problem.Suggestion);
        Assert.Equal("vol1_ch3", problem.ChapterId);
        Assert.Equal("第三章", problem.ChapterTitle);
        Assert.True(problem.HasChapterLocation);
    }

    [Fact]
    public void BuildHintsForChapter_uses_trimmed_unique_summaries_for_selected_chapter()
    {
        var problems = new[]
        {
            new ChapterRepairProblem { ChapterId = "vol1_ch4", Summary = "  修复伏笔回收  " },
            new ChapterRepairProblem { ChapterId = "vol1_ch4", Summary = "修复伏笔回收" },
            new ChapterRepairProblem { ChapterId = "vol1_ch4", Summary = "" },
            new ChapterRepairProblem { ChapterId = "vol1_ch5", Summary = "其他章节问题" }
        };

        var hints = new ChapterRepairProblemHintBuilder().BuildHintsForChapter(problems, "vol1_ch4");

        Assert.Equal(["修复伏笔回收"], hints);
    }

    [Fact]
    public void FlattenProblems_ignores_invalid_problem_json()
    {
        var summary = new ValidationSummaryData
        {
            ModuleResults =
            {
                new ModuleValidationResult
                {
                    DisplayName = "坏数据模块",
                    Result = "失败",
                    ProblemItemsJson = "{bad json}"
                },
                new ModuleValidationResult
                {
                    DisplayName = "可用模块",
                    Result = "失败",
                    ProblemItemsJson = JsonSerializer.Serialize(new[]
                    {
                        new ProblemItem { Summary = "可用问题", ChapterId = "vol1_ch1" }
                    })
                }
            }
        };

        var problem = Assert.Single(new ChapterRepairProblemHintBuilder().FlattenProblems(summary));

        Assert.Equal("可用模块", problem.ModuleName);
        Assert.Equal("可用问题", problem.Summary);
    }
}
