using TM.Services.Framework.AI.SemanticKernel.References;
using Xunit;

namespace Tianming.AI.Tests;

public class ReferenceCatalogTests
{
    [Fact]
    public void GetAvailableTypes_matches_original_dropdown_reference_types()
    {
        var types = ReferenceCatalog.GetAvailableTypes();

        Assert.Collection(types,
            item =>
            {
                Assert.Equal("续写", item.Type);
                Assert.Equal("注入章节上下文", item.Description);
            },
            item =>
            {
                Assert.Equal("重写", item.Type);
                Assert.Equal("重写指定章节", item.Description);
            },
            item =>
            {
                Assert.Equal("仿写", item.Type);
                Assert.Equal("引用爬取内容", item.Description);
            });
    }

    [Fact]
    public void BuildReferenceToken_uses_book_name_for_imitate_and_id_for_other_types()
    {
        var chapter = new ReferenceItem { Id = "vol1_ch2", Name = "第二章 风雪" };
        var book = new ReferenceItem { Id = "book-1", Name = "雪中悍刀行" };

        Assert.Equal("@续写:vol1_ch2", ReferenceCatalog.BuildReferenceToken("续写", chapter));
        Assert.Equal("@重写:vol1_ch2", ReferenceCatalog.BuildReferenceToken("重写", chapter));
        Assert.Equal("@仿写:雪中悍刀行", ReferenceCatalog.BuildReferenceToken("仿写", book));
    }

    [Fact]
    public void FilterItems_matches_keyword_against_name_or_id_case_insensitively()
    {
        var items = new[]
        {
            new ReferenceItem { Id = "vol1_ch1", Name = "第一章 命火" },
            new ReferenceItem { Id = "VOL2_CH8", Name = "归墟" },
            new ReferenceItem { Id = "book-1", Name = "旧书" }
        };

        Assert.Equal(["第一章 命火"], ReferenceCatalog.FilterItems(items, "命火").Select(i => i.Name));
        Assert.Equal(["归墟"], ReferenceCatalog.FilterItems(items, "vol2").Select(i => i.Name));
        Assert.Equal(["第一章 命火", "归墟", "旧书"], ReferenceCatalog.FilterItems(items, " ").Select(i => i.Name));
    }

    [Fact]
    public void ReferenceParser_parses_colon_chinese_colon_and_whitespace_references()
    {
        var refs = ReferenceParser.Parse("请 @续写:vol1_ch2，然后 @rewrite：vol1_ch3，再 @仿写 雪中");

        Assert.Collection(refs,
            item =>
            {
                Assert.Equal("@续写:vol1_ch2", item.FullMatch);
                Assert.Equal("chapter", item.Type);
                Assert.Equal("vol1_ch2", item.Name);
            },
            item =>
            {
                Assert.Equal("@rewrite：vol1_ch3", item.FullMatch);
                Assert.Equal("rewrite", item.Type);
                Assert.Equal("vol1_ch3", item.Name);
            },
            item =>
            {
                Assert.Equal("@仿写 雪中", item.FullMatch);
                Assert.Equal("imitate", item.Type);
                Assert.Equal("雪中", item.Name);
            });
    }

    [Fact]
    public void ReplaceReferences_expands_from_end_without_disturbing_offsets()
    {
        var text = "A @续写:vol1_ch1 B @重写:vol1_ch2 C";

        var expanded = ReferenceParser.ReplaceReferences(text, reference => reference.Type switch
        {
            "chapter" => "[CH1]",
            "rewrite" => "[CH2]",
            _ => reference.FullMatch
        });

        Assert.Equal("A [CH1] B [CH2] C", expanded);
    }
}
