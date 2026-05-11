using TM.Services.Framework.AI.SemanticKernel.References;
using Xunit;

namespace Tianming.AI.Tests;

public class ReferenceExpansionServiceTests
{
    [Fact]
    public async Task ExpandReferencesAsync_replaces_chapter_reference_with_summary_and_key_excerpts()
    {
        var longSnippet = new string('片', 405);
        var service = new ReferenceExpansionService(
            (chapterId, _) => Task.FromResult<ReferenceChapterContext?>(new ReferenceChapterContext
            {
                ChapterId = chapterId,
                Title = "第一章 <命火> & \"归来\"",
                Summary = "林衡点燃命火。"
            }),
            (chapterId, topK, _) => Task.FromResult<IReadOnlyList<ReferenceSnippet>>(
            [
                new ReferenceSnippet { ChapterId = chapterId, Content = longSnippet },
                new ReferenceSnippet { ChapterId = chapterId, Content = "第二段关键片段" },
                new ReferenceSnippet { ChapterId = chapterId, Content = "第三段不应出现" }
            ]));

        var expanded = await service.ExpandReferencesAsync("请参考 @续写:vol1_ch1 继续。");

        Assert.Contains("<context_block type=\"chapter_reference\" title=\"第一章 &lt;命火&gt; &amp; &quot;归来&quot;\">林衡点燃命火。</context_block>", expanded);
        Assert.Contains("<context_block type=\"key_excerpts\">", expanded);
        Assert.Contains(new string('片', 400) + "…", expanded);
        Assert.Contains("第二段关键片段", expanded);
        Assert.DoesNotContain("第三段不应出现", expanded);
        Assert.DoesNotContain("@续写:vol1_ch1", expanded);
    }

    [Fact]
    public async Task ExpandReferencesAsync_reports_missing_chapter_id_and_missing_context()
    {
        var service = new ReferenceExpansionService(
            (_, _) => Task.FromResult<ReferenceChapterContext?>(null));

        Assert.Equal("请 [请指定章节ID]", await service.ExpandReferencesAsync("请 @续写"));
        Assert.Equal("请 [未找到章节: vol1_ch9]", await service.ExpandReferencesAsync("请 @重写:vol1_ch9"));
    }

    [Fact]
    public async Task ExpandReferencesAsync_leaves_imitate_reference_unchanged()
    {
        var service = new ReferenceExpansionService(
            (_, _) => throw new InvalidOperationException("chapter loader should not run"));

        Assert.Equal("参考 @仿写:雪中", await service.ExpandReferencesAsync("参考 @仿写:雪中"));
    }
}
