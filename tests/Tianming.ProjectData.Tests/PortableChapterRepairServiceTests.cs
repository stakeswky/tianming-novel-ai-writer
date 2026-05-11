using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class PortableChapterRepairServiceTests
{
    [Fact]
    public async Task RepairChapterAsync_requires_fact_snapshot_before_generating()
    {
        var generatorCalled = false;
        var service = new PortableChapterRepairService(
            (_, _) => Task.FromResult<string?>("正文"),
            (_, _) => Task.FromResult<ChapterRepairContext?>(new ChapterRepairContext { ContextMode = "packaged" }),
            (_, _, _, _) =>
            {
                generatorCalled = true;
                return Task.FromResult("should not run");
            },
            (_, _, _, _) => Task.CompletedTask);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RepairChapterAsync("vol1_ch1", ["问题"], CancellationToken.None));

        Assert.Contains("缺少 FactSnapshot", ex.Message);
        Assert.Contains("packaged", ex.Message);
        Assert.False(generatorCalled);
    }

    [Fact]
    public async Task RepairChapterAsync_composes_hints_and_remembers_snapshot_for_save()
    {
        var snapshot = new FactSnapshot();
        string? capturedHints = null;
        FactSnapshot? generatorSnapshot = null;
        FactSnapshot? savedSnapshot = null;
        string? savedContent = null;
        var service = new PortableChapterRepairService(
            (_, _) => Task.FromResult<string?>("旧正文\n---CHANGES---\n" + EmptyChangesJson()),
            (_, _) => Task.FromResult<ChapterRepairContext?>(new ChapterRepairContext
            {
                ContextMode = "packaged",
                FactSnapshot = snapshot
            }),
            (_, context, factSnapshot, _) =>
            {
                capturedHints = context.RepairHints;
                generatorSnapshot = factSnapshot;
                return Task.FromResult("修复正文");
            },
            (_, content, factSnapshot, _) =>
            {
                savedContent = content;
                savedSnapshot = factSnapshot;
                return Task.CompletedTask;
            });

        var result = await service.RepairChapterAsync("vol1_ch2", ["补足伏笔"], CancellationToken.None);
        await service.SaveRepairedAsync("vol1_ch2", result.Content, CancellationToken.None);

        Assert.Equal("修复正文", result.Content);
        Assert.Contains("<repair_directive>", capturedHints);
        Assert.Contains("1. 补足伏笔", capturedHints);
        Assert.DoesNotContain("---CHANGES---", capturedHints);
        Assert.Same(snapshot, generatorSnapshot);
        Assert.Equal("修复正文", savedContent);
        Assert.Same(snapshot, savedSnapshot);
    }

    [Fact]
    public async Task SaveRepairedAsync_requires_previous_successful_repair()
    {
        var service = new PortableChapterRepairService(
            (_, _) => Task.FromResult<string?>("正文"),
            (_, _) => Task.FromResult<ChapterRepairContext?>(new ChapterRepairContext()),
            (_, _, _, _) => Task.FromResult("修复正文"),
            (_, _, _, _) => Task.CompletedTask);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SaveRepairedAsync("vol1_ch3", "修复正文", CancellationToken.None));

        Assert.Contains("未找到 FactSnapshot", ex.Message);
    }

    [Fact]
    public async Task CheckNextChapterConsistencyAsync_returns_trimmed_next_title()
    {
        var service = new PortableChapterRepairService(
            (chapterId, _) => Task.FromResult<string?>(chapterId == "vol1_ch5" ? "# 第五章 余波\n\n正文" : null),
            (_, _) => Task.FromResult<ChapterRepairContext?>(new ChapterRepairContext()),
            (_, _, _, _) => Task.FromResult("修复正文"),
            (_, _, _, _) => Task.CompletedTask);

        var hint = await service.CheckNextChapterConsistencyAsync("vol1_ch4", "修复正文", CancellationToken.None);

        Assert.Equal("与下一章（第五章 余波）衔接：数据层一致 ✓", hint);
    }

    private static string EmptyChangesJson() =>
        """
        {
          "CharacterStateChanges": [],
          "ConflictProgress": [],
          "ForeshadowingActions": [],
          "NewPlotPoints": [],
          "LocationStateChanges": [],
          "FactionStateChanges": [],
          "TimeProgression": null,
          "CharacterMovements": [],
          "ItemTransfers": []
        }
        """;
}
