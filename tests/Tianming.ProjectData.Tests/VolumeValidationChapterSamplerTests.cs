using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Generated;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class VolumeValidationChapterSamplerTests
{
    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 3)]
    [InlineData(10, 3)]
    [InlineData(25, 5)]
    [InlineData(250, 50)]
    [InlineData(1000, 50)]
    public void CalculateSampleCount_matches_original_bounds(int totalCount, int expected)
    {
        Assert.Equal(expected, VolumeValidationChapterSampler.CalculateSampleCount(totalCount));
    }

    [Fact]
    public void Sample_returns_all_chapters_when_count_is_within_limit()
    {
        var chapters = MakeChapters(3);

        var sampled = VolumeValidationChapterSampler.SampleChapters(chapters, maxCount: 5);

        Assert.Equal([1, 2, 3], sampled.Select(chapter => chapter.ChapterNumber).ToArray());
    }

    [Fact]
    public void Sample_evenly_spreads_chapters_and_preserves_first_last()
    {
        var chapters = MakeChapters(10);

        var sampled = VolumeValidationChapterSampler.SampleChapters(chapters, maxCount: 4);

        Assert.Equal([1, 4, 7, 10], sampled.Select(chapter => chapter.ChapterNumber).ToArray());
    }

    [Fact]
    public void Sample_returns_chapters_ordered_by_chapter_number_after_sampling()
    {
        var chapters = MakeChapters(8)
            .OrderByDescending(chapter => chapter.ChapterNumber)
            .ToList();

        var sampled = VolumeValidationChapterSampler.SampleChapters(chapters, maxCount: 3);

        Assert.Equal([1, 4, 8], sampled.Select(chapter => chapter.ChapterNumber).ToArray());
    }

    [Fact]
    public void Sample_handles_empty_and_single_item_inputs()
    {
        Assert.Empty(VolumeValidationChapterSampler.SampleChapters([], maxCount: 3));

        var single = VolumeValidationChapterSampler.SampleChapters(MakeChapters(1), maxCount: 3);

        Assert.Equal("vol1_ch1", Assert.Single(single).Id);
    }

    private static List<ChapterInfo> MakeChapters(int count)
    {
        return Enumerable.Range(1, count)
            .Select(index => new ChapterInfo
            {
                Id = $"vol1_ch{index}",
                VolumeNumber = 1,
                ChapterNumber = index,
                Title = $"第{index}章"
            })
            .ToList();
    }
}
