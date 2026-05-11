using System.Linq;
using TM.Services.Modules.ProjectData.Implementations;
using TM.Services.Modules.ProjectData.Models.Tracking;
using Xunit;

namespace Tianming.ProjectData.Tests;

public class ContentEntityExtractorTests
{
    [Fact]
    public void ExtractMentionedEntities_matches_primary_names_and_aliases()
    {
        var extractor = new ContentEntityExtractor(new FactSnapshot
        {
            CharacterDescriptions =
            {
                ["C7M3VT2K9P4NA"] = new CharacterCoreDescription { Name = "林衡（阿衡）" }
            },
            LocationDescriptions =
            {
                ["L7M3VT2K9P4NA"] = new LocationCoreDescription { Name = "寒潭秘境" }
            }
        });

        var mentioned = extractor.ExtractMentionedEntities("阿衡走入寒潭秘境，水声忽然变冷。");

        Assert.Contains("林衡（阿衡）", mentioned);
        Assert.Contains("寒潭秘境", mentioned);
    }

    [Fact]
    public void GetUnknownEntities_detects_likely_speaker_names_not_in_snapshot()
    {
        var extractor = new ContentEntityExtractor(["林衡"]);

        var unknown = extractor.GetUnknownEntities("\"青璃\"说道：此地不可久留。\"什么\"说道：你听见了吗？");

        Assert.Equal(["青璃"], unknown);
    }
}
