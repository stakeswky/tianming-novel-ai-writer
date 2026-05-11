using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Contexts;
using TM.Services.Modules.ProjectData.Models.Design.Templates;
using TM.Services.Modules.ProjectData.Models.Generate.StrategicOutline;
using TM.Services.Modules.ProjectData.Models.Guides;
using UnifiedChapterValidationResult = TM.Services.Modules.ProjectData.Interfaces.ChapterValidationResult;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ValidationContextAIWorkflowTests
{
    [Fact]
    public async Task ExecuteAsync_loads_context_and_context_ids_then_generates_prompt_and_parses_ai_response()
    {
        string? capturedPrompt = null;
        string? loadedContextChapterId = null;
        string? loadedIdsChapterId = null;
        var workflow = new ValidationContextAIWorkflow(
            (chapterId, _) =>
            {
                loadedContextChapterId = chapterId;
                return Task.FromResult<ValidationContext?>(new ValidationContext
                {
                    Design =
                    {
                        Templates =
                        {
                            CreativeMaterials =
                            [
                                new CreativeMaterialData
                                {
                                    Id = "tpl-1",
                                    Name = "东方玄幻模板",
                                    Genre = "玄幻"
                                }
                            ]
                        }
                    },
                    Generate =
                    {
                        Outline =
                        {
                            Outlines =
                            [
                                new OutlineData
                                {
                                    Id = "outline-1",
                                    OneLineOutline = "少年点燃命火"
                                }
                            ]
                        }
                    }
                });
            },
            (chapterId, _) =>
            {
                loadedIdsChapterId = chapterId;
                return Task.FromResult<ContextIdCollection?>(new ContextIdCollection
                {
                    TemplateIds = ["tpl-1"],
                    VolumeOutline = "outline-1"
                });
            },
            (prompt, _) =>
            {
                capturedPrompt = prompt;
                return Task.FromResult(ChapterAIValidationResponse.Success(SingleModuleIssue("PlotConsistency", "警告", "伏笔过早")));
            });
        var result = new UnifiedChapterValidationResult
        {
            ChapterId = "vol1_ch7",
            ChapterTitle = "第七章 命火",
            VolumeNumber = 1,
            ChapterNumber = 7,
            VolumeName = "第一卷"
        };

        await workflow.ExecuteAsync(result, "# 第七章 命火\n命火燃起。", CancellationToken.None);

        Assert.Equal("vol1_ch7", loadedContextChapterId);
        Assert.Equal("vol1_ch7", loadedIdsChapterId);
        Assert.Contains("东方玄幻模板: 类型=玄幻", capturedPrompt);
        Assert.Contains("一句话大纲=少年点燃命火", capturedPrompt);
        Assert.Equal("伏笔过早", Assert.Single(result.IssuesByModule["PlotConsistency"]).Message);
    }

    [Fact]
    public async Task ExecuteAsync_uses_empty_sources_when_context_or_ids_are_missing()
    {
        string? capturedPrompt = null;
        var workflow = new ValidationContextAIWorkflow(
            (_, _) => Task.FromResult<ValidationContext?>(null),
            (_, _) => Task.FromResult<ContextIdCollection?>(null),
            (prompt, _) =>
            {
                capturedPrompt = prompt;
                return Task.FromResult(ChapterAIValidationResponse.Failure("timeout"));
            });

        await workflow.ExecuteAsync(
            new UnifiedChapterValidationResult
            {
                ChapterId = "vol1_ch8",
                ChapterTitle = "第八章",
                VolumeNumber = 1,
                ChapterNumber = 8,
                VolumeName = "第一卷"
            },
            "正文",
            CancellationToken.None);

        Assert.Contains("<缺失数据说明>", capturedPrompt);
        Assert.Contains("- StyleConsistency（文风模板一致性）", capturedPrompt);
    }

    [Fact]
    public async Task ExecuteAsync_honors_cancellation_before_context_load()
    {
        var loadContextCalled = false;
        var workflow = new ValidationContextAIWorkflow(
            (_, _) =>
            {
                loadContextCalled = true;
                return Task.FromResult<ValidationContext?>(new ValidationContext());
            },
            (_, _) => Task.FromResult<ContextIdCollection?>(new ContextIdCollection()),
            (_, _) => Task.FromResult(ChapterAIValidationResponse.Failure("cancelled")));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            workflow.ExecuteAsync(new UnifiedChapterValidationResult { ChapterId = "vol1_ch9" }, "正文", cts.Token));

        Assert.False(loadContextCalled);
    }

    private static string SingleModuleIssue(string moduleName, string result, string summary)
    {
        var modules = TM.Services.Modules.ProjectData.Models.Validate.ValidationSummary.ValidationRules.AllModuleNames
            .Select(name => name == moduleName
                ? (object)new
                {
                    moduleName = name,
                    result,
                    problemItems = new[]
                    {
                        new { reason = "ValidationContextWorkflow", summary, suggestion = "调整" }
                    }
                }
                : new
                {
                    moduleName = name,
                    result = "通过",
                    problemItems = Array.Empty<object>()
                })
            .ToArray();

        return System.Text.Json.JsonSerializer.Serialize(new { moduleResults = modules });
    }
}
